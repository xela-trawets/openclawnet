# Sesión 3: Skills + Memoria

**Duración:** 50 minutos | **Nivel:** .NET Intermedio

## Descripción General

El mismo agente, usuarios diferentes — y quieres que vean comportamiento diferente. En esta sesión agregamos **skills** (archivos Markdown con frontmatter YAML que moldean el comportamiento del agente sin cambios de código) y **memoria** (summarización automática para gestionar ventanas de contexto, más búsqueda semántica en conversaciones pasadas). Al final, el agente es personalizado, eficiente con el contexto, y recuerda lo que pasó la última vez.

**Se basa en:** Sesión 1 (fundamentos) y Sesión 2 (herramientas + seguridad).

---

## Antes de la Sesión

### Requisitos Previos

- Sesión 2 completa y funcional
- LLM local ejecutándose (Ollama con el modelo `llama3.2` o Foundry Local)
- Comprensión de: async/await, E/S de archivos, conceptos básicos de EF Core
- Los archivos de skills de ejemplo ya existen en `skills/built-in/` y `skills/samples/`

### Punto de Partida

- El código de `session-2-complete`
- El bucle de herramientas está funcionando
- El orquestador del agente está operativo
- El esquema de base de datos es sólido

### Preparación del Presentador (10 min antes)

1. Ejecutar `aspire run` — verificar que la Sesión 2 funciona de extremo a extremo
2. Preparar un archivo de skill de prueba para la demostración en vivo
3. Navegar a la página de Skills — alternar uno para confirmar la carga
4. Preparar historial de conversación (ejecutar 20+ mensajes para activar la summarización)
5. Tener los skills de ejemplo listos para mostrar en pantalla

### Checkpoint de Git

**Tag de inicio:** `session-3-start` (alias: `session-2-complete`)
**Tag de fin:** `session-3-complete`

---

## Etapa 1: Sistema de Skills (12 min)

### Conceptos

- **¿Qué es un skill?** Un archivo Markdown con frontmatter YAML. No se necesitan cambios de código — coloca un archivo, recarga, y el agente se comporta diferente.
- **FileSkillLoader:** Escanea los directorios `skills/built-in/` y `skills/samples/` buscando archivos `*.md`, parsea cada uno, rastrea el estado habilitado/deshabilitado.
- **SkillParser:** Extrae el frontmatter YAML usando regex (`^---\s*\n(.*?)\n---\s*\n(.*)$`), devuelve metadatos + contenido.
- **Integración con DefaultPromptComposer:** Los skills activos se inyectan en el prompt del sistema como `## Skill: {nombre}\n{contenido}`. El agente los ve como instrucciones.

### Recorrido del Código

#### Archivo de Skill de Ejemplo (`dotnet-expert.md`)

```markdown
---
name: dotnet-expert
description: .NET development expertise and best practices
tags: [dotnet, csharp, programming, architecture]
enabled: true
---

You are a .NET expert assistant. When answering questions:

- Reference official Microsoft documentation
- Prefer modern C# features (records, pattern matching)
- Recommend Aspire for distributed applications
- Follow Microsoft coding conventions
```

**Qué explicar:** Campos del frontmatter YAML (`name`, `description`, `tags`, `enabled`). El contenido después del cierre `---` es Markdown puro — las instrucciones de comportamiento del agente.

#### Modelos SkillDefinition y SkillContent

```csharp
// Metadatos inmutables — lo que ve la UI
public sealed record SkillDefinition(
    string Name,
    string Description,
    string Category,
    string[] Tags,
    bool Enabled,
    string FilePath,
    string[] Examples);

// Lo que usa el compositor de prompts
public sealed record SkillContent(
    string Name,
    string Content,
    string Description,
    string[] Tags);
```

**Qué explicar:** Records sellados para inmutabilidad. `SkillDefinition` es para el listado/UI. `SkillContent` es lo que realmente usa el compositor de prompts.

#### Implementación de FileSkillLoader

```csharp
public class FileSkillLoader : ISkillLoader
{
    private readonly HashSet<string> _disabledSkills = new();

    public async Task<IReadOnlyList<SkillContent>>
        GetActiveSkillsAsync(CancellationToken ct)
    {
        // Escanear directorios buscando archivos *.md
        // Parsear cada uno con SkillParser
        // Devolver solo skills que NO están en _disabledSkills
    }

    public void EnableSkill(string name)
        => _disabledSkills.Remove(name);

    public void DisableSkill(string name)
        => _disabledSkills.Add(name);
}
```

**Qué explicar:** Seguridad de hilos mediante lock. El rastreo de deshabilitados es en memoria (`HashSet<string>`). `ReloadAsync()` re-escanea el directorio sin reiniciar.

#### Cómo los Skills se Integran en el Prompt del Sistema

```csharp
// DefaultPromptComposer.ComposeAsync()
var skills = await _skillLoader.GetActiveSkillsAsync(ct);
var skillText = string.Join("\n\n",
    skills.Select(s => $"## Skill: {s.Name}\n{s.Content}"));

var systemContent = DefaultSystemPrompt
    + $"\n\n# Active Skills\n{skillText}";
```

**Qué explicar:** El prompt del sistema se construye dinámicamente. Los skills aparecen como secciones Markdown que el LLM lee como instrucciones. Más skills = prompt más largo = más tokens.

### 🤖 Momento Copilot (~minuto 10)

**Objetivo:** Crear un archivo de skill completamente nuevo desde cero.

> Ver `copilot-prompts.md` → Prompt 1 para el prompt exacto.

**Resultado esperado:** Un archivo `security-auditor.md` completo con frontmatter YAML válido e instrucciones de comportamiento. Recargar skills y confirmar que aparece en la lista.

**Cómo probar:**
```bash
# Recargar skills vía API
curl -X POST http://localhost:5000/api/skills/reload

# Verificar que aparece
curl http://localhost:5000/api/skills | jq '.[] | select(.name == "security-auditor")'
```

---

## Etapa 2: Memoria y Summarización (15 min)

### Conceptos

- **Problema de la ventana de contexto:** Los LLMs tienen límites de tokens (ej., 8K–128K). Las conversaciones largas se llenan rápido. Enviar todo es caro y eventualmente falla.
- **Estrategia de summarización:** Mantener los últimos N mensajes completos. Comprimir los mensajes más antiguos en un resumen. Inyectar el resumen al inicio del prompt para que el agente tenga contexto sin el historial completo.
- **Búsqueda semántica con embeddings locales:** Convertir texto a vectores usando `Elbruno.LocalEmbeddings` (modelos ONNX, se ejecuta localmente). Encontrar conversaciones pasadas por significado, no solo coincidencia de palabras clave.

### Recorrido del Código

#### DefaultMemoryService

```csharp
public class DefaultMemoryService : IMemoryService
{
    private readonly IDbContextFactory<OpenClawDbContext> _dbFactory;

    public async Task<string?> GetSessionSummaryAsync(Guid sessionId)
    {
        // Devuelve el resumen más reciente de una sesión
    }

    public async Task StoreSummaryAsync(
        Guid sessionId, string summary, int messageCount)
    {
        // Persiste una nueva entidad SessionSummary
    }

    public async Task<MemoryStats> GetStatsAsync(Guid sessionId)
    {
        // Devuelve TotalMessages, SummaryCount,
        // CoveredMessages, LastSummaryAt
    }
}
```

**Qué explicar:** Usa `IDbContextFactory` (no un `DbContext` singleton) — patrón correcto para servicios async. `MemoryStats` le da a la UI transparencia sobre lo que el sistema de memoria está haciendo.

#### DefaultEmbeddingsService

```csharp
public class DefaultEmbeddingsService : IEmbeddingsService
{
    // Respaldado por Elbruno.LocalEmbeddings (modelo ONNX)
    // Se ejecuta localmente — sin llamadas API, los datos no salen de tu máquina

    public async Task<float[]> EmbedAsync(string text)
    {
        // Texto → vector de embedding
    }

    public float CosineSimilarity(float[] v1, float[] v2)
    {
        // producto punto / (magnitud1 * magnitud2)
    }
}
```

**Qué explicar:** Los embeddings son vectores numéricos que capturan significado. La similitud coseno mide qué tan "cercanos" son dos textos semánticamente. Ejecutar localmente significa sin llamadas API, los datos no salen de la máquina.

#### Entidad SessionSummary en Storage

```csharp
public sealed class SessionSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int CoveredMessageCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ChatSession Session { get; set; } = null!;
}
```

**Qué explicar:** Una sesión puede tener muchos resúmenes (a medida que la conversación crece). `CoveredMessageCount` rastrea cuántos mensajes fueron comprimidos. Eliminación en cascada con la sesión padre.

### Demostración en Vivo

1. **Activación de summarización:** Enviar 20+ mensajes en una conversación. Mostrar que los mensajes antiguos se resumen. Llamar al endpoint de stats para ver el conteo.
2. **Búsqueda semántica:** Buscar conversaciones pasadas por significado (ej., "inyección de dependencias" encuentra discusiones sobre DI aunque esas palabras exactas no se hayan usado).

```bash
# Ver stats de memoria
curl http://localhost:5000/api/memory/{sessionId}/stats

# Obtener todos los resúmenes
curl http://localhost:5000/api/memory/{sessionId}/summaries
```

---

## Etapa 3: Integración + UI (15 min)

### Conceptos

- **API de Skills:** Habilitar/deshabilitar skills en tiempo de ejecución sin reiniciar el servidor. Recargar para detectar nuevos archivos.
- **Stats de memoria:** Transparente para el usuario — pueden ver cuántos mensajes están resumidos, cuándo fue el último resumen, uso de tokens.
- **Patrón antes/después:** Activar un skill → hacer una pregunta → respuesta experta. Desactivarlo → misma pregunta → respuesta genérica.

### Recorrido del Código

#### SkillEndpoints en Gateway

```csharp
public static class SkillEndpoints
{
    public static void MapSkillEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/skills").WithTags("Skills");

        group.MapGet("/", async (ISkillLoader loader) =>
            Results.Ok(await loader.ListSkillsAsync()));

        group.MapPost("/reload", async (ISkillLoader loader) =>
            Results.Ok(new { reloaded = true,
                count = (await loader.ListSkillsAsync()).Count }));

        group.MapPost("/{name}/enable", (string name, ISkillLoader loader) => {
            loader.EnableSkill(name);
            return Results.Ok(new { name, enabled = true });
        });

        group.MapPost("/{name}/disable", (string name, ISkillLoader loader) => {
            loader.DisableSkill(name);
            return Results.Ok(new { name, enabled = false });
        });
    }
}
```

**Qué explicar:** Patrón de Minimal API — cada endpoint es un solo lambda. `ISkillLoader` se inyecta por DI. No se necesita reiniciar para habilitar/deshabilitar.

#### MemoryEndpoints en Gateway

```csharp
public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/memory").WithTags("Memory");

        group.MapGet("/{sessionId:guid}/summary",
            async (Guid sessionId, IMemoryService memoryService) =>
                Results.Ok(new { sessionId,
                    summary = await memoryService
                        .GetSessionSummaryAsync(sessionId) }));

        group.MapGet("/{sessionId:guid}/summaries",
            async (Guid sessionId, IMemoryService memoryService) =>
                Results.Ok(await memoryService
                    .GetAllSummariesAsync(sessionId)));

        group.MapGet("/{sessionId:guid}/stats",
            async (Guid sessionId, IMemoryService memoryService) =>
                Results.Ok(await memoryService
                    .GetStatsAsync(sessionId)));
    }
}
```

**Qué explicar:** Tres endpoints de solo lectura. Stats le da a la UI todo lo necesario para renderizar un panel de memoria. Este es el endpoint que el Momento Copilot extenderá.

### Demostración en Vivo

1. **Alternancia de skill — antes/después:**
   - Habilitar `dotnet-expert` → preguntar: "¿Cuál es la mejor forma de manejar DI en .NET?" → respuesta experta con patrones específicos
   - Deshabilitar `dotnet-expert` → misma pregunta → respuesta genérica
2. **Panel de stats de memoria:** Mostrar el componente de stats de memoria en la UI Blazor — total de mensajes, conteo de resúmenes, hora del último resumen.

### 🤖 Momento Copilot (~minuto 40)

**Objetivo:** Agregar un filtro de búsqueda por fecha a `MemoryEndpoints`.

> Ver `copilot-prompts.md` → Prompt 2 para el prompt exacto.

**Resultado esperado:** Un nuevo endpoint `GET /api/memory/{sessionId}/summaries?from=...&to=...` que filtra resúmenes por rango de `CreatedAt`.

**Cómo probar:**
```bash
# Obtener resúmenes de la última hora
curl "http://localhost:5000/api/memory/{sessionId}/summaries?from=2025-01-01T00:00:00Z&to=2025-12-31T23:59:59Z"
```

---

## Cierre (8 min)

### Idea Clave

> **"Los skills son solo markdown. La memoria es transparente."**

Cualquiera puede crear un skill — no se requiere C#. La gestión de memoria es visible para el usuario, no es una caja negra.

### Lo Que Construimos Hoy (Lista de Verificación)

- [x] Sistema de skills: archivos YAML + Markdown → comportamiento del agente
- [x] FileSkillLoader: escanear, parsear, habilitar/deshabilitar en tiempo de ejecución
- [x] DefaultPromptComposer: skills integrados en el prompt del sistema
- [x] DefaultMemoryService: summarización con persistencia en base de datos
- [x] DefaultEmbeddingsService: búsqueda semántica local
- [x] Endpoints de API de Skills (listar, habilitar, deshabilitar, recargar)
- [x] Endpoints de API de Memoria (resumen, stats)
- [x] Copilot: creó un archivo de skill desde cero
- [x] Copilot: agregó filtrado por fecha a los endpoints de memoria

### Vista Previa de la Sesión 4

Nuestro agente tiene personalidad y memoria. **Próxima sesión:** despliegue en la nube con Azure, pipelines CI/CD, configuración de producción y monitoreo. Llevamos OpenClawNet de localhost al mundo real.

---

## Referencia de Registro de DI

```csharp
// Registro de Skills
services.AddSingleton<ISkillLoader>(sp =>
    new FileSkillLoader(skillDirectories,
        sp.GetRequiredService<ILogger<FileSkillLoader>>()));

// Registro de Memoria
services.AddScoped<IMemoryService, DefaultMemoryService>();
services.AddSingleton<IEmbeddingsService, DefaultEmbeddingsService>();
```

## Proyectos Cubiertos

| Proyecto | LOC | Responsabilidad Principal |
|----------|-----|--------------------------|
| OpenClawNet.Skills | 237 | Definiciones de skills, carga, parseo |
| OpenClawNet.Memory | 234 | Summarización, embeddings, stats |
| OpenClawNet.Agent | — | DefaultPromptComposer (integración de skills) |
| OpenClawNet.Storage | — | Entidad SessionSummary |
| OpenClawNet.Gateway | — | SkillEndpoints, MemoryEndpoints |
