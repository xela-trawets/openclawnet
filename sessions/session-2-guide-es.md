# Sesión 2: Tools + Agent Workflows

**Duración:** 50 minutos | **Nivel:** .NET Intermedio

## Descripción General

En la arquitectura de agentes, una **herramienta (tool)** es cualquier capacidad que la IA puede invocar para interactuar con el mundo — leer archivos, ejecutar comandos, obtener páginas web, programar tareas. La diferencia entre un chatbot y un agente es simple: **un chatbot genera texto; un agente usa herramientas**.

Esta sesión sigue el enfoque **"Explicar → Explorar → Extender"**: explicamos la arquitectura, exploramos el código pre-construido y lo extendemos con pequeños cambios asistidos por Copilot. La seguridad es una preocupación de primera clase — cada herramienta tiene protecciones de defensa en profundidad contra vectores de ataque del mundo real.

### Lo Que los Asistentes Entenderán

- Cómo la capa de abstracción de herramientas habilita la extensibilidad
- Por qué las puertas de seguridad (aprobación, validación, listas de bloqueo) son esenciales
- Cómo el bucle del agente coordina el razonamiento del modelo con la ejecución de herramientas
- La separación entre `IToolRegistry` (qué herramientas existen) e `IToolExecutor` (cómo se ejecutan de forma segura)

---

## Etapa 1: Arquitectura de Herramientas (12 min)

### Conceptos a Explicar

- **Qué diferencia a un agente de un chatbot**: ¡El uso de herramientas! Un chatbot genera texto. Un agente decide que necesita *hacer algo* — leer un archivo, ejecutar un comando, obtener una URL — y solicita una llamada a herramienta. El modelo no ejecuta nada; emite una solicitud estructurada de herramienta que nuestro código ejecuta.
- **Interfaz ITool**: Cada herramienta implementa `ITool` con cuatro miembros:
  - `Name` — identificador único (ej., `"file_system"`, `"shell"`)
  - `Description` — qué hace la herramienta (se envía al LLM)
  - `Metadata` — esquema de parámetros, requisitos de aprobación, categoría, etiquetas
  - `ExecuteAsync(ToolInput, CancellationToken)` — la ejecución real
- **Políticas de aprobación**: `IToolApprovalPolicy` con dos métodos:
  - `RequiresApprovalAsync` — ¿esta herramienta necesita aprobación humana?
  - `IsApprovedAsync` — ¿ha sido aprobada?
  - Incluido: `AlwaysApprovePolicy` (aprobación automática para todo)
  - ShellTool establece `RequiresApproval = true` en sus metadatos
- **Separación de responsabilidades entre IToolRegistry e IToolExecutor**:
  - `IToolRegistry` gestiona el descubrimiento de herramientas: `Register`, `GetTool`, `GetAllTools`, `GetToolManifest`
  - `IToolExecutor` gestiona la ejecución segura: búsqueda → verificación de aprobación → ejecución → registro
  - ¿Por qué separar? El registro trata sobre *qué existe*; el ejecutor trata sobre *cómo ejecutar de forma segura*

### Recorrido por el Código

#### Tools.Abstractions (7 archivos, 90 LOC)

Recorrido breve por cada archivo:

1. **`ITool.cs`** — La interfaz principal. Cada herramienta del sistema la implementa. Señalar que `ExecuteAsync` devuelve `ToolResult`, no cadenas de texto.

2. **`IToolExecutor.cs`** — Dos métodos: `ExecuteAsync` (herramienta individual) y `ExecuteBatchAsync` (múltiples herramientas). El ejecutor no conoce herramientas específicas — usa el registro.

3. **`IToolRegistry.cs`** — Cuatro métodos: `Register`, `GetTool`, `GetAllTools`, `GetToolManifest`. El manifiesto devuelve solo metadatos (sin capacidad de ejecución) — seguro para exponer al modelo.

4. **`IToolApprovalPolicy.cs`** — La interfaz de puerta de seguridad. Incluye `AlwaysApprovePolicy` como predeterminada. En producción, implementarías una política que verifique permisos de usuario.

5. **`ToolInput.cs`** — Envuelve argumentos JSON crudos con métodos auxiliares: `GetArgument<T>`, `GetStringArgument`. Usa `JsonDocument.Parse` para acceso sin asignación de memoria.

6. **`ToolMetadata.cs`** — Lo que el LLM ve: Name, Description, `ParameterSchema` (JSON Schema), `RequiresApproval`, Category, Tags.

7. **`ToolResult.cs`** — Éxito/fallo con output, error y duración. Métodos fábrica: `ToolResult.Ok(...)` y `ToolResult.Fail(...)`.

#### Tools.Core (3 archivos, 101 LOC)

1. **`ToolExecutor.cs`** — El patrón de puerta de aprobación:
   ```csharp
   // 1. Búsqueda
   var tool = _registry.GetTool(toolName);
   if (tool is null) return ToolResult.Fail(...);

   // 2. Verificación de aprobación
   if (await _approvalPolicy.RequiresApprovalAsync(toolName, arguments) &&
       !await _approvalPolicy.IsApprovedAsync(toolName, arguments))
       return ToolResult.Fail(...);

   // 3. Ejecutar con cronómetro
   var sw = Stopwatch.StartNew();
   var result = await tool.ExecuteAsync(input, cancellationToken);
   ```
   Señalar: cada ejecución se registra con duración. El ejecutor es un punto de control — todas las llamadas a herramientas pasan por él.

2. **`ToolRegistry.cs`** — Diccionario seguro para hilos con `StringComparer.OrdinalIgnoreCase`. Simple pero efectivo — los nombres de herramientas no distinguen mayúsculas.

3. **`ToolsServiceCollectionExtensions.cs`** — Cableado DI:
   - `AddToolFramework()` registra Registry (singleton), Executor (scoped), ApprovalPolicy (singleton)
   - `AddTool<T>()` registra herramientas individuales como singletons

### Demo en Vivo

**Mostrar el endpoint de lista de herramientas: `GET /api/tools`**

1. Abrir navegador o cliente HTTP
2. Navegar a `https://localhost:{port}/api/tools`
3. Mostrar la respuesta JSON — lista de metadatos de herramientas (nombre, descripción, esquema de parámetros)
4. Señalar: esto es lo que el modelo ve al decidir qué herramienta llamar

---

## Etapa 2: Herramientas Integradas + Seguridad (15 min)

### Conceptos a Explicar

Tres amenazas de seguridad del mundo real contra las que las herramientas del agente deben defenderse:

1. **Recorrido de Ruta (Path Traversal)** — Un atacante (o LLM confundido) intenta `../../etc/passwd` o `..\..\Windows\System32`. La herramienta de sistema de archivos debe confinar el acceso al espacio de trabajo.
2. **Inyección de Comandos** — El LLM genera `rm -rf /` o `format C:`. La herramienta de shell debe bloquear comandos peligrosos antes de ejecutarlos.
3. **SSRF (Server-Side Request Forgery)** — El LLM obtiene `http://127.0.0.1:8080/admin` o `http://169.254.169.254/metadata`. La herramienta web debe bloquear solicitudes a redes internas/privadas.

**Patrón de defensa**: Cada herramienta valida las entradas *antes* de la ejecución. Fallar rápido, fallar seguro.

### Recorrido por el Código

#### FileSystemTool (`OpenClawNet.Tools.FileSystem`, 142 LOC)

Características de seguridad clave a destacar:

1. **Array de rutas bloqueadas**:
   ```csharp
   private static readonly string[] BlockedPaths = [".env", ".git", "appsettings.Production"];
   ```

2. **Resolución de ruta con prevención de recorrido** — el método `ResolvePath`:
   ```csharp
   var fullPath = Path.GetFullPath(Path.Combine(_workspaceRoot, relativePath));
   if (!fullPath.StartsWith(_workspaceRoot, StringComparison.OrdinalIgnoreCase))
   {
       _logger.LogWarning("Path traversal attempt blocked: {Path}", relativePath);
       return null;
   }
   ```
   Explicar: `Path.GetFullPath` resuelve segmentos `..`. Luego verificamos que el resultado permanezca dentro del espacio de trabajo. Los `..` desaparecen para cuando verificamos — esto detecta todos los trucos de recorrido.

3. **Límite de tamaño de archivo**: Máximo 1MB para prevenir agotamiento de memoria.

4. **Tres operaciones**: leer, escribir, listar — cada una con protecciones apropiadas.

#### ShellTool (`OpenClawNet.Tools.Shell`, 148 LOC)

Características de seguridad clave:

1. **Lista de comandos bloqueados**:
   ```csharp
   private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
   {
       "rm", "del", "format", "fdisk", "mkfs", "dd", "shutdown", "reboot",
       "kill", "taskkill", "net", "reg", "regedit", "powershell", "cmd"
   };
   ```

2. **Verificación de seguridad** — extrae la primera palabra, elimina prefijo de ruta, verifica la lista de bloqueo:
   ```csharp
   private static bool IsSafeCommand(string command)
   {
       var firstWord = command.Split(' ', StringSplitOptions.RemoveEmptyEntries)
           .FirstOrDefault()?.ToLowerInvariant();
       firstWord = Path.GetFileNameWithoutExtension(firstWord);
       return !BlockedCommands.Contains(firstWord);
   }
   ```

3. **Tiempo límite**: 30 segundos máximo de ejecución con `CancellationTokenSource.CreateLinkedTokenSource` y eliminación del árbol de procesos.

4. **Límite de salida**: 10,000 caracteres para prevenir agotamiento de memoria.

5. **Multiplataforma**: Usa `cmd.exe /c` en Windows, `/bin/sh -c` en Linux/Mac.

6. **RequiresApproval = true**: Esta herramienta requiere aprobación explícita (a diferencia de sistema de archivos y web).

#### WebTool (`OpenClawNet.Tools.Web`, 121 LOC)

Características de seguridad clave:

1. **Prevención de SSRF** — el método `IsLocalUri`:
   ```csharp
   private static bool IsLocalUri(Uri uri)
   {
       var host = uri.Host.ToLowerInvariant();
       return host == "localhost" ||
              host == "127.0.0.1" ||
              host == "::1" ||
              host.StartsWith("192.168.") ||
              host.StartsWith("10.") ||
              host.StartsWith("172.16.");
   }
   ```
   Explicar: Esto bloquea solicitudes a redes internas. En producción, también resolverías DNS para detectar trucos CNAME (ej., `evil.com` → `127.0.0.1`).

2. **Validación de esquema**: Solo `http` y `https` — sin `file://`, `ftp://`, `gopher://`.

3. **Límite de respuesta**: 50,000 caracteres para prevenir agotamiento de memoria.

4. **Tiempo límite**: 15 segundos.

#### SchedulerTool (`OpenClawNet.Tools.Scheduler`, 173 LOC)

- Tres acciones: `create`, `list`, `cancel`
- Persistencia en base de datos vía EF Core (`IDbContextFactory<OpenClawDbContext>`)
- Soporta trabajos únicos (datetime ISO 8601) y trabajos recurrentes (expresiones cron)
- Lista hasta 20 trabajos con estado y próxima ejecución
- Cancelación de trabajos por GUID

### 🤖 Momento Copilot: Agregar un Patrón de Comando Bloqueado

**Cuándo:** ~minuto 22

**Contexto:** Acabamos de recorrer la lista de bloqueo del ShellTool. Ahora la extendemos.

**Qué hacer:** Abrir `ShellTool.cs`, colocar el cursor dentro del HashSet `BlockedCommands`, y preguntar a Copilot:

> Add `wget` and `curl` to the blocked commands list in the ShellTool. These tools could be used to exfiltrate data from the server. Also add a comment explaining why network tools are blocked.

**Resultado esperado:** Copilot agrega `"wget"` y `"curl"` al HashSet `BlockedCommands` y añade un comentario sobre prevención de exfiltración de datos.

**Por qué es interesante:** Cambio pequeño y enfocado que refuerza la mentalidad de seguridad. Muestra que extender la defensa es trivial con buena arquitectura.

---

## Etapa 3: Bucle del Agente + Integración (15 min)

### Conceptos a Explicar

- **El bucle de razonamiento del agente**: Este es el algoritmo central que hace que un agente sea un agente:
  1. Componer prompt (sistema + historial + mensaje del usuario + definiciones de herramientas)
  2. Enviar al modelo
  3. El modelo responde con texto O llamadas a herramientas
  4. Si hay llamadas a herramientas → ejecutar cada herramienta → agregar resultados a la conversación → volver al paso 2
  5. Si es texto → devolver respuesta final
  
  Este bucle se repite hasta que el modelo no tiene más llamadas a herramientas, o alcanzamos el límite de seguridad.

- **Iteraciones máximas como límite de seguridad**: `MaxToolIterations = 10`. Sin esto, un modelo confundido podría iterar indefinidamente. Después de 10 iteraciones, el agente devuelve un mensaje de "iteraciones máximas alcanzadas".

- **Cómo se inyectan las herramientas en el prompt del sistema**: El `DefaultPromptComposer` construye el prompt completo:
  1. Mensaje de sistema (prompt base + skills activos + resumen de sesión)
  2. Historial de conversación
  3. Mensaje actual del usuario
  
  Las definiciones de herramientas se pasan por separado a la API del modelo como objetos estructurados `ToolDefinition` — el modelo ve el nombre, descripción y esquema de parámetros para cada herramienta registrada.

### Recorrido por el Código

#### AgentOrchestrator (`OpenClawNet.Agent`)

El orquestador es la API pública — crea un `AgentContext` y delega a `IAgentRuntime`:

```csharp
public async Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken)
{
    var context = new AgentContext
    {
        SessionId = request.SessionId,
        UserMessage = request.UserMessage,
        ModelName = request.Model ?? "llama3.2",
        ProviderName = request.Provider
    };

    var executedContext = await _runtime.ExecuteAsync(context, cancellationToken);

    return new AgentResponse
    {
        Content = executedContext.FinalResponse ?? string.Empty,
        ToolResults = executedContext.ToolResults,
        ToolCallCount = executedContext.ExecutedToolCalls.Count,
        TotalTokens = executedContext.TotalTokens
    };
}
```

Señalar: El orquestador no sabe de herramientas, modelos ni prompts. Es un coordinador.

#### DefaultAgentRuntime — El Bucle Central

Recorrido detallado del bucle de llamadas a herramientas:

```csharp
while (iterations < MaxToolIterations)
{
    var response = await InvokeHostedAgentAsync(currentMessages, context.ModelName, toolDefs, agentSession, ct);
    totalTokens += response.Usage?.TotalTokens ?? 0;

    if (response.ToolCalls is { Count: > 0 })
    {
        // Agregar mensaje del asistente con llamadas a herramientas a la conversación
        currentMessages.Add(new ChatMessage { Role = Assistant, Content = response.Content ?? "", ToolCalls = response.ToolCalls });

        // Ejecutar cada herramienta
        foreach (var toolCall in response.ToolCalls)
        {
            var result = await _toolExecutor.ExecuteAsync(toolCall.Name, toolCall.Arguments, ct);
            allToolResults.Add(result);

            // Devolver resultado como mensaje Tool
            currentMessages.Add(new ChatMessage { Role = Tool, Content = result.Success ? result.Output : $"Error: {result.Error}", ToolCallId = toolCall.Id });
        }
        iterations++;
    }
    else
    {
        // Sin llamadas a herramientas — esta es la respuesta final
        context.FinalResponse = response.Content;
        context.IsComplete = true;
        return context;
    }
}
```

Puntos clave:
- El modelo decide cuándo llamar herramientas — nuestro código solo las ejecuta
- Los resultados de herramientas regresan a la conversación como mensajes `Role = Tool`
- El bucle continúa hasta que el modelo deja de solicitar herramientas
- El uso de tokens se acumula a través de todas las iteraciones

#### DefaultPromptComposer — Inyección de Herramientas

```csharp
public async Task<IReadOnlyList<ChatMessage>> ComposeAsync(PromptContext context, CancellationToken ct)
{
    var messages = new List<ChatMessage>();

    // 1. Prompt del sistema con skills
    var systemContent = DefaultSystemPrompt;
    var skills = await _skillLoader.GetActiveSkillsAsync(ct);
    if (skills.Count > 0)
        systemContent += $"\n\n# Active Skills\n{skillText}";

    // 2. Resumen de sesión
    if (!string.IsNullOrEmpty(context.SessionSummary))
        systemContent += $"\n\n# Previous Conversation Summary\n{context.SessionSummary}";

    messages.Add(new ChatMessage { Role = System, Content = systemContent });

    // 3. Historial + 4. Mensaje actual
    foreach (var msg in context.History) messages.Add(msg);
    messages.Add(new ChatMessage { Role = User, Content = context.UserMessage });

    return messages;
}
```

Señalar: Las definiciones de herramientas NO están en el prompt del sistema — se pasan como objetos estructurados vía la API del modelo. El prompt del sistema contiene skills y contexto; las herramientas son un canal separado.

#### Gateway DI — Cómo Se Registran Todas las Herramientas

En el `Program.cs` del Gateway, todas las herramientas se registran vía las extensiones de DI:

```csharp
builder.Services.AddToolFramework();     // Registry + Executor + ApprovalPolicy
builder.Services.AddTool<FileSystemTool>();
builder.Services.AddTool<ShellTool>();
builder.Services.AddTool<WebTool>();
builder.Services.AddTool<SchedulerTool>();
builder.Services.AddAgentRuntime();      // Orchestrator + Runtime + PromptComposer
```

Al inicio, cada singleton `ITool` se resuelve y registra en el `ToolRegistry`. El ejecutor puede entonces encontrar cualquier herramienta por nombre.

### Demo en Vivo

**Demo 1: Agente usa la herramienta FileSystem**
1. Abrir la UI de Blazor
2. Escribir: "List files in the current directory"
3. Observar cómo el agente emite una llamada a herramienta `file_system` → ejecutar → mostrar resultados
4. Señalar la llamada/resultado de herramienta en la respuesta

**Demo 2: Agente usa la herramienta Web**
1. Escribir: "What's on the front page of Hacker News?"
2. Observar cómo el agente emite una llamada a `web_fetch` → obtener → resumir
3. Señalar: el agente decidió usar la herramienta, obtuvo la página, luego la resumió

**Demo 3: Rechazo de comando bloqueado**
1. Escribir: "Run `rm -rf /` on the server"
2. Observar cómo el agente intenta usar la herramienta `shell` → ShellTool la bloquea → el agente reporta el rechazo
3. Señalar: la puerta de seguridad funcionó — el comando nunca se ejecutó

### 🤖 Momento Copilot: Agregar Seguimiento de Duración de Ejecución

**Cuándo:** ~minuto 40

**Contexto:** Hemos visto el bucle del agente ejecutar herramientas. Ahora agreguemos observabilidad.

**Qué hacer:** Abrir `ToolExecutor.cs` y preguntar a Copilot:

> In the ToolExecutor, add a method `GetExecutionStats()` that returns a dictionary of tool name → average execution duration. Track each tool's execution duration in a `ConcurrentDictionary<string, List<TimeSpan>>` field. Update it after each successful execution.

**Resultado esperado:** Copilot agrega un campo `_executionStats` y un método `GetExecutionStats()` que calcula promedios.

**Por qué es interesante:** Muestra cómo el patrón de punto de control (todas las herramientas a través del ejecutor) hace trivial agregar preocupaciones transversales como métricas.

---

## Cierre (8 min)

### Resumen de Seguridad

| Amenaza | Herramienta | Defensa |
|---------|-------------|---------|
| Recorrido de Ruta | FileSystemTool | `Path.GetFullPath` + verificación de límite del espacio de trabajo |
| Inyección de Comandos | ShellTool | HashSet de comandos bloqueados + tiempo límite |
| SSRF | WebTool | Lista de bloqueo de IP privadas + validación de esquema |

Tres amenazas. Tres defensas. Todas implementadas como validación de entrada antes de la ejecución.

### Lo Que Construimos

- ✅ Capa de abstracción de herramientas (ITool, IToolExecutor, IToolRegistry)
- ✅ Puerta de política de aprobación (IToolApprovalPolicy)
- ✅ FileSystemTool con prevención de recorrido de ruta
- ✅ ShellTool con lista de bloqueo de comandos y tiempo límite
- ✅ WebTool con protección contra SSRF
- ✅ SchedulerTool con CRUD de trabajos
- ✅ Bucle de razonamiento del agente (prompt → modelo → herramienta → bucle)
- ✅ Composición de prompts con inyección de herramientas

### Vista Previa: Sesión 3

> "El agente ahora tiene manos. Próxima sesión: darle personalidad y memoria."

La Sesión 3 cubre:
- **Skills** — Archivos de personalidad basados en YAML que personalizan el comportamiento del agente
- **Memoria** — Resumen de conversaciones para contexto a largo plazo
- **Carga de Skills** — Descubrimiento dinámico e inyección en el prompt del sistema
