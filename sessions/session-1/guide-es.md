# Sesión 1: Fundamentos + Chat Local

**Duración:** 75-90 minutos | **Nivel:** .NET Intermedio | **Serie:** OpenClawNet — Microsoft Reactor

## Descripción General

Esta sesión presenta la arquitectura de la plataforma OpenClawNet y recorre cada capa necesaria para entregar un chatbot de IA funcional — desde la interfaz de abstracción del modelo hasta el streaming de tokens en tiempo real en el navegador mediante HTTP NDJSON. Al finalizar, los asistentes habrán visto el flujo vertical completo: una interfaz Blazor comunicándose con un endpoint HTTP NDJSON del Gateway, que llama a un cliente de modelo basado en un LLM local, Azure OpenAI o GitHub Copilot SDK, con conversaciones persistidas en SQLite a través de EF Core, todo orquestado por Aspire.

La sesión sigue un enfoque de **Explicar → Explorar → Extender**. Para cada etapa, primero explicamos los conceptos y decisiones de diseño, luego exploramos el código real juntos, y finalmente extendemos el código con una pequeña modificación asistida por Copilot. Esto mantiene la sesión interactiva sin requerir que los asistentes escriban grandes cantidades de código desde cero.

OpenClawNet es una plataforma preconstruida con 27 proyectos y ~4,300 líneas de código. El objetivo no es programar todo en vivo — el código ya está escrito y funcionando. En cambio, usamos la sesión para entender *por qué* existe cada pieza y *cómo* se conectan las capas. Los tres momentos con Copilot son completaciones pequeñas y enfocadas que refuerzan la comprensión de los patrones del código.

---

## Etapa 1: Arquitectura y Abstracciones Fundamentales (15 min)

### Conceptos a Explicar

- **Arquitectura de cortes verticales**: 27 proyectos enfocados organizados por responsabilidad — modelos, herramientas, almacenamiento, habilidades, memoria, runtime del agente e infraestructura. Cada proyecto tiene una única responsabilidad y se comunica a través de interfaces.
- **El contrato `IAgentProvider`**: La abstracción central que hace intercambiables a los proveedores de modelos.Cualquier proveedor de LLM (Ollama, Azure OpenAI, Microsoft Foundry, Foundry Local, GitHub Copilot SDK) implementa esta única interfaz a través del Microsoft Agent Framework (MAF). Esta es la decisión de diseño clave que evita la dependencia de un solo proveedor.
- **Records inmutables para DTOs**: `ChatRequest`, `ChatResponse` y `ChatMessage` son records de C# — inmutables, con igualdad por valor, y perfectos para transferencia de datos. Explicar por qué records en lugar de clases para este caso de uso.

### Recorrido por el Código

**Proyecto: `OpenClawNet.Models.Abstractions` (93 LOC)**

Recorrido por la interfaz principal y sus tipos de soporte:

```csharp
// IAgentProvider.cs — El contrato que cada proveedor implementa
public interface IAgentProvider
{
    string ProviderName { get; }
    IChatClient CreateChatClient(AgentProfile profile);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
```

Puntos clave a destacar:
- `CreateChatClient` devuelve una instancia de `IChatClient` configurada con el perfil del agente — este es el punto de integración con Microsoft Agent Framework (MAF)
- `IsAvailableAsync` habilita verificaciones de salud — crítico para el descubrimiento de servicios de Aspire
- La interfaz `IChatClient` de MAF maneja tanto completaciones con streaming como sin streaming — listo para producción desde el primer día

Luego mostrar los records de datos:

```csharp
// ChatRequest — Lo que enviamos al modelo
public sealed record ChatRequest
{
    public string? Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public IReadOnlyList<ToolDefinition>? Tools { get; init; }
    public double? Temperature { get; init; }
    public int? MaxTokens { get; init; }
}

// ChatResponse — Lo que el modelo devuelve
public sealed record ChatResponse
{
    public required string Content { get; init; }
    public required ChatMessageRole Role { get; init; }
    public IReadOnlyList<ToolCall>? ToolCalls { get; init; }
    public required string Model { get; init; }
    public UsageInfo? Usage { get; init; }
    public string? FinishReason { get; init; }
}
```

Mostrar cómo la inyección de dependencias conecta la implementación en `Gateway/Program.cs`:

```csharp
builder.Services.AddSingleton<IAgentProvider, OllamaAgentProvider>();
builder.Services.Configure<ModelOptions>(builder.Configuration.GetSection("Model"));
```

### Demo en Vivo

1. Abrir la solución en VS Code
2. Mostrar el Explorador de Soluciones con los 27 proyectos
3. Navegar desde `IAgentProvider` → `OllamaAgentProvider` → `ChatStreamEndpoints` para mostrar la cadena de dependencias
4. Abrir el diagrama `solution-structure.svg` para visualizar la organización del proyecto

### 🤖 Momento Copilot: Implementar un Nuevo Proveedor de Modelo

**Qué:** Dado `IAgentProvider` y `OllamaAgentProvider`, pedirle a Copilot que genere `FoundryLocalAgentProvider` desde cero.

**Cómo:**
1. Abrir `IAgentProvider.cs` y `OllamaAgentProvider.cs` lado a lado
2. Abrir Copilot Chat (Ctrl+Shift+I)
3. Referenciar ambos archivos con `#IAgentProvider.cs` y `#OllamaAgentProvider.cs`
4. Escribir: *"Usando OllamaAgentProvider como patrón de referencia, implementa FoundryLocalAgentProvider usando Microsoft.AI.Foundry.Local — respeta la firma del método CreateChatClient de IAgentProvider"*
5. Copilot genera una implementación completa y funcional en segundos
6. Revisar brevemente: nota que usa correctamente la API del SDK de FoundryLocal, implementa correctamente el método `CreateChatClient`

**Resultado esperado:** Una clase `FoundryLocalAgentProvider.cs` completa (~100 LOC) que implementa correctamente la interfaz `IAgentProvider`.

**Punto de enseñanza:** Esto es lo que permite el diseño limpio de interfaces — Copilot ve el contrato + un ejemplo concreto y extrapola una implementación funcional. Cuando tu código tiene buena estructura, la IA lo amplifica. La interfaz `IAgentProvider` es simple pero poderosa — es el punto de integración con MAF que hace que todos los proveedores sean intercambiables.

---

## Etapa 2: Proveedor de LLM Local + Capa de Datos (15 min)

### Conceptos a Explicar

- **¿Qué son los LLMs locales?** Runtimes como Ollama o Foundry Local que exponen modelos a través de una API REST. Sin nube, sin claves API, sin costo — perfecto para desarrollo y aprendizaje. Los modelos se ejecutan en tu máquina con `ollama pull llama3.2` o `foundrylocal model download --assigned phi-4`.
- **Streaming con IChatClient**: La interfaz `IChatClient` del Microsoft Agent Framework maneja respuestas con streaming usando `IAsyncEnumerable<StreamingChatCompletionUpdate>`. La implementación del proveedor envuelve la API de streaming del LLM subyacente.
- **EF Core code-first**: La capa de almacenamiento usa Entity Framework Core con SQLite. Las entidades se definen como clases de C#, y el esquema de base de datos se deriva de ellas. Sin SQL, sin migraciones necesarias para desarrollo — `EnsureCreatedAsync()` se encarga de todo.
- **Decisiones de diseño de entidades**: Eliminación suave mediante marcas de tiempo, `OrderIndex` para secuenciación de mensajes, `ToolCallsJson` nullable para datos estructurados de herramientas, y propiedades de navegación para datos relacionados.

### Recorrido por el Código

**Proyecto: `OpenClawNet.Models.Ollama` (181 LOC)**

Recorrido por la implementación del proveedor:

```csharp
// OllamaAgentProvider.cs — Método CreateChatClient
public IChatClient CreateChatClient(AgentProfile profile)
{
    // 1. Configurar el endpoint de Ollama y el modelo desde el perfil
    // 2. Devolver instancia de IChatClient que envuelve OllamaSharp
    // 3. El IChatClient maneja streaming vía IAsyncEnumerable
    // 4. Produce tokens conforme llegan del LLM
}
```

Puntos clave:
- El proveedor crea un `IChatClient` configurado para el modelo y endpoint específico
- El streaming se maneja a través de la interfaz `IChatClient` de MAF
- La implementación subyacente usa OllamaSharp para streaming NDJSON sobre HTTP
- Los tokens fluyen al consumidor conforme llegan — sin almacenamiento intermedio

**Proyecto: `OpenClawNet.Storage` (275 LOC)**

Recorrido por el modelo de entidades:

```csharp
// Entidades clave en la capa de almacenamiento
public sealed class ChatSession        // Contenedor de conversación
public sealed class ChatMessageEntity  // Mensajes individuales con rol + orden
public sealed class SessionSummary     // Resúmenes del sistema de memoria
public sealed class ToolCallRecord     // Registro de auditoría de ejecución de herramientas
public sealed class ScheduledJob       // Definiciones de trabajos recurrentes
public sealed class JobRun             // Historial de ejecución de trabajos
public sealed class ProviderSetting    // Configuración por proveedor
```

Luego mostrar `ConversationStore` — el patrón repositorio:

```csharp
public sealed class ConversationStore : IConversationStore
{
    Task<ChatSession> CreateSessionAsync(string? title = null, ...);
    Task<ChatSession?> GetSessionAsync(Guid sessionId, ...);
    Task<IReadOnlyList<ChatSession>> ListSessionsAsync(...);
    Task DeleteSessionAsync(Guid sessionId, ...);
    Task<ChatMessageEntity> AddMessageAsync(Guid sessionId, string role, string content, ...);
    Task<IReadOnlyList<ChatMessageEntity>> GetMessagesAsync(Guid sessionId, ...);
}
```

### Demo en Vivo

1. Verificar que Ollama está ejecutándose: `ollama list` (debería mostrar `llama3.2`)
2. Prueba rápida con curl: `curl http://localhost:11434/api/tags`
3. Mostrar el diagrama de relación de entidades (`entity-relationship.svg`)
4. Recorrer la configuración de `OpenClawNetDbContext`

### 🤖 Momento Copilot: Nuevo Método del Repositorio

**Qué:** Agregar un método `GetRecentSessionsAsync` a `ConversationStore`.

**Cómo:**
1. Abrir `ConversationStore.cs`
2. Posicionar el cursor después del último método
3. Comenzar a escribir la firma del método:

```csharp
public async Task<List<ChatSession>> GetRecentSessionsAsync(int count = 10)
```

4. Dejar que Copilot complete la implementación con sugerencia inline (Tab para aceptar)

**Resultado esperado:**
```csharp
public async Task<List<ChatSession>> GetRecentSessionsAsync(int count = 10)
{
    return await _context.ChatSessions
        .OrderByDescending(s => s.UpdatedAt)
        .Take(count)
        .ToListAsync();
}
```

**Punto de enseñanza:** Copilot genera consultas LINQ correctas a partir de las firmas de métodos. El nombre `GetRecentSessions` + el parámetro `count` + el tipo de retorno `List<ChatSession>` le dan suficiente contexto para producir `OrderByDescending` → `Take` → `ToListAsync` — exactamente el patrón que escribirías tú mismo. Esto demuestra cómo los métodos bien nombrados guían tanto a humanos como a IA.

---

## Etapa 3: Gateway + HTTP NDJSON + Blazor (15 min)

### Conceptos a Explicar

- **Patrón Minimal API**: Las Minimal APIs de ASP.NET Core reemplazan los controladores con mapeo de endpoints basado en lambdas. Los endpoints se organizan en clases estáticas de extensión (`ChatEndpoints`, `SessionEndpoints`) para una separación limpia.
- **HTTP NDJSON streaming**: El Gateway expone `POST /api/chat/stream`, que acepta un ChatStreamRequest y devuelve JSON delimitado por saltos de línea (NDJSON). Cada línea es un evento JSON discreto (`content`, `complete`, `error`, `tool_start`, etc.) que el cliente analiza incrementalmente. Los errores se muestran como códigos de estado HTTP — más simple y confiable que enfoques basados en WebSocket.
- **Orquestación con Aspire**: Aspire reemplaza Docker Compose + configuración manual de servicios. Un proyecto `AppHost` define la topología — qué servicios existen, sus dependencias, verificaciones de salud y variables de entorno. `aspire run` inicia todo.

### Recorrido por el Código

**Proyecto: `OpenClawNet.Gateway` (611 LOC)**

Mostrar la organización de endpoints:

```csharp
// Superficie de la API REST del Gateway
POST   /api/chat/stream              → Transmitir respuesta de chat como NDJSON
GET    /api/sessions/                 → Listar todas las sesiones
POST   /api/sessions/                 → Crear nueva sesión
GET    /api/sessions/{id}             → Obtener sesión con mensajes
DELETE /api/sessions/{id}             → Eliminar sesión
PATCH  /api/sessions/{id}/title       → Actualizar título de sesión
GET    /api/settings                  → Obtener configuración del proveedor
PUT    /api/settings                  → Actualizar configuración del proveedor
GET    /api/agent-profiles            → Listar todos los perfiles de agente
```

Luego recorrer `ChatStreamEndpoints` — el endpoint principal de streaming HTTP:

```csharp
// ChatStreamEndpoints.cs — Streaming HTTP NDJSON
app.MapPost("/api/chat/stream", async (
    ChatStreamRequest request,
    IAgentOrchestrator orchestrator,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    // 1. Validar entrada
    // 2. Establecer headers de respuesta para streaming NDJSON
    httpContext.Response.ContentType = "application/x-ndjson";
    httpContext.Response.Headers["Cache-Control"] = "no-cache";

    // 3. Transmitir eventos del orquestador como NDJSON
    await foreach (var evt in orchestrator.StreamAsync(agentRequest, cancellationToken))
    {
        var line = JsonSerializer.Serialize(streamEvent, JsonOpts);
        await httpContext.Response.WriteAsync(line + "\n", cancellationToken);
        await httpContext.Response.Body.FlushAsync(cancellationToken);
    }
})
.WithName("StreamChat")
.WithDescription("Stream a chat response as newline-delimited JSON events");
```

Puntos clave:
- `POST /api/chat/stream` acepta un `ChatStreamRequest` (SessionId, Message, Model opcional)
- El tipo de contenido de la respuesta es `application/x-ndjson` — cada línea es un objeto JSON completo
- Los tipos de eventos incluyen: `"content"` (delta de token), `"complete"` (terminado), `"error"`, `"tool_start"`, `"tool_complete"`
- Los errores se capturan y envían como eventos JSON (`ChatStreamEvent` con tipo `"error"`)
- Sin sobrecarga de WebSocket — HTTP estándar — más resistente y depurable

Mostrar el registro de DI en `Program.cs`:

```csharp
// Gateway/Program.cs — Composición completa de DI
builder.AddServiceDefaults();          // Telemetría de Aspire + salud
builder.Services.AddOpenClawStorage(); // EF Core + SQLite
builder.Services.AddSingleton<IAgentProvider, OllamaAgentProvider>(); // Proveedor de LLM
builder.Services.AddAgentRuntime();    // Orquestador + compositor de prompts
builder.Services.AddHostedService<JobSchedulerService>(); // Trabajos en segundo plano
```

**Proyecto: `OpenClawNet.AppHost` (18 LOC)**

```csharp
// AppHost.cs — La topología completa en 18 líneas
var gateway = builder.AddProject<Projects.OpenClawNet_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithEnvironment("ConnectionStrings__DefaultConnection", sqliteConnectionString)
    .WithEnvironment("Model__Endpoint", ollamaEndpoint);

builder.AddProject<Projects.OpenClawNet_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(gateway)     // Descubrimiento de servicios
    .WaitFor(gateway)           // Orden de inicio
    .WithEnvironment("OpenClawNet__OllamaBaseUrl", ollamaEndpoint);
```

### Demo en Vivo: Cambio de Proveedor sin Cambios de Código

**Demostración de configuración en tiempo de ejecución**: OpenClawNet permite cambiar proveedores de LLM a través de la página de Proveedores de Modelo (`/model-providers`) — navegar a la página de Proveedores de Modelo, configurar un proveedor diferente (Ollama → Azure OpenAI → GitHub Copilot SDK), y enviar un mensaje para verlo funcionar inmediatamente. El proveedor activo y el modelo se muestran en el badge del selector de agente en la página de chat.

**Demo del stack completo:**
1. Ejecutar `aspire run` desde la raíz del repositorio
2. Abrir el Dashboard de Aspire (`https://localhost:15100`) — mostrar la topología de servicios
3. Abrir la interfaz Blazor (`http://localhost:5001`)
4. Crear una nueva sesión de chat
5. Enviar un mensaje: *"¿Qué es Aspire y por qué debería usarlo?"*
6. Observar los tokens llegando en tiempo real — señalar el efecto de "escritura"
7. Abrir DevTools del navegador, pestaña Red — mostrar POST HTTP a `/api/chat/stream`, respuesta NDJSON con líneas JSON incrementales

**Enfoque alternativo de demo**: El archivo `bonus-demos.md` contiene un enfoque basado en archivos de configuración para cambiar proveedores para quienes prefieren configuración basada en código.

---

## Cierre (5 min)

### Resumen: Lo Que Construimos

Recorrer el flujo completo una vez más:

1. **El usuario escribe un mensaje** en la interfaz Blazor
2. **HTTP POST enviado** al endpoint `/api/chat/stream` del Gateway
3. **El orquestador** compone un prompt y llama a `IAgentProvider.CreateChatClient()` para obtener un `IChatClient`
4. **El IChatClient** (respaldado por OllamaAgentProvider, Azure OpenAI o GitHub Copilot SDK) envía una solicitud al proveedor del modelo
5. **Los tokens regresan** vía la interfaz de streaming de MAF → respuesta HTTP NDJSON → navegador (analizados incrementalmente)
6. **La conversación se persiste** en SQLite a través de `ConversationStore`
7. **Aspire orquesta** todo el inicio con verificaciones de salud y descubrimiento de servicios

### Por Qué Importa Cada Capa

- **`IAgentProvider`** — Cambiar el LLM local por Azure OpenAI sin modificar ningún otro código. La abstracción MAF hace que todos los proveedores sean intercambiables.
- **`ConversationStore`** — Historial, contexto y registro de auditoría
- **HTTP NDJSON** — Streaming confiable y depurable sin sobrecarga de WebSocket. Los errores aparecen como códigos de estado HTTP.
- **Aspire** — Un comando para ejecutar, observar y depurar todo el stack

### Vista Previa: Sesión 2

> "Tenemos un chatbot que puede responder preguntas. ¿Pero qué tal si pudiera *hacer* cosas? En la Sesión 2, le daremos herramientas — acceso al sistema de archivos, obtención de páginas web, ejecución de shell — y construiremos el bucle del agente que decide cuándo y cómo usarlas."

### Recursos

- 📦 **Repositorio en GitHub**: [github.com/elbruno/openclawnet](https://github.com/elbruno/openclawnet)
- 📖 **Documentación de Aspire**: [learn.microsoft.com/dotnet/aspire](https://learn.microsoft.com/dotnet/aspire)
- 🦙 **Ollama**: [ollama.com](https://ollama.com)
- 🏭 **Foundry Local**: [github.com/microsoft/foundry-local](https://github.com/microsoft/foundry-local)
- 📡 **HTTP NDJSON**: [developer.mozilla.org/en-US/docs/Web/API/Server-sent_events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events)
- 🤖 **GitHub Copilot**: [github.com/features/copilot](https://github.com/features/copilot)
