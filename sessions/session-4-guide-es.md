# Sesión 4: Automatización + Nube

**Duración:** 50 minutos | **Nivel:** .NET Intermedio

---

## Descripción General

Tu agente funciona localmente. A los usuarios les encanta. Ahora tres desafíos de producción se interponen entre tú y una plataforma real:

1. **Proveedores en la nube** — Los LLMs locales (Ollama, Foundry Local) son geniales para desarrollo, pero producción necesita GPT-4o, SLAs y acceso compartido para el equipo.
2. **Programación automatizada** — El agente debe ejecutar tareas en segundo plano sin que un usuario esté presente.
3. **Testing** — No puedes lanzar lo que no puedes probar. 24 tests demuestran que la arquitectura funciona.

Este es el **gran final de la serie**. Al terminar esta sesión, cada pieza se conecta: chat, herramientas, habilidades, memoria, programación de tareas, health checks y proveedores en la nube — una plataforma completa de agentes de IA construida con .NET.

---

## Antes de la Sesión

### Requisitos Previos

- Sesión 3 completa y funcionando
- .NET 10 SDK, VS Code o Visual Studio
- LLM local ejecutándose (Ollama con el modelo `llama3.2` o Foundry Local)
- Comprensión de: background services, HTTP clients, testing unitario
- **Opcional:** Cuenta de Azure con acceso a Azure OpenAI o Foundry

### Punto de Partida

- El código de `session-3-complete`
- Orquestación completa del agente con habilidades y memoria
- Todas las herramientas implementadas y funcionando
- Base de datos con historial de conversaciones

### Git Checkpoint

**Tag inicial:** `session-4-start` (alias: `session-3-complete`)
**Tag final:** `session-4-complete`

---

## Etapa 1: Proveedores en la Nube (12 min)

### Conceptos

**¿Por qué la nube? Más allá de los LLMs locales.**
Los LLMs locales como Ollama y Foundry Local son perfectos para desarrollo — gratis, local, sin credenciales. Pero producción necesita más:
- **Calidad GPT-4o** — Mejor razonamiento, contexto más largo, confiabilidad en tool calling
- **SLAs** — Garantías de 99.9% de uptime, no "mi laptop está encendida"
- **Compartir en equipo** — Un endpoint, muchos desarrolladores, facturación centralizada
- **Cumplimiento** — Residencia de datos, registros de auditoría, seguridad empresarial

**Polimorfismo de IModelClient.**
La magia: una interfaz, tres implementaciones. Tu código de agente no cambia — solo cambia el registro de DI.

```csharp
public interface IModelClient
{
    string ProviderName { get; }
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct);
    IAsyncEnumerable<ChatResponseChunk> StreamAsync(ChatRequest request, CancellationToken ct);
    Task<bool> IsAvailableAsync(CancellationToken ct);
}
```

**Configuración de proveedores con el patrón Options.**
Cada proveedor tiene su propia clase de opciones (`AzureOpenAIOptions`, `FoundryOptions`), vinculada desde `appsettings.json` o variables de entorno. Separación limpia de configuración y código.

### Recorrido por el Código

#### AzureOpenAIModelClient (137 LOC)

```csharp
public sealed class AzureOpenAIModelClient : IModelClient
{
    public string ProviderName => "azure-openai";

    public AzureOpenAIModelClient(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIModelClient> logger) { ... }

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        // Usa el SDK Azure.AI.OpenAI
        // Mapea ChatMessage → OpenAI.Chat.ChatMessage
        // Retorna ChatResponse estructurado con info de uso
    }

    public async IAsyncEnumerable<ChatResponseChunk> StreamAsync(...)
    {
        // Streaming con SDK usando await foreach
        // Produce ChatResponseChunk por cada token
    }
}
```

**Puntos clave:**
- Usa el paquete NuGet oficial `Azure.AI.OpenAI`
- `MapMessages()` convierte los `ChatMessage` de OpenClawNet a los tipos del SDK
- Streaming usa `IAsyncEnumerable` — mismo patrón que el cliente de LLM local
- Configuración vía `AzureOpenAIOptions`: Endpoint, ApiKey, DeploymentName, Temperature, MaxTokens

#### FoundryModelClient (195 LOC)

```csharp
public sealed class FoundryModelClient : IModelClient
{
    public string ProviderName => "foundry";

    public FoundryModelClient(
        HttpClient httpClient,
        IOptions<FoundryOptions> options,
        ILogger<FoundryModelClient> logger) { ... }

    public async Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct)
    {
        EnsureConfigured();
        var payload = BuildPayload(request, stream: false);
        // HTTP POST directo — sin SDK necesario
        var response = await _httpClient
            .PostAsJsonAsync(url, payload, ct);
        return MapFoundryResponse(response);
    }
}
```

**Puntos clave:**
- Sin SDK — `HttpClient` directo con DTOs personalizados (`FoundryChatResponse`, `FoundryChoice`, etc.)
- `BuildPayload()` construye el cuerpo de la petición
- `EnsureConfigured()` valida endpoint/key antes de las llamadas
- Muestra lo que significa "forma de API diferente" en la práctica

#### Registro de DI — Cambiar de Proveedor

```csharp
// En Program.cs o startup — elige UNO:

// Opción A: Desarrollo local
services.AddOllama(o => o.Model = "llama3.2");

// Opción B: Azure OpenAI
services.AddAzureOpenAI(o => {
    o.Endpoint = config["AzureOpenAI:Endpoint"]!;
    o.ApiKey = config["AzureOpenAI:ApiKey"]!;
    o.DeploymentName = "gpt-4o";
});

// Opción C: Microsoft Foundry
services.AddFoundry(o => {
    o.Endpoint = config["Foundry:Endpoint"]!;
    o.ApiKey = config["Foundry:ApiKey"]!;
    o.Model = "gpt-4o";
});
```

Los tres se registran como `IModelClient`. El agente, el compositor de prompts y el loop de herramientas nunca saben qué proveedor está activo.

### Demo en Vivo

1. Mostrar el chat actual funcionando con el LLM local
2. Cambiar a Azure OpenAI (si está disponible) cambiando el registro de DI
3. Misma interfaz de chat, mismas herramientas, mismas habilidades — diferente proveedor en la nube
4. Comparar calidad y velocidad de respuesta lado a lado
5. **Alternativa:** Si no hay Azure, mostrar la configuración y explicar — el código es el mismo de cualquier forma

---

## Etapa 2: Programación + Salud (12 min)

### Conceptos

**Patrón BackgroundService.**
El `BackgroundService` de ASP.NET Core ejecuta tareas junto a tu aplicación web. Sin proceso separado, sin Windows Service — solo sobrescribe `ExecuteAsync` y haz un loop.

**Programación basada en cron.**
Los usuarios (o el propio agente) crean tareas con expresiones cron. El programador revisa cada 30 segundos si hay tareas pendientes y las ejecuta.

**Health checks + integración con Aspire.**
Las aplicaciones en producción necesitan responder: "¿Estás saludable?" Los health checks de ASP.NET Core proporcionan endpoints `/health` y `/alive`. El Dashboard de Aspire muestra todo en una vista.

### Recorrido por el Código

#### JobSchedulerService

```csharp
public class JobSchedulerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            // Consultar BD: tareas donde IsActive && NextRunTime <= ahora
            // Para cada tarea pendiente:
            //   Crear registro JobRun (Status=Running)
            //   Llamar orchestrator.RunAsync con petición sintética
            //   Capturar salida y resultado
            //   Actualizar JobRun (Status=Success/Failed)
            //   Calcular NextRunTime desde cron
        }
    }
}
```

**Puntos clave:**
- Consulta cada 30 segundos — simple, confiable, sin programador externo necesario
- Crea registros `JobRun` para pista de auditoría
- Usa el mismo `IAgentOrchestrator` que el chat — acceso completo a herramientas/habilidades
- Apagado graceful vía `CancellationToken`

#### SchedulerTool — El Agente se Programa a Sí Mismo

```csharp
public sealed class SchedulerTool : ITool
{
    public string Name => "schedule";
    // Acciones: "create", "list", "cancel"

    private async Task<ToolResult> CreateJobAsync(ToolInput input, ...)
    {
        // Usuario: "Recuérdame cada mañana a las 9 AM"
        // → El agente llama a la herramienta schedule
        // → Crea tarea cron: "0 9 * * *"
        // → El programador la ejecuta automáticamente
    }
}
```

Esto es poderoso: un usuario dice "Recuérdame cada mañana a las 9 AM de revisar mi calendario" y el agente llama a la herramienta schedule para crear una tarea recurrente.

#### ServiceDefaults — Salud + Telemetría

```csharp
// Una llamada para gobernarlos a todos:
builder.AddServiceDefaults();
```

**Endpoints de salud:**
- `GET /health` — Probe de preparación (base de datos, proveedor de modelo)
- `GET /alive` — Probe de vida (el proceso está corriendo)

**OpenTelemetry:**
- Métricas de ASP.NET Core (duración de peticiones, tasas de error)
- Métricas de HttpClient (seguimiento de llamadas salientes)
- Métricas de runtime (GC, thread pool)
- Exportador OTLP para el Dashboard de Aspire

### Demo en Vivo

1. Mostrar el Dashboard de Aspire — todos los servicios visibles
2. Programar una tarea vía la API: "Revisar el clima cada hora"
3. Mostrar la tarea en la base de datos
4. Llamar a `GET /health` — mostrar la respuesta JSON
5. Señalar las trazas de OpenTelemetry en Aspire

---

## Etapa 3: Testing + Producción (12 min)

### Conceptos

**Pirámide de testing: unitario → integración → E2E.**
- **Tests unitarios (23):** Prueban componentes individuales aislados. Rápidos, confiables, sin dependencias externas.
- **Tests de integración (1):** Prueban componentes trabajando juntos. Usa base de datos real (en memoria).
- **E2E:** Demo manual — recorrer el flujo completo.

**Qué simular (mock).**
- `IModelClient` — No llamar a la IA real en tests
- `IDbContextFactory` — Usar proveedor in-memory de EF Core
- `ITool` — Herramientas falsas para tests del executor
- `ISkillLoader` — Cargador de habilidades falso para tests del compositor

**Lista de verificación de producción:**
- ✅ Health checks respondiendo
- ✅ Todos los tests pasando
- ✅ Cambio de proveedor funciona
- ✅ Tareas programadas ejecutándose
- ✅ OpenTelemetry exportando
- ✅ Manejo de errores graceful

### Recorrido por el Código

#### PromptComposerTests — Verificar Inyección de Habilidades

```csharp
[Fact]
public async Task ComposeAsync_IncludesActiveSkills()
{
    // Arrange: FakeSkillLoader retorna habilidad "code-review"
    // Act: composer.ComposeAsync(context)
    // Assert: el prompt del sistema contiene nombre y contenido de la habilidad
}
```

4 tests cubriendo: presencia de prompt del sistema, inyección de habilidades, resumen de sesión, historial de conversación.

#### ToolExecutorTests — Cumplimiento de Política de Aprobación

```csharp
[Fact]
public async Task ExecuteAsync_ReturnsFail_WhenToolNotFound()
{
    // Verifica fallo graceful para herramientas desconocidas
}

[Fact]
public async Task ExecuteAsync_CallsTool_WhenFound()
{
    // Registra SuccessTool, ejecuta, verifica la salida
}
```

3 tests cubriendo: manejo de herramienta faltante, ejecución exitosa, ejecución por lotes.

#### SkillParserTests — Casos Extremos de Parsing YAML

```csharp
[Fact]
public void Parse_WithValidFrontmatter_ExtractsMetadata()
{
    // YAML completo: name, description, category, tags
    // Verifica que todos los campos se extraen correctamente
}

[Fact]
public void Parse_WithoutFrontmatter_UsesFileName()
{
    // Contenido plano → el nombre del archivo se convierte en nombre de habilidad
}
```

4 tests cubriendo: frontmatter válido, sin frontmatter, flag deshabilitado, contenido vacío.

#### ConversationStoreTests — EF Core en Memoria

```csharp
public class ConversationStoreTests : IDisposable
{
    // Usa TestDbContextFactory con base de datos en memoria
    // Cada test obtiene una base de datos con nombre Guid fresco

    [Fact]
    public async Task AddMessage_IncrementsOrderIndex()
    {
        // Agrega 2 mensajes, verifica OrderIndex: 0, 1
    }
}
```

7 tests cubriendo: crear, obtener, agregar mensaje, índice de orden, listar, eliminar, actualizar título.

#### ToolRegistryTests — Registro + Búsqueda

5 tests cubriendo: registro, búsqueda insensible a mayúsculas, no encontrado, obtener todos, manifiesto.

### Demo en Vivo

1. Ejecutar `dotnet test` → las 24 pasan (23 unitarios + 1 integración)
2. Mostrar la salida de los tests con categorías
3. Señalar los patrones de testing: Arrange/Act/Assert, implementaciones falsas, BD en memoria

### 🤖 Momento Copilot — Escribir un Nuevo Test

**Contexto:** `ToolRegistryTests.cs` está abierto en el editor.

**Prompt para Copilot:**
> Escribe un nuevo test unitario para ToolRegistry que verifique que registrar una herramienta con un nombre duplicado sobrescribe el registro anterior. Registra dos instancias diferentes de FakeTool con el mismo nombre, luego verifica que GetTool retorna la segunda.

**Esperado:** Copilot genera un método de test `[Fact]` que:
- Crea dos instancias de `FakeTool` con el mismo `Name`
- Registra ambas
- Verifica que `GetTool` retorna la segunda instancia

---

## Cierre (14 min) — GRAN FINAL DE LA SERIE

### Demo Completa de la Plataforma (5 min)

Recorrer toda la plataforma de extremo a extremo:
1. Iniciar la app con Aspire
2. Abrir el chat — enviar un mensaje (Sesión 1)
3. Usar una herramienta — "¿Qué archivos hay en el proyecto?" (Sesión 2)
4. Activar una habilidad — habilitar modo "code-review" (Sesión 3)
5. Programar una tarea — "Recuérdame en 5 minutos" (Sesión 4)
6. Verificar salud — `GET /health` (Sesión 4)
7. Mostrar Dashboard de Aspire — todos los servicios, trazas, métricas

### Resumen de la Serie (4 min)

| Sesión | Tema | Lo Que Construimos |
|--------|------|--------------------|
| **1** | Scaffolding + Chat Local | Host Aspire, integración de LLM local, gateway, chat UI |
| **2** | Herramientas + Flujos de Agente | Interfaz ITool, registry, executor, políticas de aprobación, tool loop |
| **3** | Habilidades + Memoria | Habilidades Markdown, parsing YAML, sumarización, búsqueda semántica |
| **4** | Automatización + Nube | Proveedores en la nube, programación de tareas, health checks, testing |

### Diagrama de Arquitectura

```
┌─────────────────────────────────────────────────────────┐
│                  Plataforma OpenClawNet                  │
├──────────┬──────────┬──────────┬───────────────────────────┤
│  Web UI  │ REST API │  Aspire  │     Health Checks       │
├──────────┴──────────┴──────────┴───────────────────────────┤
│                Orquestador de Agente                      │
│         ┌──────────────────────────────┐                  │
│         │    Compositor de Prompts     │                  │
│         │ Sistema + Habilidades + Mem  │                  │
│         └──────────────────────────────┘                  │
├──────────┬──────────┬──────────┬───────────────────────────┤
│Herramien │Habilidad │ Memoria  │   Programador           │
│  -tas    │   -es    │          │ BackgroundService       │
├──────────┴──────────┴──────────┴───────────────────────────┤
│              Capa de Abstracción de Modelos                │
│         ┌────────┬──────────┬──────────┐                  │
│         │ Ollama │ Azure AI │ Foundry  │                  │
│         └────────┴──────────┴──────────┘                  │
├────────────────────────────────────────────────────────────┤
│           Almacenamiento (EF Core + SQLite)               │
└────────────────────────────────────────────────────────────┘
```

### Hacia Dónde Ir Desde Aquí

- **Herramientas personalizadas:** Construir herramientas específicas de dominio (Jira, GitHub, Slack)
- **Habilidades de dominio:** Crear paquetes de habilidades especializadas para tu equipo
- **Despliegue en Azure:** Desplegar en Azure Container Apps con Aspire
- **Memoria avanzada:** RAG con búsqueda vectorial, bases de conocimiento a largo plazo
- **Multi-agente:** Patrones de comunicación agente-a-agente
- **Integración con GitHub Copilot:** Usar Copilot para extender la plataforma misma

### Gracias + Preguntas y Respuestas

- Repositorio: `github.com/elbruno/openclawnet`
- Serie: Microsoft Reactor — OpenClawNet
- Construido con: .NET 10, Aspire, GitHub Copilot, LLMs locales (Ollama / Foundry Local)

---

## Después de la Sesión

### Lo Que Ahora Funciona

- ✅ Cambio de proveedor en la nube (LLM local → Azure OpenAI → Foundry)
- ✅ Programación de tareas en segundo plano con expresiones cron
- ✅ Endpoints de health check (`/health`, `/alive`)
- ✅ Métricas y trazas de OpenTelemetry
- ✅ 24 tests pasando (23 unitarios + 1 integración)
- ✅ Plataforma completa de agente IA — lista para producción

### Conceptos Clave Cubiertos

1. Polimorfismo de `IModelClient` — una interfaz, múltiples proveedores en la nube
2. Patrón Options para configuración de proveedores
3. `BackgroundService` para tareas de larga ejecución
4. Programación basada en cron con pista de auditoría
5. Health checks de ASP.NET Core e integración con Aspire
6. OpenTelemetry para observabilidad
7. Patrones de testing unitario: fakes, BD en memoria, Arrange/Act/Assert
8. Lista de verificación de preparación para producción

### Git Checkpoint

**Tag:** `session-4-complete`

**Archivos cubiertos:**
- `src/OpenClawNet.Models.AzureOpenAI/` — Cliente de Azure OpenAI
- `src/OpenClawNet.Models.Foundry/` — Cliente de Foundry
- `src/OpenClawNet.Models.Abstractions/` — Interfaz IModelClient
- `src/OpenClawNet.Tools.Scheduler/` — SchedulerTool
- `src/OpenClawNet.Gateway/Services/JobSchedulerService.cs` — Programador en segundo plano
- `src/OpenClawNet.ServiceDefaults/` — Health checks + telemetría
- `tests/OpenClawNet.UnitTests/` — 23 tests unitarios
- `tests/OpenClawNet.IntegrationTests/` — 1 test de integración
