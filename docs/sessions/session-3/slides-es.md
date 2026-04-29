---
marp: true
title: "OpenClawNet — Sesión 3: Habilidades, Almacenamiento y Memoria"
description: "Personalidad del agente a través de habilidades, almacenamiento de cierre seguro y planificación del sistema de memoria"
theme: openclaw
paginate: true
size: 16:9
footer: "OpenClawNet · Sesión 3 · Habilidades, Almacenamiento y Memoria"
---

<!-- _class: lead -->

# OpenClawNet
## Sesión 3 — Habilidades, Almacenamiento y Memoria

**Serie Microsoft Reactor · ~75 min · .NET Intermedio**

> *Mismo agente. Diferente personalidad. Disco más seguro. Memoria más larga.*

<br>

<div class="speakers">

**Bruno Capuano** — Principal Cloud Advocate, Microsoft
[github.com/elbruno](https://github.com/elbruno) · [@elbruno](https://twitter.com/elbruno)

**Pablo Nunes Lopes** — Cloud Advocate, Microsoft
[linkedin.com/in/pablonuneslopes](https://www.linkedin.com/in/pablonuneslopes/)

</div>

<!--
SPEAKER NOTES — diapositiva de título.
Bienvenidos de nuevo a la Sesión 3. En la Sesión 1 construimos la base. En la Sesión 2 le dimos manos al agente. Hoy le damos personalidad, un lugar seguro para vivir en disco, y una hoja de ruta para memoria a largo plazo. También lanzamos dos nuevas herramientas de desarrollo que querrás en tu barra de tareas. Gran sesión — vamos.
-->

---

## Dónde quedaron las Sesiones 1–2

- **Sesión 1** — Aplicación Aspire, `IAgentProvider`, streaming NDJSON, SQLite
- **Sesión 2** — `ITool` + MCP, una puerta de aprobación, un runtime
- **5** herramientas in-process, **5** servidores MCP incluidos, **5** demos
- 3 ataques bloqueados: path traversal, inyección de comandos, SSRF
- `aspire start` → chat con agente usando herramientas en 30 segundos

> Hoy agregamos: **personalidad, almacenamiento seguro y una hoja de ruta de memoria.**

<!--
SPEAKER NOTES — recapitulación.
Recapitulación rápida para que los que lleguen tarde puedan ponerse al día. Sesión 1 = una aplicación de chat funcional sobre Aspire/Blazor con cinco proveedores. Sesión 2 = el bucle del agente, dos superficies de herramientas, una puerta de aprobación y los primitivos de seguridad que todo framework de herramientas necesita. Ambas sesiones están grabadas y el código está en el repositorio. Si te las perdiste, las diapositivas y demos están en docs/sessions.
-->

---

## El alcance de hoy, en una diapositiva

1. 🛠️ **Herramientas extra** — Ollama Monitor + Aspire Monitor en tu barra de tareas
2. 🔧 **Actualizaciones de tool-calling** — Alineación con OpenAI, sanitizadores, refactorizaciones
3. 🎭 **Sistema de habilidades** — Personalidad en Markdown, hot-reload, por agente
4. 💾 **Refactorización de almacenamiento** — `C:\openclawnet\`, `ISafePathResolver`, H-1..H-8
5. 🧠 **Hoja de ruta de memoria** — ventanas de contexto, resumenes, embeddings
6. 🧪 **Demos de consola** — `aspire start`, `/api/skills`, skill drop-in manual

<!--
SPEAKER NOTES — alcance.
Seis capítulos. Los primeros dos son "lo que se lanzó desde la sesión 2 — calidad de vida y pulido de tool-calling". Los capítulos tres y cuatro son la carne — habilidades y almacenamiento. El capítulo cinco mira hacia adelante: hacia dónde va la memoria. El capítulo seis es práctico. Nos moveremos rápido en las partes de recapitulación y más lento en las decisiones de diseño.
-->

---

## El modelo mental de la sesión-3

<div class="cols">
<div>

### Comportamiento
- Las habilidades definen **qué hace el agente**
- Markdown + YAML, sin código
- Hot-reload, habilitación por agente

</div>
<div>

### Límites
- El almacenamiento define **dónde escribe el agente**
- Una raíz: `C:\openclawnet\`
- Contención de cierre seguro (fail-closed)

</div>
</div>

> Comportamiento + Límites = un agente personalizado en el que puedes confiar en disco.

<!--
SPEAKER NOTES — modelo mental.
Toda la sesión gira en torno a esto. Habilidades = comportamiento, almacenamiento = límites. Ambas tratan de dar más autonomía al agente de forma SEGURA. No puedes lanzar habilidades si el agente puede escribir en cualquier lugar del disco; no puedes confiar en el endurecimiento del almacenamiento si cualquiera puede soltar una habilidad maliciosa. Llegan juntas.
-->

---

<!-- _class: lead -->

# 🛠️  Parte 1 — Herramientas Extra

<!--
SPEAKER NOTES — divisor Parte 1.
Dos nuevas herramientas de desarrollo que lanzamos entre la Sesión 2 y la Sesión 3. Viven en la bandeja del sistema y hacen que el desarrollo diario de OpenClawNet sea mucho menos doloroso. Diez minutos en total.
-->

---

## Por qué necesitábamos herramientas extra

- Ollama muere silenciosamente → "¿por qué mi agente está lento?"
- El dashboard de Aspire es genial, pero **enterrado en una pestaña del navegador**
- Desarrollo de LLM local = 3 terminales + 2 dashboards abiertos todo el día
- Los demos se caen en vivo porque *algo* no estaba corriendo

> Queríamos **estado visible de un vistazo** — no "ve a abrir una pestaña".

<!--
SPEAKER NOTES — puntos de dolor.
Esto es lo que todo desarrollador de LLM local experimenta. Olvidas iniciar Ollama, el agente expira, depuras durante diez minutos antes de darte cuenta de que el servidor del modelo ni siquiera está arriba. O tu aplicación Aspire está corriendo pero cerraste la pestaña del dashboard. Construimos dos aplicaciones de bandeja que ponen las respuestas en tu bandeja del sistema.
-->

---

## Ollama Monitor — qué es

- 📦 **Herramienta dotnet de NuGet** — `dotnet tool install -g OpenClawNet.Tools.OllamaMonitor`
- 🟢 **Ícono de bandeja del sistema** con salud codificada por colores
- 📊 **Métricas en tiempo real** — modelo cargado, capas GPU, tokens/seg
- 🔔 **Notificaciones toast** cuando Ollama se cae
- 🪟 Windows primero (funciona en bandejas macOS/Linux también)

<!--
SPEAKER NOTES — Ollama Monitor.
Primera herramienta: Ollama Monitor. Distribuida como una herramienta dotnet global — un comando para instalar, vive en tu bandeja del sistema. Verde = Ollama está arriba y sirviendo. Amarillo = arriba pero lento. Rojo = caído. Haz clic en el ícono y obtienes detalles del modelo, estadísticas de GPU, solicitudes recientes. Las notificaciones toast son la característica asesina — te enteras antes que tu demo.
-->

---

## Ollama Monitor — características

- ⚡ **Sondeos de salud** cada 5s (configurable)
- 📈 **Últimos 60 segundos** de rendimiento en un sparkline
- 🧠 **Modelo cargado** + **tamaño** + **cuantización**
- 🎯 Contador de **solicitudes activas**
- 🛑 **Detener / iniciar rápido** desde el menú de la bandeja

<div class="cols">
<div>

### Códigos de color
- 🟢 Saludable, < 200ms latencia
- 🟡 Saludable, > 200ms latencia
- 🟠 Degradado (timeouts)
- 🔴 Inalcanzable

</div>
<div>

### Disparadores de toast
- Salida del proceso
- Falla del sondeo de salud (3x)
- Descarga del modelo
- Recuperación

</div>
</div>

<!--
SPEAKER NOTES — detalle de características.
Cada cinco segundos golpeamos /api/tags y /api/ps. El sparkline muestra tokens/seg agregados a través de solicitudes activas. Detener/iniciar rápido usa el CLI de Ollama bajo el capó. Los colores de estado son deliberados — amarillo no significa roto, significa "tu laptop está en batería y el modelo está paginado". Triple falla antes de mostrar toast para no bombardearte en una red inestable.
-->

---

## Ollama Monitor — instalación

```pwsh
# Instalar una vez
dotnet tool install -g OpenClawNet.Tools.OllamaMonitor

# Ejecutar bajo demanda
ollama-monitor

# O auto-iniciar con Windows
ollama-monitor --autostart
```

- La configuración vive en `%APPDATA%\OpenClawNet\OllamaMonitor\settings.json`
- Sobrescribe la URL de Ollama con `--endpoint http://host:11434`
- Los logs van a `%LOCALAPPDATA%\OpenClawNet\OllamaMonitor\logs\`

<!--
SPEAKER NOTES — instalación.
Tres líneas para instalar y ejecutar. Auto-start agrega una tarea programada de Windows al inicio de sesión — sobrevive reinicios, sin acceso directo de carpeta de inicio que gestionar. La configuración es JSON, puedes sincronizarla entre máquinas si quieres. El directorio de logs usa el patrón estándar de "datos de aplicación local" — el mismo lugar donde todas las otras aplicaciones Windows modernas ponen logs.
-->

---

## Ollama Monitor — demo

```text
🟢 Ollama (llama3.2:3b)            127.0.0.1:11434
   Loaded model:   llama3.2:3b-instruct-q4_K_M
   Size:           2.0 GB
   GPU layers:     33 / 33
   Throughput:     78 tok/s  ▁▂▅▇▇▆▅▃▂▁
   Active reqs:    2
   ───────────────────────────
   Open dashboard      [Ctrl+D]
   Restart Ollama      [Ctrl+R]
   Settings…
   Exit
```

> Clic derecho en el ícono de la bandeja → ve el latido del agente.

<!--
SPEAKER NOTES — demo.
Demo en vivo si Ollama está corriendo. Clic derecho en el ícono de la bandeja. Muestra el modelo cargado, el conteo de capas GPU — para un 3B cuantizado eso debería ser 33/33 significando completamente en GPU. El sparkline muestra los últimos 60 segundos. Las solicitudes activas suben cuando disparas una solicitud de chat. El hotkey del dashboard abre una ventana más detallada con línea de tiempo por solicitud.
-->

---

## Aspire Monitor — qué es

- 📦 **Herramienta dotnet de NuGet** — `dotnet tool install -g OpenClawNet.Tools.AspireMonitor`
- 🟢 Compañero de **bandeja del sistema de Windows** para `aspire start`
- 📁 **Vigilancia de carpeta de trabajo** — auto-detecta qué aplicación estás corriendo
- 📌 Mini ventana de **recursos fijados** — tus endpoints favoritos
- ⏯️ Controles de **Iniciar / Detener** desde la bandeja

<!--
SPEAKER NOTES — Aspire Monitor.
Segunda herramienta. Aspire Monitor resuelve un punto de dolor diferente: estás trabajando en múltiples aplicaciones Aspire, olvidas cuál está corriendo, la URL del dashboard cambia en cada reinicio. Esto se ancla a una carpeta, sabe qué AppHost vive allí, y te da iniciar/detener sin volver a la terminal.
-->

---

## Aspire Monitor — características

- 🔍 **Auto-descubrimiento** de proyectos `*.AppHost` en la carpeta vigilada
- 📊 **Estado por recurso** (corriendo, iniciando, fallido) con logs de un clic
- 📌 **Mini ventana fijada** — mantén 3-5 endpoints siempre visibles
- 🎯 **URL del dashboard** copiada al portapapeles al pasar el mouse
- 🔁 **Reiniciar** cualquier recurso individual sin reiniciar el host

<div class="cols">
<div>

### Qué vigila
- stdout/stderr del AppHost
- API del dashboard de Aspire
- Endpoints de salud de recursos
- Cambios de archivos en la carpeta de trabajo

</div>
<div>

### Qué expone
- ✅ Conteo de recursos corriendo
- ⏱️ Tiempo de inicio
- 🌐 URLs de endpoint (HTTP + HTTPS)
- ⚠️ Eventos de crash / reinicio

</div>
</div>

<!--
SPEAKER NOTES — características de Aspire.
La ventana fijada es la característica de la que la gente se enamora. Fijas "Gateway", "Ollama", y "Abrir Dashboard" — esos tres se quedan en una ventana flotante diminuta en la esquina de tu pantalla. Un clic y estás en cualquiera de ellos. Tras bambalinas estamos golpeando la API de recursos del dashboard de Aspire, así que obtenemos salud gratis.
-->

---

## Aspire Monitor — instalación

```pwsh
# Instalar una vez
dotnet tool install -g OpenClawNet.Tools.AspireMonitor

# Vigilar la carpeta actual
aspire-monitor

# Vigilar una carpeta específica
aspire-monitor --folder C:\src\openclawnet

# Auto-iniciar con el AppHost
aspire-monitor --auto
```

- La configuración vive en `%APPDATA%\OpenClawNet\AspireMonitor\settings.json`
- Los ítems fijados sobreviven reinicios
- Múltiples instancias = múltiples carpetas vigiladas

<!--
SPEAKER NOTES — instalación de Aspire.
Mismo patrón de instalación que Ollama Monitor — herramienta dotnet, global, no necesita admin. La bandera --auto es para CI / grabación de demo: inicia el AppHost en el momento en que se abre el monitor. Puedes correr múltiples copias apuntando a diferentes carpetas y aparecerán como íconos de bandeja separados.
-->

---

## Aspire Monitor — demo

```text
🟢 OpenClawNet.AppHost (3 of 3 running)
   ├── 🟢 gateway        https://localhost:7234
   ├── 🟢 ollama         http://localhost:11434
   └── 🟢 dashboard      https://localhost:17000
   ─────────────────────────────────────
   📌 Pinned
      Gateway · /chat
      Skills page
      Aspire dashboard
   ─────────────────────────────────────
   Start    Stop    Restart    Logs…
```

> Un ícono de bandeja, una carpeta vigilada, tres endpoints fijados.

<!--
SPEAKER NOTES — demo.
Clic derecho en el ícono de la bandeja. Tres recursos, todos verdes, con sus endpoints. Sección fijada en la parte inferior — las tres URLs que abro diez veces al día. Iniciar/Detener/Reiniciar se aplica a todo el AppHost. Logs abre la página de logs del dashboard directamente, no la raíz del dashboard, así que te saltas un clic.
-->

---

<!-- _class: lead -->

# 🔧  Parte 2 — Actualizaciones de Tool-Calling

<!--
SPEAKER NOTES — divisor Parte 2.
Ahora el trabajo bajo el capó. Entre la Sesión 2 y 3 hicimos un trozo de plomería en tool calling — alineación con el formato de OpenAI, una refactorización de FileSystemTool, y tres nuevos sanitizadores. Nada de esto es glamoroso; todo hace que el agente sea más confiable.
-->

---

## ¿Por qué tocar tool-calling en absoluto?

- La Sesión 2 lanzó un stack funcional — pero...
- Diferentes proveedores esperan **diferentes formatos de llamada de herramientas**
- `FileSystemTool` había crecido a un solo archivo de 600 líneas
- Los sanitizadores estaban duplicados en 3 herramientas
- "Funciona en Ollama" ≠ "funciona en Azure OpenAI"

> Objetivo: **un formato canónico**, sanitizadores reutilizables, herramientas más pequeñas.

<!--
SPEAKER NOTES — por qué.
Historia de origen honesta. Probamos principalmente en Ollama en la Sesión 2. Tan pronto como golpeamos Azure OpenAI y Foundry con el mismo agente, vimos diferencias sutiles en cómo se serializaban las llamadas de herramientas — orden de argumentos, comillas JSON, formas de error. Lo mismo con el FileSystemTool: se había convertido en un cajón de sastre. Esta parte de la sesión muestra qué cambiamos y por qué.
-->

---

## Alineación con formato OpenAI

```jsonc
// Llamada de herramienta canónica (coincide con OpenAI / Azure OpenAI / Foundry)
{
  "id": "call_abc123",
  "type": "function",
  "function": {
    "name": "file_system",
    "arguments": "{\"action\":\"read\",\"path\":\"README.md\"}"
  }
}
```

- Los argumentos son una **cadena JSON**, no un objeto (trampa heredada)
- `id` es opaco — los proveedores lo generan, nosotros solo lo devolvemos
- `type` es siempre `"function"` para herramientas v1

<!--
SPEAKER NOTES — formato.
La forma de llamada de herramienta de OpenAI es el estándar de facto. Arguments-as-string es la verruga histórica que todos soportan porque la API original se lanzó así. Microsoft.Extensions.AI nos da la abstracción correcta (AIFunction) pero los proveedores pueden desviarse. Canonizamos todo internamente para que nunca tengamos que hacer casos especiales de "¿es esto Ollama o Azure?" en el runtime.
-->

---

## Qué cambió desde la Sesión 2

| Componente | Antes | Después |
|-----------|--------|-------|
| Envoltura de resultado de herramienta | `{ok, value}` | `ToolResult.Ok/Fail` (record) |
| Análisis de argumentos | ad-hoc por herramienta | `JsonSchema` + `JsonElement` |
| Formas de error | cadenas | `ToolError(code, message)` |
| Sanitizadores | duplicados | registro `IInputSanitizer` |
| `FileSystemTool` | 1 archivo, 600 LOC | 5 archivos, ~120 LOC cada uno |

> Mismo contrato público. Internos más limpios. Mismos eventos NDJSON.

<!--
SPEAKER NOTES — tabla de diferencias.
Qué cambió realmente. La interfaz pública ITool es idéntica — tus herramientas personalizadas de la Sesión 2 todavía compilan y corren. Lo que limpiamos es el interior: un ToolResult basado en record para que los llamadores puedan hacer pattern-match, análisis de argumentos impulsado por esquema, códigos de error estructurados, sanitizadores detrás de una interfaz. La refactorización de FileSystemTool es la que más importa para la siguiente diapositiva.
-->

---

## Refactorización de FileSystemTool

```text
OpenClawNet.Tools/FileSystem/
├── FileSystemTool.cs         (orquestador — 110 LOC)
├── Operations/
│   ├── ReadOperation.cs
│   ├── WriteOperation.cs
│   ├── ListOperation.cs
│   └── DeleteOperation.cs
└── Validation/
    ├── PathValidator.cs       (delega a ISafePathResolver)
    └── ContentValidator.cs    (tamaño, codificación)
```

- Una operación por archivo — responsabilidad única
- Toda resolución de rutas **fluye a través de `ISafePathResolver`** (más en la Parte 4)
- Las pruebas ahora reflejan el diseño de archivos

<!--
SPEAKER NOTES — refactorización.
Esta es la estructura que repetiremos para las otras herramientas en las próximas sesiones. Un orquestador que elige la operación, un archivo por operación, validadores en su propia carpeta. La ganancia es testabilidad — en lugar de hacer mock de toda la herramienta, pruebas ReadOperation contra un IFileSystem falso. El PathValidator es un envoltorio delgado que delega al nuevo ISafePathResolver, que es el puente hacia la Parte 4.
-->

---

## Sanitizadores de herramientas — el nuevo contrato

```csharp
public interface IInputSanitizer<TInput>
{
    SanitizationResult<TInput> Sanitize(TInput input);
}

public sealed record SanitizationResult<T>(
    bool IsAccepted,
    T? Value,
    string? RejectionReason);
```

- Una interfaz, muchas implementaciones
- Sanitizadores de ruta, URL, comando de shell, esquema JSON
- Componible: encadena `PathSanitizer` → `SizeSanitizer` → `EncodingSanitizer`

<!--
SPEAKER NOTES — contrato de sanitizador.
Los sanitizadores ahora son de primera clase. Cada herramienta declara qué sanitizadores necesita y se ejecutan en orden antes de que el cuerpo de la operación vea la entrada. Este es el patrón que queremos para cualquier herramienta futura: nunca confíes en cadenas suministradas por LLM, siempre sanitiza, siempre ten una razón de rechazo estructurada. La razón de rechazo fluye de vuelta al modelo para que pueda corregirse en el siguiente turno.
-->

---

## Tres sanitizadores se lanzan con v1

<div class="cols">
<div>

### `PathSanitizer`
- Rechaza reparse points
- Fuerza contención (H-1..H-4)
- Aplica lista permitida de nombres (H-5)
- Delega a `ISafePathResolver`

</div>
<div>

### `UrlSanitizer`
- Solo HTTPS por defecto
- Bloquea rangos IP privados
- Bloquea hosts de metadatos cloud
- Limita redirecciones + tamaño de cuerpo

</div>
</div>

### `JsonArgumentSanitizer`
- Valida contra el `JsonSchema` de la herramienta
- Elimina propiedades desconocidas (opción fail-loud)
- Coerciona tipos solo cuando es seguro (`"42"` → `42` para propiedades int)

<!--
SPEAKER NOTES — tres sanitizadores.
PathSanitizer es el puente a la Parte 4 — es la capa de cara al usuario sobre ISafePathResolver. UrlSanitizer mantiene las defensas SSRF de la Sesión 2 pero como un componente reutilizable. JsonArgumentSanitizer es el que se paga más rápido: cada vez que el modelo inventa un nombre de propiedad o envía un número como cadena, el sanitizador o lo coerciona correctamente o lo rechaza con un mensaje claro. Los tokens ahorrados en reintentos pagan el esfuerzo de ingeniería en una semana.
-->

---

## De extremo a extremo: cómo se ve una llamada de herramienta ahora

```
LLM emite tool_call
        │
        ▼
┌──────────────────────┐
│ JsonArgumentSanitizer│ → validar contra JsonSchema
└────────┬─────────────┘
         │ válido
         ▼┌──────────────────────┐
│ PathSanitizer        │ → ISafePathResolver
└────────┬─────────────┘
         │ contained
         ▼
┌──────────────────────┐
│ IToolApprovalPolicy  │ → ¿humano en el ciclo?
└────────┬─────────────┘
         │ approved
         ▼
   ToolResult.Ok / .Fail   (auditoría en cada rama)
```

<!--
NOTAS DEL ORADOR — pipeline.
Este es el pipeline ahora. Cuatro puertas explícitas, cada una probable de forma independiente. JsonArgumentSanitizer primero porque es gratis y rechaza la mayor parte de la basura. Luego, cualquier argumento de tipo path pasa por PathSanitizer. Después la política de aprobación. Solo entonces se ejecuta el cuerpo de la operación. Cada puerta emite un registro de auditoría al aceptar y al rechazar — H-8 en la Parte 4.
-->

---

## Un diff concreto: operación `read`

```csharp
// Before (Session 2)
public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct)
{
    var path = input.Args["path"]!.ToString();
    var full = Path.GetFullPath(Path.Combine(_root, path));
    if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        return Fail("path escape");
    return Ok(File.ReadAllText(full));
}
```

```csharp
// After (Session 3)
public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct)
{
    var args = _argSanitizer.Sanitize(input);          // schema
    if (!args.IsAccepted) return Fail(args.RejectionReason!);

    var resolved = _paths.Resolve(args.Value!.Path, scope: _scope);
    if (!resolved.IsAllowed) return Fail(resolved.Reason!);

    return Ok(await File.ReadAllTextAsync(resolved.FullPath, ct));
}
```

<!--
NOTAS DEL ORADOR — diff.
Misma operación, dos mundos distintos. El "antes" está bien para una demo y es un disparo en el pie en producción. El "después" delega todo lo peligroso a un único resolver auditado. Sin Path.GetFullPath crudo sobre input del LLM. Sin verificación por prefijo de cadena que se rompe en C:\openclawnet vs C:\openclawnet-evil. Sin reescritura silenciosa. Este es el patrón que repetiremos para cada herramienta que reciba paths.
-->

---

## Los metadatos por herramienta no cambian

```csharp
public sealed record ToolMetadata(
    JsonDocument ParameterSchema,
    bool RequiresApproval,
    string Category,
    string[] Tags);
```

- `ParameterSchema` ahora alimenta a `JsonArgumentSanitizer`
- `RequiresApproval` sigue controlando el ejecutor
- Las herramientas que escribiste en la Sesión 2 siguen funcionando

> El contrato no se movió. La implementación se volvió más segura.

<!--
NOTAS DEL ORADOR — metadatos sin cambios.
Tranquilidad importante. Si escribiste una herramienta personalizada en la Sesión 2, NADA CAMBIA para ti a nivel de API. Mismo ITool, mismos metadatos, misma puerta de aprobación. SÍ obtienes los nuevos sanitizers gratis si optas por usarlos. La compatibilidad hacia atrás fue un requisito firme de esta refactorización.
-->

---

## Adiciones de eventos NDJSON

```jsonl
{"type":"ToolApprovalRequest","tool":"file_system","args":{...}}
{"type":"ToolCallStart","tool":"file_system","callId":"abc"}
{"type":"ToolSanitizationFailed","tool":"file_system","reason":"reparse-point"}
{"type":"ToolCallComplete","tool":"file_system","callId":"abc","durationMs":12}
{"type":"ContentDelta","text":"File contents..."}
```

- Nuevo: `ToolSanitizationFailed` — se muestra en la UI como advertencia inline
- Eventos existentes sin cambios — la UI sigue funcionando

<!--
NOTAS DEL ORADOR — NDJSON.
Un nuevo tipo de evento — ToolSanitizationFailed — emitido cuando un sanitizer rechaza una entrada antes de la aprobación. La UI lo muestra como una nota amarilla inline en la conversación para que el usuario vea "el modelo intentó leer C:\Windows\System32 y el sanitizer lo bloqueó". Esa transparencia es oro para depurar intentos de prompt-injection.
-->

---

## Lo que te aporta

- ✅ El mismo agente funciona en **Ollama, Azure OpenAI, Foundry, Copilot**
- ✅ Los modos de falla por path traversal ahora son **un bug, no cinco**
- ✅ Las razones del sanitizer regresan al modelo → menos bucles de reintento
- ✅ FileSystemTool es **5× más pequeño** por archivo → PRs más fáciles

> El trabajo "aburrido" de la sesión 3 que permite que existan las próximas 3 partes.

<!--
NOTAS DEL ORADOR — recompensa.
Esta es la diapositiva fundacional. Ni habilidades, ni endurecimiento de almacenamiento, ni memoria podían aterrizar limpiamente sobre las internals de herramientas de la Sesión 2 tal como estaban. Tuvimos que hacer esta refactorización primero. La portabilidad entre proveedores es el gran beneficio externo; el beneficio interno es que las próximas sesiones puedan AGREGAR sin reescribir.
-->

---

<!-- _class: lead -->

# 🎭  Parte 3 — Sistema de Habilidades

<!--
NOTAS DEL ORADOR — divisor de la Parte 3.
La parte más grande de la sesión — unas treinta diapositivas. Las habilidades son la característica que la mayoría de los usuarios notarán primero. Archivos Markdown que cambian el comportamiento del agente. Hot-reload. Habilitación por agente. Vamos.
-->

---

## ¿Qué es una habilidad?

> Un **archivo Markdown con frontmatter YAML** que moldea el comportamiento del agente — sin código, sin redeploy.

```markdown
---
name: dotnet-expert
description: .NET expertise — DI, async, Aspire, EF Core
tags: [dotnet, csharp, aspire]
---

You are a senior .NET architect. When answering:

- Prefer modern C# patterns (records, primary constructors)
- Cite Microsoft Learn for any non-trivial claim
- Show code in full, never abridged with "..."
```

<!--
NOTAS DEL ORADOR — qué es una habilidad.
Léelo en pantalla. Tres líneas de YAML, un párrafo de Markdown, y el agente ahora se comporta como un arquitecto sénior de .NET. Sin C#. Sin deploy. Sin reinicio. El punto que estamos haciendo toda la sesión: las habilidades son CONTENIDO, no CÓDIGO. Cualquiera del equipo puede escribir una — tu PM, tu QA, tu líder de seguridad.
-->

---

## ¿Por qué habilidades (vs. herramientas)?

|              | **Herramientas**                | **Habilidades**                     |
|--------------|---------------------------------|-------------------------------------|
| Formato      | código C# (`ITool`)             | Markdown + YAML                     |
| Autoría      | ingenieros                      | **cualquiera** (PM, QA, seguridad…) |
| Efecto       | nuevas **capacidades**          | nuevo **comportamiento**            |
| Ciclo de vida| compilar + desplegar            | soltar un archivo, hot-reload       |
| Superficie de riesgo | ejecución de código     | prompt injection                    |

> **Herramientas = brazos.** **Habilidades = personalidad.** Problemas distintos.

<!--
NOTAS DEL ORADOR — vs herramientas.
Mucha gente ve las habilidades y pregunta "¿no es solo un system prompt?" Sí — pero con estructura, ciclo de vida y auditoría. La columna crucial es "autoría". Las herramientas requieren un ingeniero; las habilidades no. Ese único hecho cambia quién en la empresa puede moldear el comportamiento del agente. La superficie de riesgo también es genuinamente distinta — las habilidades no pueden ejecutar código en v1 (S-8) pero pueden hacer prompt-injection al modelo para que haga cosas malas, por eso importan las puertas de aprobación y la habilitación por agente.
-->

---

## El bug estrella que arreglamos

OpenClawNet tenía **dos cargadores de habilidades paralelos** que no compartían estado:

| Sistema | Lee desde | Consumido por |
|--------|------------|-------------|
| `FileSkillLoader` propio | `skills/built-in`, `skills/samples` | `/api/skills/*` |
| `AgentSkillsProvider` de MAF | config `Agent:SkillsPath` | el agente real |

**Resultado:** click en "Disable" en la UI → `200 OK` → el agente **sigue usando la habilidad**.

<!--
NOTAS DEL ORADOR — bug estrella.
Doloroso de admitir pero vale la pena mostrarlo. Teníamos dos subsistemas de habilidades que ambos funcionaban, ninguno sabía del otro. Click en disable, el agente sigue usando la habilidad. Click en install, el archivo cae en una carpeta que el agente no escanea. El hot reload recarga el loader que el agente no usa. Esto fue el catalizador de toda la propuesta de habilidades.
-->

---

## La solución: un cargador, una fuente de verdad

```
┌──────────────────────────────────────┐
│  ISkillsRegistry  (singleton)        │
│  3-layer discovery + watcher         │
└────────────┬─────────────────────────┘
             │ BuildFor(agentName)
             ▼
┌──────────────────────────────────────┐
│  OpenClawNetSkillsProvider (scoped)  │
│  AIContextProvider · per-request     │
└────────────┬─────────────────────────┘
             │
             ▼
┌──────────────────────────────────────┐
│  MAF AgentSkillsProvider             │
│  (agentskills.io spec compliant)     │
└──────────────────────────────────────┘
```

> Eliminar `FileSkillLoader`. Eliminar `SkillParser`. **MAF es la fuente de verdad.**

<!--
NOTAS DEL ORADOR — solución.
Eliminamos el cargador paralelo. MAF — Microsoft Agent Framework — ya implementa la especificación completa de agentskills.io, incluyendo divulgación progresiva, parsing de YAML y herramientas de recursos. Nuestro trabajo se vuelve un decorador delgado scoped que agrega tres cosas: atribución de capa, filtrado de habilitación por agente y logging estructurado. La UI llama a nuestro registry; el registry alimenta a MAF; MAF alimenta al agente. Un solo pipeline.
-->

---

## Almacenamiento en 3 capas

```
C:\openclawnet\
└── skills\
    ├── system\                          # Ships with app, read-only
    │   ├── memory\SKILL.md
    │   └── doc-processor\SKILL.md
    ├── installed\                       # From imports, shared
    │   ├── awesome-copilot\dotnet-expert\SKILL.md
    │   └── .install-manifest.json
    ├── agents\{agent-name}\             # Per-agent overrides
    │   ├── enabled.json                 # which skills are visible
    │   └── skills\
    │       └── {skill-name}\SKILL.md
    └── .quarantine\                     # imported, not yet approved
        └── {import-id}\…
```

**Precedencia:** `agents/{name}/` > `installed/` > `system/`.

<!--
NOTAS DEL ORADOR — 3 capas.
Tres capas, una regla de precedencia. System se entrega con la aplicación y es de solo lectura. Installed es compartido por todos los agentes y vive detrás del pipeline de importación. Agents-slash-name son overrides por agente — tanto para habilitación (enabled.json) como para habilidades genuinamente personalizadas. Quarantine es donde caen las importaciones antes de la aprobación. La capa más alta gana en colisiones de nombre, así que un agente puede ocultar una habilidad del sistema con la suya propia.
-->

---

## ¿Por qué almacenamiento compartido y no por agente?

> "La amenaza real es **contenido-en-prompt**, no contenido-en-disco."

- La *habilitación* por agente controla la exposición
- El *almacenamiento* por agente significaría N copias de cada habilidad
- N copias → fatiga de actualización → aprobaciones de trámite → CVE
- El almacenamiento por agente es **teatro**; la habilitación por agente es **real**

<!--
NOTAS DEL ORADOR — almacenamiento compartido.
La decisión de Drummond del review de seguridad. La amenaza es lo que entra al system prompt en runtime, no lo que está en disco. Si copiamos cada habilidad instalada en la carpeta de cada agente, tendríamos N copias para actualizar en cada CVE. Los usuarios saltarían la puerta de aprobación solo para mantener el ritmo. Almacenamiento compartido con habilitación por agente es a la vez más seguro y más sensato.
-->

---

## Frontmatter de agentskills.io

```yaml
---
name: dotnet-expert
description: .NET expertise — DI, async, Aspire, EF Core
license: MIT
metadata:
  openclawnet:
    tags: [dotnet, csharp, aspire]
    category: programming
    examples:
      - "How should I structure DI in a Blazor app?"
      - "Why is my async method blocking?"
---
```

- Núcleo **conforme a la spec**: `name`, `description`, `license`
- Los extras de OpenClawNet viven bajo `metadata.openclawnet.*`
- MAF ignora con elegancia los campos desconocidos

<!--
NOTAS DEL ORADOR — frontmatter.
agentskills.io es la spec abierta con la que nos alineamos. Define los campos núcleo — name, description, license — y reserva un namespace metadata para extensiones del proveedor. Nuestros extras (tags, category, examples) se mueven a metadata.openclawnet.* para ser compatibles a futuro con cualquier otro host que hable la spec. MAF parsea YAML correctamente, incluyendo strings multi-línea entrecomillados — nuestro parser hecho a mano se atragantaba con esos.
-->

---

## Qué se descarta del frontmatter antiguo

| Campo antiguo | Estado | Por qué |
|-----------|--------|-----|
| `enabled: true` | ❌ eliminado | reemplazado por `enabled.json` por agente |
| `category: …` | ✅ movido | ahora `metadata.openclawnet.category` |
| `tags: […]` | ✅ movido | ahora `metadata.openclawnet.tags` |
| `examples: […]` | ✅ movido | ahora `metadata.openclawnet.examples` |

> Conforme a la spec por encima. Sabor OpenClawNet por debajo.

<!--
NOTAS DEL ORADOR — campos antiguos.
El gran cambio es eliminar enabled-true del frontmatter mismo. Por qué: la habilitación es por agente, no por habilidad. Ponerlo en el archivo lo hace ver global. Los valores por defecto para "qué built-ins están on por defecto para nuevos agentes" se mueven a un SystemSkillsDefaults.json en el content root del gateway. Separación más limpia entre "qué es la habilidad" y "quién la tiene activada".
-->

---

## Hot-reload con `FileSystemWatcher`

```csharp
public sealed class SkillsLayerWatcher : IDisposable
{
    private readonly FileSystemWatcher _fsw;
    private readonly Channel<Unit> _coalesce;

    public SkillsLayerWatcher(string root, Action onChange)
    {
        _fsw = new FileSystemWatcher(root, "*.md")
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _fsw.Changed += (_, _) => _coalesce.Writer.TryWrite(default);
        // … 500ms debounce loop calls onChange()
    }
}
```

- Un watcher **por capa** (system, installed, agents/{name})
- Debounce de 500 ms — los guardados son a ráfagas (baile de archivos temporales del editor)
- Al cambiar → el registry reconstruye el snapshot

<!--
NOTAS DEL ORADOR — watcher.
FileSystemWatcher es notoriamente ruidoso — VS Code guarda un archivo escribiendo un temp, eliminando el original y renombrando. Tres eventos por un guardado. Coalescemos con un debounce de 500ms: cualquier número de eventos dentro de la ventana colapsa en una sola reconstrucción. Watchers por capa porque las capas pueden vivir en discos o volúmenes distintos y queremos dominios de falla independientes.
-->

---

## Snapshot por solicitud

```csharp
public sealed record SkillsSnapshot(
    ImmutableArray<ResolvedSkill> Skills,
    long Version);
```

- El registry mantiene el snapshot **actual**
- Cada solicitud obtiene una vista **estable**
- Ediciones de archivo a mitad de conversación → el siguiente turno toma el cambio
- Sin lecturas rotas

<!--
NOTAS DEL ORADOR — snapshot.
Decisión de diseño importante. Cuando el watcher dispara no metemos la mano en las conversaciones en curso para parchearlas. Reconstruimos un nuevo snapshot inmutable. Los turnos activos terminan en el snapshot viejo; el siguiente turno toma el nuevo. Esta es la respuesta a la pregunta abierta Q2 de Bruno en la propuesta — "¿auto-recarga a mitad de conversación? no — snapshot por turno." Evita lecturas rotas y mantiene una sola conversación determinística.
-->

---

## Habilitación por agente: `enabled.json`

```json
{
  "version": 1,
  "skills": {
    "memory": "enabled",
    "doc-processor": "enabled",
    "awesome-copilot/dotnet-expert": "enabled",
    "awesome-copilot/security-auditor": "disabled"
  },
  "default": "use-default"
}
```

- Un archivo por agente en `agents/{name}/enabled.json`
- Tres estados: `enabled`, `disabled`, `use-default`
- Las habilidades nuevas por defecto están **disabled** por seguridad fail-closed
- Persistido en SQLite; `enabled.json` es la proyección en disco

<!--
NOTAS DEL ORADOR — enabled.json.
Lógica de tres valores. enabled = encendido explícito. disabled = apagado explícito. use-default = "pregúntale al registry cuál es el default" — útil para habilidades nuevas de las que el agente aún no ha sido informado. El default para habilidades externas recién importadas es disabled. El default para built-ins es enabled. El estado autoritativo vive en SQLite para poder consultar "muéstrame todo agente que tenga la habilidad X habilitada" sin leer cada archivo JSON. El JSON en disco es la proyección amigable para humanos.
-->

---

## Default = disabled, cierre seguro (fail-closed)

> Instalar ≠ activo.

- Un usuario importa `security-auditor` desde awesome-copilot
- El archivo cae en `installed/awesome-copilot/security-auditor/`
- **Ningún agente lo usa todavía**
- El usuario va a la página de Habilidades → lo activa por agente
- La habilitación por agente cambia a `"enabled"` → efectivo en el siguiente turno

<!--
NOTAS DEL ORADOR — fail closed.
Esto es S-7 de la propuesta — cierre seguro. Es fricción deliberada. No queremos que un usuario acepte un diálogo de importación y que el contenido de la habilidad se cuele en el system prompt de cada agente. Dos gestos: importar y habilitar. El paso de habilitar te hace elegir qué agentes lo reciben. Si importaste por accidente, ningún agente se ve afectado.
-->

---

## UI Skills.razor — navegación de nivel superior

<div class="cols">
<div>

### Explorar
- Filtrar por built-in / installed / enabled
- Columnas de fuente, versión, categoría
- "Habilitada en: GptAgent, ResearchBot"
- Click en una fila → expansión de detalle inline

</div>
<div>

### Actuar
- Toggle de asignación por agente (modal)
- Pestaña "Install from URL"
- Disable / enable / remove
- Estadísticas de uso (últimos 7 días)

</div>
</div>

> **Una página** para todo lo relacionado con habilidades — no enterrada en Settings.

<!--
NOTAS DEL ORADOR — página de Habilidades.
Promovimos las habilidades fuera del submenú de Settings a un ítem de navegación de nivel superior. Dos mitades: Explorar (izquierda) y Actuar (derecha). La columna "Habilitada en" es la clave — de un vistazo ves qué agentes tienen qué habilidades. El modal de asignación por agente es donde cambias la habilitación; lo veremos a continuación.
-->

---

## Modal de asignación por agente

```
┌─────────────────────────────────────────────┐
│ dotnet-expert                          [✕]  │
├─────────────────────────────────────────────┤
│  Default for new agents:   [✓ Enabled]      │
│                                             │
│  Per-agent overrides                        │
│   GptAgent           [enabled  ▼]           │
│   ResearchBot        [disabled ▼]           │
│   SupportBot         [use default ▼]        │
│                                             │
│  Effective on the next chat turn.           │
│                                  [ Save ]   │
└─────────────────────────────────────────────┘
```

> Toggle por defecto arriba. Overrides explícitos por agente abajo.

<!--
NOTAS DEL ORADOR — modal de asignación.
Diálogo único. Toggle por defecto en la parte superior — qué heredan los nuevos agentes. Dropdowns por agente abajo — encendido explícito, apagado explícito o "usar default". Save persiste a SQLite, la proyección de archivo se actualiza en la próxima reconstrucción de snapshot. El texto "efectivo en el siguiente turno de chat" es importante — establece expectativas sobre la semántica de hot-reload.
-->

---

## Asistente de importación de 4 pasos

1. **Entrada** — pega URL o explora colecciones de la allowlist
2. **Vista previa** — fuente, SHA, lista de archivos, SKILL.md renderizado, diff vs existente
3. **Progreso** — descarga, verifica, extrae (con log detallado)
4. **Resultado** — toast: "View / Edit Assignment" o error + retry

> Banner amarillo grande: *"Este contenido será inyectado en el system prompt del agente."*

<!--
NOTAS DEL ORADOR — asistente de importación.
Cuatro pasos, deliberadamente. Entrada recolecta la URL. Vista previa es el paso crítico para seguridad: lista completa de archivos, tamaños, SHA-256 por archivo, Markdown renderizado para que veas lo que el modelo verá, y un diff contra cualquier versión instalada previamente. La advertencia "este contenido entra al system prompt" no es descartable — esa única oración es la diferencia entre un usuario informado y un click-through.
-->

---

## v1 import: solo awesome-copilot

- Una fuente en allowlist: `github.com/github/awesome-copilot`
- Anclado a un **commit SHA** (no a `main`, no a un tag)
- Manifiesto SHA-256 de cada archivo del bundle
- Agregar más fuentes = editar `appsettings` (sin UI)

> "Solo pega una URL" es una vulnerabilidad, no una característica.

<!--
NOTAS DEL ORADOR — allowlist.
S-12 de la propuesta. v1 se entrega con una fuente confiable. El razonamiento: permitir URLs arbitrarias de GitHub convierte la importación en un primitivo de prompt injection remoto. Anclar a un commit SHA derrota ataques de time-of-check/time-of-use donde el upstream cambia entre vista previa y confirmación. Para agregar una nueva fuente, un admin edita appsettings — fricción deliberadamente alta.
-->

---

## Ruta de autoría manual

No tienes que usar el pipeline de importación. Puedes crear habilidades localmente:

```pwsh
# 1. Pick a folder
mkdir C:\openclawnet\skills\agents\GptAgent\skills\my-tone-of-voice

# 2. Drop a SKILL.md
@"
---
name: my-tone-of-voice
description: Concise, friendly, no buzzwords
---

When responding: short sentences. No "leverage" or "synergize".
"@ | Out-File ...\SKILL.md

# 3. Watcher picks it up; skill is enabled for that agent
```

<!--
SPEAKER NOTES — autoría manual.
Válvula de escape crítica. La tubería de importación es la ruta más segura para compartir habilidades. Pero día a día, tú y tu equipo escribirán habilidades manualmente en su editor, las guardarán bajo agents/{name}/skills/, y el watcher las recogerá automáticamente. Sin reinicio, sin llamadas API. Así es también cómo iteras mientras escribes una habilidad — guardar, probar, guardar, probar.
-->

---

## Anatomía de SKILL.md en detalle

```markdown
---
name: security-auditor
description: Security review focused on .NET + OWASP
license: MIT
metadata:
  openclawnet:
    tags: [security, audit, owasp, dotnet]
    category: security
    examples:
      - "Audit this controller for OWASP Top 10"
      - "Check for SQL injection in this query"
---

You are a security auditor. When reviewing code:

- Identify OWASP Top 10 issues by name
- Flag SQL injection, XSS, path traversal
- Suggest mitigations with code examples
- Sort findings by severity: Critical / High / Medium / Low
```

<!--
SPEAKER NOTES — anatomía.
Ejemplo concreto que podemos copiar y pegar. Frontmatter en la parte superior — delimitado por triple guion. Cuerpo debajo — Markdown puro. El cuerpo se convierte en parte del prompt del sistema textualmente. El array de ejemplos es solo metadatos — no se inyecta, pero la UI lo usa para mostrar sugerencias "intenta preguntar…". El archivo total suele tener menos de 2KB; el máximo que permitimos es 256 KB (S-11) para que una sola habilidad no explote tu presupuesto de tokens por accidente.
-->

---

## Recursos limitados (S-11)

- Por habilidad: `SKILL.md` ≤ **256 KB**
- Por prompt del sistema del agente: tokens inyectados por habilidades ≤ **8 KB** (por defecto)
- Los más antiguos por orden de carga se descartan si se excede el presupuesto
- Auditoría WARN cuando ocurre el descarte

> Una habilidad que infla tu prompt **se degrada elegantemente**, no falla.

<!--
SPEAKER NOTES — limitados.
Los presupuestos de tokens son dinero real. Limitamos el tamaño de cada archivo de habilidad y la contribución total al prompt del sistema. Si tres habilidades grandes están habilitadas y exceden el presupuesto, la más antigua por orden de carga se descarta — pero hacemos auditoría WARN para que puedas enterarte. El presupuesto por defecto es 8KB que cabe cómodamente en cualquier ventana de contexto moderna. Configurable por agente.
-->

---

## Invariantes de endurecimiento S-1..S-12

| # | Qué |
|---|------|
| **S-1** | Anclaje de procedencia (URL + SHA de commit + SHA-256 del paquete) |
| **S-2** | Lista permitida de tipos de archivo (sin ejecutables, nunca) |
| **S-3** | Contención de rutas de almacenamiento (reusar H-1..H-6) |
| **S-4** | Reserva de nombres integrados (`shell-exec` etc. no puede ser suplantado) |
| **S-5** | Puerta de aprobación en instalación **y** cada actualización |
| **S-6** | Sin actualización automática desde fuentes externas |

<!--
SPEAKER NOTES — S-1..S-6.
Primera mitad de la lista de endurecimiento. S-1 es el manifiesto. S-2 es la lista permitida de tipos de archivo — sin .py, .ps1, .dll, sin bit ejecutable, nada que empiece con MZ o shebang. S-3 delega la resolución de rutas a la capa de almacenamiento. S-4 evita que una habilidad maliciosa reclame ser el shell-exec integrado. S-5 es previsualización/confirmación de dos pasos. S-6 significa que los cambios upstream nunca se aplican silenciosamente — ejecutas nuevamente la puerta.
-->

---

## Invariantes de endurecimiento S-1..S-12 (cont.)

| # | Qué |
|---|------|
| **S-7** | Habilitación por agente, almacenamiento **compartido** |
| **S-8** | Sin contenido ejecutable de habilidad en v1 |
| **S-9** | Pista de auditoría en instalar/actualizar/cargar/invocar |
| **S-10** | Revocación efectiva dentro de **un turno de chat** |
| **S-11** | Uso limitado de recursos (256 KB / presupuesto de 8 KB tokens) |
| **S-12** | Lista permitida de fuentes, denegar por defecto |

> **Ninguno negociable para v1.** Cada PR revisado contra esta lista.

<!--
SPEAKER NOTES — S-7..S-12.
Segunda mitad. S-7 ya lo cubrimos. S-8 es "sin contenido ejecutable todavía" — esa es una propuesta futura con su propia caja de arena. S-9 pista de auditoría cubre cada evento del ciclo de vida para que puedas responder "quién instaló qué cuándo" forenseménte. S-10 — deshabilitar toma efecto en el siguiente turno de chat, no en el siguiente reinicio del proceso. S-11 presupuestos de tokens. S-12 lista permitida de fuentes. Doce invariantes, cada PR es revisado contra ellas, sin excepciones para v1.
-->

---

## Registro estructurado — 14 eventos

```text
SkillLoaderStarted        SkillDiscovered          SkillLoaded
SkillLoadFailed           SkillInvoked             SkillFunctionReturned
SkillFunctionThrew        ImportRequested          ImportApproved
ImportRejected            ImportCompleted          ImportFailed
SkillEnabled              SkillDisabled
```

- Todos vía clases generadas por código con `LoggerMessage`
- Correlación de 8 campos: `RunId`, `SkillInvocationId`, `AgentId`, `UserId`, `SkillId`, `FunctionName`, `RequestId`, `Timestamp`
- OTel `ActivitySource: OpenClawNet.Skills` → panel de Aspire

<!--
SPEAKER NOTES — registro.
Catorce eventos estructurados que cubren el ciclo de vida completo. Los generadores de código LoggerMessage nos dan registro de asignación cero en las rutas críticas. Ocho campos de correlación significa que una sola consulta SQL puede responder "muéstrame todo lo que pasó durante el último turno de chat de este usuario incluyendo qué habilidades se cargaron, qué funciones se invocaron, qué intentos de importación hubo". Los spans OTel van directamente al panel de Aspire que viste en la Parte 1.
-->

---

## Qué NO registramos

- ❌ Valores de parámetros (PII, claves API, credenciales)
- ❌ Valores de retorno (lo mismo)
- ❌ Contenido del cuerpo de SKILL.md (controlado por atacante — inyección de log)
- ❌ Contenido del chat / respuestas del agente / tokens OAuth

> Registrar **esquema** + **tamaño** + **SHA-256** de los primeros 1 KB. Eso es suficiente.

<!--
SPEAKER NOTES — qué no registramos.
Regla crítica de sensibilidad. Los valores de parámetros pueden contener cualquier cosa — claves API, contraseñas, PII. Valores de retorno también. Los cuerpos de SKILL.md son controlados por el atacante, así que registrarlos amplifica ataques de inyección de log. Registramos ESQUEMA — tipos y formas — más tamaño y un hash parcial. Eso es suficiente para forense, no suficiente para filtrar. Recomendación de Dylan en la propuesta.
-->

---

## Pruebas E2E — qué pasa

- ✅ `GET /api/skills` devuelve las mismas habilidades que usa el agente
- ✅ `POST /api/skills/{name}/disable` toma efecto en el siguiente turno
- ✅ Recarga en caliente: soltar un archivo → siguiente turno lo ve
- ✅ Habilitación por agente: activar para AgentA → AgentB no afectado
- ✅ Vista previa de importación → confirmar → ronda completa de instalación
- ✅ Instalación de nombre reservado rechazada con error claro

<!--
SPEAKER NOTES — E2E.
Seis pruebas end-to-end son los criterios de aceptación para K-1 (la ola fundacional). Cada una es una solicitud HTTP real contra una gateway en ejecución. La prueba de recarga en caliente suelta un archivo y espera un turno. La prueba por agente afirma aislamiento. La prueba de importación ejercita la tubería completa de previsualización-confirmación-instalación incluyendo verificación SHA y limpieza de cuarentena. Ejecutamos estas en CI en cada PR.
-->

---

## Olas de implementación

| Ola | Alcance |
|------|-------|
| **K-1** | Eliminar cargador paralelo, único `ISkillsRegistry`, 3 capas + watcher, `enabled.json` |
| **K-2** | 14 eventos de log + correlación de 8 campos + filas de habilidad en panel de Actividad |
| **K-3** | Tubería de importación (previsualización → confirmar), fetcher awesome-copilot |
| **K-4** | Pulido de UI — modal de asignación, asistente, estadísticas de uso |

> Se encadena después de **W-1..W-4** (almacenamiento). Las habilidades no pueden enviarse antes de que el almacenamiento esté endurecido.

<!--
SPEAKER NOTES — olas.
Cuatro olas. K-1 es la fundación — eliminar el cargador paralelo, obtener un registro. K-2 es observabilidad. K-3 es la tubería de importación. K-4 es pulido de UX. La flecha de dependencia en la parte inferior es el remate de toda esta sesión: las habilidades dependen del almacenamiento. No podemos escribir contenido proporcionado por el usuario al disco de forma segura hasta que la capa de almacenamiento imponga contención. Que es exactamente la Parte 4.
-->

---

<!-- _class: lead -->

# 💾  Parte 4 — Refactorización de Almacenamiento

<!--
SPEAKER NOTES — divisor de Parte 4.
Veinte diapositivas sobre almacenamiento. Esta es la parte portante de la sesión. Sin H-1..H-8, las habilidades no pueden enviarse de forma segura; sin una raíz predeterminada sensata, los usuarios no pueden encontrar sus archivos. Ambos problemas, un diseño.
-->

---

## La pregunta de Bruno

> "¿Dónde están las configuraciones para la **ubicación de almacenamiento** de OpenClawNet — el lugar predeterminado para almacenar archivos de la aplicación?"

| Escenario | Ruta esperada |
|----------|---------------|
| El agente genera un archivo markdown | `C:\openclawnet\agents\{name}\out.md` |
| La herramienta descarga un modelo local | `C:\openclawnet\models\` |
| El usuario apunta al agente a una carpeta | `C:\openclawnet\workspaces\samples\` |
| Predeterminado general | `C:\openclawnet\` |

<!--
SPEAKER NOTES — pregunta de Bruno.
Cita directa del issue que empezó esto. Bruno quiere UNA raíz, descubrible, predecible. El predeterminado de hoy está enterrado en bin/Debug/net10.0/ — inútil para usuarios finales. Necesitamos arreglar el predeterminado Y endurecer cada ruta de código que toma una ruta del LLM.
-->

---

## Qué estaba mal

- 🟥 Los prompts del agente decían *"tu raíz de workspace es `bin/Debug/net10.0/`"*
- 🟥 `FileSystemTool` por defecto estaba en la **raíz de la solución**
- 🟥 Sin subcarpeta `workspaces/` para áreas de scratch nombradas por el usuario
- 🟥 Las descargas de modelos aterrizaban en `~/.cache/huggingface`
- 🟥 La raíz predeterminada era `C:\openclawnet\storage\` — nivel extra que nadie pidió
- 🟥 `ResolvePath` permitía cualquier ruta **absoluta**

> La propuesta **redirigía** escrituras; esta revisión las **restringe**.

<!--
SPEAKER NOTES — qué estaba mal.
Seis problemas concretos. Cinco sobre descubribilidad. Uno sobre seguridad. La última viñeta es la peligrosa: incluso después de que arreglamos el predeterminado a C:\openclawnet, el agente TODAVÍA podía escribir en cualquier parte del disco emitiendo una ruta absoluta. Redirección no es restricción. La revisión de endurecimiento hizo eso explícito.
-->

---

## Los nuevos predeterminados

```
C:\openclawnet\
├── agents\{agent-name}\          # salidas por agente
├── models\                       # modelos locales (Ollama, HF, ONNX)
├── workspaces\{name}\            # scratch nombrado por usuario
├── uploads\                      # subidas de usuario
├── exports\                      # artefactos generados
├── skills\                       # (Parte 3)
└── dataprotection-keys\          # llavero de ASP.NET
```

- Configurable vía `Storage:RootPath` en `appsettings`
- O variable de entorno `OPENCLAWNET_STORAGE_ROOT` (único nombre canónico)
- Registra ruta resuelta + fuente en INFO al inicio

<!--
SPEAKER NOTES — predeterminados.
Así es como se ve tu C:\openclawnet después de la Sesión 3. Siete subcarpetas bien conocidas, cada una con un propósito claro. Los agentes obtienen su propia carpeta por nombre de agente — base para futura aislación por agente. Los modelos son compartidos. Workspaces son áreas de scratch nombradas por usuario. Uploads y exports separan archivos de usuario entrantes de salientes. Skills viste. Dataprotection-keys lo cubriremos en la diapositiva de ACL.
-->

---

## Configuración: tres fuentes, un ganador

```text
Prioridad (gana la más alta):
  1. OPENCLAWNET_STORAGE_ROOT  (var de entorno)
  2. Storage:RootPath          (appsettings.json)
  3. Predeterminado integrado  (C:\openclawnet en Windows)
```

```jsonc
// appsettings.json
{
  "Storage": {
    "RootPath": "D:\\openclaw",
    "AdditionalWritablePaths": [ "C:\\shared\\datasets" ]
  }
}
```

> Registrado en INFO al inicio para que la mala configuración sea visible en Aspire.

<!--
SPEAKER NOTES — config.
Tres fuentes. La variable de entorno gana para que contenedores y CI puedan sobrescribir sin tocar JSON. Appsettings es la respuesta cotidiana. El predeterminado integrado se activa para UX de primera ejecución. AdditionalWritablePaths es la lista permitida explícita para "sí, quiero que el agente también pueda escribir aquí" — usado con cuidado. El log INFO de inicio es la recomendación de endurecimiento: la mala configuración se vuelve visible en el panel, no silenciosa.
-->

---

## `OPENCLAWNET_STORAGE_ROOT` — solo un nombre

> "No tengas dos variables de entorno. Un atacante que pueda configurar el entorno del proceso podría configurar la *inesperada* y redirigir silenciosamente el almacenamiento."

- Elige **un** nombre → documéntalo → ignora todo lo demás
- `OPENCLAW_STORAGE_DIR` **no** se consulta (incluso si está presente)
- Bonus: registra la ruta resuelta **y su fuente** (env / appsettings / predeterminado)

<!--
SPEAKER NOTES — nombre único.
Modelo de amenaza sutil de Drummond. Si respetas tanto OPENCLAWNET_STORAGE_ROOT como OPENCLAW_STORAGE_DIR, un atacante que puede configurar una pero no la otra en un contenedor mal configurado redirige todas tus escrituras. Elige un nombre, documéntalo en voz alta, ignora todo lo demás. El log de inicio incluye la FUENTE del valor — variable de entorno, appsettings, o predeterminado — para que la mala configuración esté a un vistazo del panel de Aspire.
-->

---

## `ISafePathResolver` — un resolvedor, una regla

```csharp
public interface ISafePathResolver
{
    PathResolution Resolve(string requested, string? scope = null);
}

public sealed record PathResolution(
    bool IsAllowed,
    string? FullPath,
    string? Reason);
```

- Toda resolución de rutas va aquí
- Ninguna herramienta llama a `Path.GetFullPath` sobre entrada del LLM directamente
- El resolvedor impone H-1, H-3, H-4, H-5 en un solo lugar
- Parámetro `scope` opcional para aislación por agente (H-6)

<!--
SPEAKER NOTES — resolvedor.
El punto único de estrangulamiento. Cada herramienta que toma una ruta delega a este resolvedor. Dentro de él, todos los invariantes de endurecimiento viven en UNA clase comprobable — no cinco copias en cinco herramientas. El parámetro scope es la costura para futura aislación por agente: hoy predetermina a RootPath; mañana podemos pasar agents/{name}/ sin romper la API.
-->

---

## H-1: contención de raíz de almacenamiento, cierre seguro (fail-closed)

```csharp
// Dentro del resolvedor
var full = Path.GetFullPath(requested);
var allowedRoots = new[] { _root, ..._additional };

if (!allowedRoots.Any(root => IsContained(full, root)))
    return PathResolution.Denied("outside storage root");
```

- Las lecturas PUEDEN ser más amplias, las escrituras DEBEN estar dentro de `RootPath` (+ lista permitida)
- **Rechazar**, no reescribir silenciosamente
- Misma puerta para herramientas `ITool` y MCP

<!--
SPEAKER NOTES — H-1.
Invariante más importante. Cada escritura — cada una — tiene que aterrizar bajo la raíz de almacenamiento o bajo una lista permitida de rutas adicionales explícitas. Las lecturas pueden ser más amplias porque las lecturas son de menor riesgo y a veces legítimamente necesitas mirar un proyecto hermano. La decisión de diseño crucial: RECHAZAR, no REESCRIBIR. Si el LLM emite C:\Windows\System32, decimos "no" — no decimos "redigiré silenciosamente eso a C:\openclawnet\Windows\System32".
-->

---

## H-2: un sanitizador, un resolvedor

- `ISafePathResolver` es **el** punto de entrada de rutas
- Ninguna herramienta llama a `Path.GetFullPath` / `Path.Combine` sobre entrada del LLM
- Completamente probado unitariamente con casos positivos **y** adversarios
- Auditado contra H-1, H-3, H-4, H-5 en un solo lugar

> Si encuentras una herramienta llamando a `Path.GetFullPath` sobre entrada de usuario, reporta un bug.

<!--
SPEAKER NOTES — H-2.
La regla de "detener implementaciones proliferantes". La forma más confiable de asegurar que cada resolución de ruta esté endurecida es tener solo UN lugar que lo haga. Tenemos un elemento de lista de verificación de revisión de código: cualquier herramienta nueva que tome una cadena de ruta debe inyectar ISafePathResolver y delegar. Sin excepciones. Las pruebas unitarias adversarias viven junto a ella.
-->

---

## H-3: sin escapes de punto de reanálisis

```csharp
var info = new FileInfo(fullPath);
var realTarget = info.ResolveLinkTarget(returnFinalTarget: true);
if (realTarget != null && !IsContained(realTarget.FullName, _root))
    return PathResolution.Denied("reparse-point escape");
```

- `Path.GetFullPath` **no** resuelve uniones / enlaces simbólicos
- Una unión dentro de `RootPath` → `C:\Windows` pasaría de otro modo
- Re-verificar **cada padre** de la ruta resuelta
- La creación de enlaces simbólicos por la herramienta misma está prohibida

<!--
SPEAKER NOTES — H-3.
Sutil. Path.GetFullPath NO sigue puntos de reanálisis — resuelve .. y barras redundantes, pero una unión de directorio dentro de la raíz de almacenamiento apuntando a C:\Windows pasa la verificación de prefijo. Usamos ResolveLinkTarget en la ruta final Y en cada directorio padre. Sí, es costoso en rutas frías; cacheamos. Los enlaces simbólicos creados por el agente están prohibidos de plano — demasiado fácil de usar como escondite.
-->

---

## H-4: contención segura de límites

```csharp
static bool IsContained(string path, string root)
{
    root = Path.TrimEndingDirectorySeparator(root);
    return path.Equals(root, StringComparison.OrdinalIgnoreCase) ||
           path.StartsWith(root + Path.DirectorySeparatorChar,
                           StringComparison.OrdinalIgnoreCase);
}
```

- `C:\openclawnet` es un **prefijo** de `C:\openclawnet-evil`
- `StartsWith` simple ampliaría silenciosamente el límite
- La verificación de separador-final-o-fin lo arregla
- La prueba de regresión en el caso `evil` se envía en la suite

<!--
SPEAKER NOTES — H-4.
Bug de prefijo de cadena que muerde a cada biblioteca de manejo de rutas en algún punto. C:\openclawnet vs C:\openclawnet-evil — mismo prefijo, directorio diferente. La corrección es requerir igualdad O startswith de root+separador. Hay una prueba de regresión para este par exacto para que una refactorización futura no pueda reintroducir el bug.
-->

---

## H-5: lista estricta de nombres permitidos

```csharp
private static readonly Regex SafeName =
    new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.Compiled);

private static readonly HashSet<string> Reserved = new(
    [ "CON", "PRN", "AUX", "NUL",
      "COM1", "COM2", … "COM9",
      "LPT1", "LPT2", … "LPT9" ],
    StringComparer.OrdinalIgnoreCase);
```

- Lista permitida `[A-Za-z0-9._-]`, longitud ≤ 64
- Rechazar nombres de dispositivos reservados de Windows (insensible a mayúsculas)
- Rechazar punto inicial, punto/espacio final
- Misma regla para nombres de **agente**, **workspace**, **subida**, **habilidad**

<!--
SPEAKER NOTES — H-5.
Adiós a la vieja lista de denegación de tres subcadenas. Lista permitida gana a lista de denegación cada vez. Límite de sesenta y cuatro caracteres porque MAX_PATH de Windows se pone feo después de eso. Nombres de dispositivos reservados — CON, PRN, AUX, NUL, COM1-9, LPT1-9 — son especiales en Windows y crearían un directorio que no puedes eliminar. Puntos y espacios finales también: Windows los elimina silenciosamente, así que "foo." y "foo" colisionan de formas sorprendentes.
-->

---

## H-6: costura de alcance por agente

```csharp
public interface ISafePathResolver
{
    PathResolution Resolve(string requested,
                           string? scope = null);  // ← futura raíz por agente
}
```

- `scope` predetermina a `StorageOptions.RootPath`
- Puede configurarse a `agents/{name}/` por solicitud
- **No se envía lógica de alcance por agente en v1** — solo la costura
- Evita romper la API más tarde

<!--
SPEAKER NOTES — H-6.
Con visión de futuro. Hoy el runtime del agente pasa scope=null y el resolvedor usa RootPath. Mañana cuando enviemos aislación multi-agente — agente de Slack vs agente de Telegram vs agente de investigación — el runtime puede pasar agents/SlackAgent/ y esa única invocación de agente solo puede escribir en su propio subárbol. La filtración entre agentes se vuelve imposible. Hoy: solo el parámetro. Mañana: la política.
-->

---

## H-7: endurecimiento de ACL en subdirectorios de credenciales

```csharp
// Al inicio, después de EnsureDirectories():
var keysDir = Path.Combine(_root, "dataprotection-keys");

if (OperatingSystem.IsWindows())
    SetExplicitDacl(keysDir, currentUser, FullControl,
                    inheritance: false);
else
    File.SetUnixFileMode(keysDir, UnixFileMode.UserRead | UserWrite | UserExecute);
```

- Verificar que el usuario actual tiene control total sobre `RootPath`
- DACL explícita en `dataprotection-keys/`, `vault/`, `tokens/`
- POSIX: `chmod 0700` en lo mismo
- Rechazar iniciar servicios de credenciales si falla la verificación de ACL

<!--
SPEAKER NOTES — H-7.
Endurecimiento de ACL para los directorios que contienen secretos. Por defecto C:\openclawnet hereda de la raíz del volumen, que en la mayoría de las instalaciones de Windows otorga Users(OI)(CI)M — cada usuario local puede leerlo. Ese es el predeterminado incorrecto para un directorio que contiene claves de DataProtection de ASP.NET, tokens OAuth, y futuros bóvedas de claves API. Establecemos una DACL explícita en los subdirectorios de credenciales al inicio: usuario actual + SYSTEM, sin herencia. POSIX obtiene chmod 0700. Si falla la verificación, rechazamos iniciar los servicios que portan credenciales con un mensaje de remediación claro.
-->

---

## H-8: auditar cada escritura

```jsonc
{
  "type": "FileSystemWriteAudit",
  "agent": "GptAgent",
  "action": "write",
  "path": "C:\\openclawnet\\agents\\GptAgent\\out.md",
  "bytes": 4218,
  "sha256": "9f3c…",
  "source": "llm-suggested",
  "runId": "r-7b4a",
  "timestamp": "2026-05-22T14:03:11Z"
}
```

- **Cada escritura exitosa** → registro de auditoría
- **Cada escritura bloqueada** → auditoría WARN con la entrada no resuelta
- Fundamento para forense, facturación, políticas de retención

<!--
SPEAKER NOTES — H-8.
Cada escritura al disco deja un rastro. Las escrituras exitosas obtienen la ruta resuelta, conteo de bytes, SHA-256 del contenido, atribución de fuente (¿fue esto sugerido por el LLM o explícito del usuario?), ids de correlación. Las escrituras fallidas — path traversal bloqueado, ACL denegada, fallo de lista permitida de nombres — también son auditadas en WARN con la cadena de entrada original no resuelta para forense. Combinado con auditoría de habilidades (S-9 de la Parte 3) puedes decir exactamente qué pasó durante cualquier turno de chat.
-->

---

## Los ocho, lado a lado

| # | Qué |
|---|------|
| **H-1** | Contención en raíz de almacenamiento, cierre seguro (fail-closed) |
| **H-2** | Un sanitizador / un resolvedor (`ISafePathResolver`) |
| **H-3** | Sin escapes por reparse-point |
| **H-4** | Verificación de contención segura en límites |
| **H-5** | Lista de nombres permitidos estricta |
| **H-6** | Alcance de separación por agente |
| **H-7** | ACL restrictiva en raíz + subdirectorios de credenciales |
| **H-8** | Auditar cada escritura |

> Ocho invariantes. Un resolvedor. **Cierre seguro (fail-closed) por diseño.**

<!--
SPEAKER NOTES — resumen.
Ocho invariantes en una diapositiva. Memoriza estos — son el contrato que cualquier código que tome rutas debe satisfacer. Igual que la lista de habilidades S-1..S-12, cada PR se revisa contra ellos. Tenemos pruebas unitarias cubriendo cada uno con casos adversarios.
-->

---

## Conectándolo

```csharp
// Program.cs
builder.Services
    .AddOpenClawStorage()         // binds StorageOptions, ensures dirs, ACL
    .AddSafePathResolver()        // ISafePathResolver
    .AddOpenClawTools();          // FileSystemTool uses the resolver

// Anywhere a tool needs a path:
public sealed class MyTool(ISafePathResolver paths) : ITool { … }
```

- Un método de extensión por preocupación
- Resolvedor inyectado por DI — sin estáticos, sin globales
- Funciona igual en Gateway, AppHost, servidores MCP

<!--
SPEAKER NOTES — conexión.
Tres métodos de extensión, en este orden. AddOpenClawStorage vincula StorageOptions, asegura el árbol de directorios y ejecuta el endurecimiento de ACL. AddSafePathResolver registra el resolvedor singleton. AddOpenClawTools conecta cada herramienta incorporada para usar el resolvedor. Las herramientas personalizadas solo inyectan ISafePathResolver y están listas.
-->

---

## Interfaz de configuración

- Nueva tarjeta **"Storage"** en la página `/settings`
- Muestra: raíz actual, fuente (env / appsettings / predeterminado), espacio libre
- Botón "Mover raíz a…" (escribe nueva ruta, requiere reinicio)
- Salud: estado de ACL por subdir de credenciales
- Medidor de cuota por subcarpeta de nivel superior

> La descubribilidad reemplaza las conjeturas.

<!--
SPEAKER NOTES — interfaz de configuración.
Hasta ahora tenías que saber sobre Storage:RootPath en appsettings para siquiera verificar dónde iban los archivos. La página Settings ahora tiene una tarjeta Storage mostrando la raíz actual, la FUENTE (para que sepas si vino de env o config), y espacio libre. Mover-raíz requiere un reinicio por diseño — no queremos migrar escrituras en vivo. El estado de ACL expone violaciones de H-7 como puntos rojos.
-->

---

## Historia de migración

- Primer arranque después de actualización: detectar raíz antigua (`C:\openclawnet\storage\`)
- Ofrecer mover contenidos a `C:\openclawnet\` (un clic)
- O mantener ruta antigua vía anulación `Storage:RootPath`
- Saltar migración completamente con `--no-migrate`

> Sin movimientos silenciosos. Sin pérdida de datos. **Tú optas por entrar o salir.**

<!--
SPEAKER NOTES — migración.
Eliminamos el sufijo /storage entre lanzamientos. Las instalaciones existentes perderían el rastro de sus archivos a menos que manejemos la migración explícitamente. En el primer arranque detectamos el diseño antiguo, preguntamos al usuario, y ya sea movemos atómicamente o mantenemos la raíz antigua fija vía config. El flag de CLI existe para despliegues desatendidos donde no es posible preguntar.
-->

---

<!-- _class: lead -->

# 🧠  Parte 5 — Hoja de ruta de memoria

<!--
SPEAKER NOTES — divisor de Parte 5.
La parte prospectiva. La memoria está mayormente diseñada, parcialmente construida, y la versión de grado de producción es lo que la Sesión 4 recogerá. Ocho diapositivas sobre cuál es el problema, cuál es la estrategia y qué viene después.
-->

---

## El problema de la ventana de contexto

- Los LLMs tienen límites de tokens — **8K a 128K** típico
- Cada mensaje en el historial se reenvía en cada turno
- Truncamiento ingenuo = el agente **olvida**
- El costo crece linealmente incluso con modelos locales (latencia, GPU)
- Chat de 100 mensajes a 4K promedio = **400K tokens** por turno

> *"¿Ya discutimos esto?" — tu agente, cada conversación.*

<!--
SPEAKER NOTES — problema de ventana de contexto.
Por qué importa la memoria. Incluso con una ventana de contexto de 128K, cada turno reenvía todo el historial. Después de 100 mensajes tus prompts son enormes, tu latencia es alta, tu GPU está caliente, y el modelo empieza a perder el medio del contexto de todos modos (el problema de atención en forma de U). Truncamiento ingenuo — soltar los N más antiguos — significa que el agente olvida el nombre del usuario. Ambos son malos.
-->

---

## Estrategia de resumen

```
┌──────────────── historial completo ──────────────┐
│                                                   │
│  [viejo]  [viejo]  [viejo]  [reciente]  [reciente]│
│    │       │       │        │          │          │
│    └───────┴───┬───┘        │          │          │
│         resumir             │          │          │
│            │                │          │          │
│            ▼                ▼          ▼          │
│      [resumen]   +   [reciente]    [reciente]     │
└────────────────────────────────────────────────────┘
             │
             ▼
    System prompt: instrucciones + resumen + reciente
```

- Mensajes recientes: **verbatim** (últimos N)
- Mensajes más antiguos: **resumidos** en puntos clave
- Muy antiguos: **búsqueda semántica** bajo demanda

<!--
SPEAKER NOTES — estrategia.
Estrategia de tres niveles. Los mensajes recientes se mantienen verbatim porque el modelo los necesita palabra por palabra para coherencia. Los mensajes más antiguos colapsan en un resumen de párrafo. Los mensajes muy antiguos están fuera del prompt activo completamente pero almacenados en un índice vectorial — el agente puede recuperarlos por similitud semántica cuando sea relevante. Este es el patrón estándar en todos los frameworks de agentes modernos.
-->

---

## Entidad `SessionSummary`

```csharp
public sealed class SessionSummary
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public string Summary { get; init; } = "";
    public int CoveredMessageCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
```

- Una sesión → muchos resúmenes (ventana rodante a medida que crece)
- `CoveredMessageCount` le dice al compositor dónde empezar "reciente"
- Cascada de eliminación con la sesión padre

<!--
SPEAKER NOTES — entidad.
Una entidad pequeña de EF Core. SessionId es la clave foránea, Summary es la prosa real, CoveredMessageCount le dice al compositor de prompt "los primeros N mensajes de esta sesión están resumidos — empieza verbatim desde el mensaje N+1". Cada resumen es inmutable; se agregan nuevos resúmenes en lugar de actualizarse, para que podamos reconstruir cualquier vista histórica de la conversación.
-->

---

## Forma de `IMemoryService`

```csharp
public interface IMemoryService
{
    Task<SessionSummary?> GetLatestSummaryAsync(
        Guid sessionId, CancellationToken ct = default);

    Task StoreSummaryAsync(
        SessionSummary summary, CancellationToken ct = default);

    Task<MemoryStats> GetStatsAsync(
        Guid sessionId, CancellationToken ct = default);
}
```

- Respaldado por `IDbContextFactory<OpenClawDbContext>` (patrón async correcto)
- `MemoryStats` expone mensajes totales, conteo de resúmenes, tiempo de último resumen
- Disparado por umbral de conteo de mensajes (predeterminado 20)

<!--
SPEAKER NOTES — interfaz.
Tres métodos. Obtener el último resumen para que el compositor pueda inyectarlo. Almacenar un nuevo resumen cuando el umbral se dispara. GetStatsAsync alimenta el panel de memoria de UI. El patrón factory es el correcto para servicios async — servicio singleton, DbContext con alcance por llamada, sin trampas de seguridad de hilos. El umbral de 20 mensajes es configurable por sesión.
-->

---

## Embeddings locales — sin llamadas API

- `Elbruno.LocalEmbeddings` — modelos ONNX, se ejecuta en proceso
- Embeber texto → vector de 384 dimensiones
- Similitud de coseno para búsqueda de vecino más cercano
- **Sin red**, sin clave API, ningún dato sale de la máquina

```csharp
var v1 = await _embeddings.EmbedAsync("dependency injection in .NET");
var v2 = await _embeddings.EmbedAsync("how do I configure IoC?");
var sim = CosineSimilarity(v1, v2);  // ~0.82 — coincidencia fuerte
```

<!--
SPEAKER NOTES — embeddings.
Los embeddings son la primitiva que potencia la búsqueda semántica. Microsoft ofrece APIs de embedding administradas pero para desarrollo local-primero usamos Elbruno.LocalEmbeddings que envuelve ONNX. 384 dimensiones es el tamaño all-MiniLM — diminuto, rápido, suficientemente bueno para recuperación conversacional. El ejemplo muestra la ganancia: "dependency injection" e "IoC container" se embeben a vectores que son 0.82 similar por coseno aunque no comparten palabras superficiales.
-->

---

## Búsqueda semántica a través de sesiones pasadas

- Cada mensaje → embedding → columna de vector SQLite
- Nueva pregunta → embeber → top-K mensajes pasados más cercanos
- Fragmentos recuperados → inyectados en el system prompt como contexto
- Encontrar la conversación sobre "DI" incluso si el usuario escribió "IoC"

> El agente **recuerda** — sin una ventana de contexto de 1M-token.

<!--
SPEAKER NOTES — búsqueda semántica.
El eventual tercer nivel. Cada mensaje se embebe una vez y se almacena en caché. Llega una nueva pregunta, la embebemos, ejecutamos un escaneo de similitud de coseno sobre embeddings de mensajes pasados, tomamos los top K, inyectamos esos fragmentos como contexto adicional. SQLite no es una base de datos vectorial pero a escala de historial de conversación (miles de mensajes) es perfectamente adecuado — usamos una columna BLOB serializada y un top-K hecho a mano. Si creces más allá de eso, intercambia DuckDB o un almacén vectorial real sin cambio de API.
-->

---

## Panel de memoria transparente

```
┌─ Memoria ────────────────────────────────────────┐
│ Total mensajes         : 142                     │
│ Resumidos              : 100  (3 resúmenes)      │
│ Recientes (verbatim)   : 42                      │
│ Último resumen         : hace 2 minutos          │
│ Tokens de prompt estimados: 3.8 K  (eran 26 K)   │
└──────────────────────────────────────────────────┘
```

- Los usuarios **ven** lo que el sistema de memoria está haciendo
- No es una caja negra — cada conteo es auditable
- Respalda el endpoint `GET /api/memory/{sessionId}/stats`

<!--
SPEAKER NOTES — panel.
La transparencia es una característica. Los usuarios entran en pánico cuando una IA afirma "recordar" cosas — quieren saber cómo. El panel muestra mensajes totales, cuántos están resumidos (y en cuántos resúmenes), cuántos están aún verbatim, cuándo se disparó el último resumen, y el impacto en tokens. El número "eran 26 K" es el más poderoso — muestra el ahorro sin resumen en términos concretos.
-->

---

## Qué viene después (vista previa de Sesión 4)

- ✅ Diseñado: `SessionSummary`, `IMemoryService`, `IEmbeddingsService`
- 🚧 Construyendo: resumidor de grado de producción con política de reintento de modelo
- 🚧 Construyendo: índice vectorial sobre mensajes pasados (blob SQLite → DuckDB)
- 🔜 Próximamente: espacios de nombres de memoria por agente + políticas de retención
- 🔜 Próximamente: memoria **exportación / importación** para portabilidad

> **Sesión 4** = nube + programación + memoria a escala.

<!--
SPEAKER NOTES — qué viene.
Dónde estamos: el diseño está hecho, la entidad existe, las interfaces son estables. Lo que falta: un resumidor robusto que maneje fallas de modelo con gracia, un índice vectorial real, aislamiento de memoria por agente, e importación-exportación. Todo eso llega en Sesión 4 junto con proveedores en la nube y el programador. Para el final de Sesión 4 el agente recordará a través de reinicios y a través de sesiones.
-->

---

<!-- _class: lead -->

# 🧪  Parte 6 — Demos de consola

<!--
SPEAKER NOTES — divisor de Parte 6.
Tres demos, ocho minutos en total. Iniciaremos la pila, golpearemos la API de habilidades, y soltaremos una habilidad autoral manual para probar hot-reload.
-->

---

## Demo 1 — recorrido de `aspire start`

```pwsh
cd C:\src\openclawnet
aspire run

# Expected:
# 🟢 OpenClawNet.AppHost (3 of 3 running)
#    ├── 🟢 gateway       https://localhost:7234
#    ├── 🟢 ollama        http://localhost:11434
#    └── 🟢 dashboard     https://localhost:17000
```

- Observa el ícono de bandeja de Aspire Monitor ponerse verde
- Abre el dashboard → confirma línea de log `Storage:RootPath`
- Log INFO: `Storage root resolved to C:\openclawnet (source: default)`

<!--
SPEAKER NOTES — demo 1.
Demo en vivo. aspire run en el repo, observa Aspire Monitor en la bandeja ponerse verde a medida que los recursos suben. Abre el dashboard, desplaza el log para encontrar la línea de almacenamiento que enviamos esta sesión — "Storage root resolved to C:\openclawnet (source: default)". Source es el valor que la revisión de endurecimiento pidió. Si la variable env está configurada, muestra "source: env". Si appsettings, "source: config". Visible de un vistazo.
-->

---

## Demo 2 — `curl /api/skills`

```pwsh
# List skills the agent actually uses
curl https://localhost:7234/api/skills | jq

# Output:
# [
#   { "name": "memory",        "layer": "system", "enabledFor": ["GptAgent"] },
#   { "name": "doc-processor", "layer": "system", "enabledFor": ["GptAgent"] }
# ]

# Toggle one for a specific agent
curl -X POST https://localhost:7234/api/agents/GptAgent/skills/memory/disable
```

- API y agente comparten el **mismo** registro — sin deriva
- Desactivar toma efecto en el **siguiente** turno de chat (S-10)

<!--
SPEAKER NOTES — demo 2.
Curl al endpoint de habilidades. Dos habilidades, ambas capa de sistema, ambas habilitadas para GptAgent. Nota la forma por agente: enabledFor es un array, no un booleano global. POST disable, luego envía un mensaje de chat — el agente responde sin la habilidad de memoria en su prompt. Verificamos revisando la auditoría de prompt. Esta es la API unificada que la diapositiva de error titular prometió.
-->

---

## Demo 3 — soltar habilidad manual

```pwsh
# 1. Author a skill in your editor
code C:\openclawnet\skills\agents\GptAgent\skills\concise-tone\SKILL.md

# 2. File contents:
@"
---
name: concise-tone
description: Short, friendly responses
---

Keep responses under 3 sentences. No buzzwords.
"@ > SKILL.md

# 3. Watch the gateway log:
# [INFO] SkillDiscovered  name=concise-tone layer=agents:GptAgent
# [INFO] SkillLoaded      name=concise-tone duration=12ms

# 4. Send a chat — agent is now concise.
```

<!--
SPEAKER NOTES — demo 3.
El demo "wow". Crea una habilidad en tiempo real. Guarda el archivo. Observa el log de gateway emitir SkillDiscovered y SkillLoaded — eso es el FileSystemWatcher y la reconstrucción del registro disparándose. Envía un mensaje de chat en la UI y la respuesta es de repente dos oraciones y carece de "aprovechar". Sin reinicio. Sin despliegue. Sin código. Ese es todo el discurso en 60 segundos.
-->

---

<!-- _class: lead -->

# 🎯  Cierre

<!--
SPEAKER NOTES — divisor de cierre.
Dos diapositivas más una diapositiva de pregunta.
-->

---

## Perspectivas clave

1. 🧠 **Las habilidades son markdown** — cualquiera puede crear, sin código, sin reinicio
2. 🛡️ **El almacenamiento es fail-closed** — ocho invariantes, un resolvedor, sin reescrituras silenciosas
3. 📈 **La memoria es transparente** — los usuarios ven qué está resumido
4. 🔧 **La llamada de herramientas es portable** — mismo agente, cuatro proveedores
5. 👀 **Las operaciones son visuales** — monitores Ollama + Aspire en la bandeja

> *"Las herramientas son brazos. Las habilidades son personalidad. El almacenamiento es el piso."*

<!--
SPEAKER NOTES — perspectivas.
Cinco conclusiones. Las habilidades son contenido no código — esa es la ganancia más visible para el usuario. El almacenamiento es fail-closed — esa es la ganancia de seguridad. La memoria es transparente — esa es la ganancia de confianza. La portabilidad de llamada de herramientas es la ganancia de ingeniería. Los monitores de bandeja son la ganancia de experiencia de desarrollador. Cada ítem mapea a una de las seis partes de la sesión.
-->

---

## Lo que construimos hoy ✅

- ✅ Dos aplicaciones de bandeja NuGet — Ollama Monitor + Aspire Monitor
- ✅ Formato de llamada de herramientas alineado con OpenAI
- ✅ Refactor de FileSystemTool (5 archivos, ~120 LOC cada uno)
- ✅ Tres sanitizadores reutilizables (path / URL / JSON)
- ✅ Un solo `ISkillsRegistry` (eliminado `FileSkillLoader` paralelo)
- ✅ Almacenamiento de 3 capas con hot-reload de `FileSystemWatcher`
- ✅ Habilitación por agente vía `enabled.json` (fail-closed)
- ✅ UI de `Skills.razor` + modal de asignación + asistente de importación
- ✅ `C:\openclawnet\` predeterminado + variable env `OPENCLAWNET_STORAGE_ROOT`
- ✅ `ISafePathResolver` aplicando **H-1..H-8**
- ✅ Endurecimiento de ACL en subdirs de credenciales
- ✅ Hoja de ruta de memoria: `SessionSummary`, embeddings locales, búsqueda semántica

<!--
SPEAKER NOTES — lo que construimos.
Doce marcas de verificación. La mitad son visibles para el usuario (monitores, página de habilidades, tarjeta de configuración). La mitad son calidad bajo el capó (refactors, sanitizadores, endurecimiento). Las dos mitades van juntas: las características visibles para el usuario solo son seguras PORQUE el trabajo bajo el capó llegó primero. Esa es la lección de la sesión 3.
-->

---

## Vista previa de Sesión 4

- ☁️ **Proveedores en la nube** — Azure OpenAI, Foundry a escala
- ⏰ **Programación de trabajos** — expresiones cron, trabajos durables
- 🧠 **Memoria a escala** — índice vectorial, políticas de retención
- 🩺 **Health checks + pruebas** — endurecimiento de producción
- 🎬 **Final de serie** — demo de plataforma completa

> Hoy: un agente con personalidad, límites y un plan de memoria.
> Siguiente: un agente que se ejecuta mientras duermes.

<!--
SPEAKER NOTES — vista previa de Sesión 4.
Hacia dónde vamos después. Proveedores en la nube significa que el mismo agente se ejecuta contra Azure OpenAI sin cambios de código — el trabajo de alineación de llamada de herramientas en la Parte 2 es lo que hace eso posible. La programación significa trabajos impulsados por cron que el agente ejecuta autónomamente. La memoria a escala termina el trabajo que esbozamos en la Parte 5. Las pruebas + health checks convierten el demo en un despliegue. La Sesión 4 es el final.
-->

---

<!-- _class: lead -->

# ¿Preguntas?

<div class="speakers">

**Bruno Capuano** — Principal Cloud Advocate, Microsoft
[github.com/elbruno](https://github.com/elbruno) · [@elbruno](https://twitter.com/elbruno)

**Pablo Nunes Lopes** — Cloud Advocate, Microsoft
[linkedin.com/in/pablonuneslopes](https://www.linkedin.com/in/pablonuneslopes/)

</div>

<br>

`elbruno/openclawnet` · MIT licensed · contributions welcome
`docs/sessions/session-3/` for everything from today

<!--
SPEAKER NOTES — cierre.
Gracias a todos. El repo es github.com/elbruno/openclawnet, licencia MIT. Todo lo de hoy — diapositivas, script del orador, prompts de copilot, los documentos de propuesta — vive bajo docs/sessions/session-3/. Las dos nuevas herramientas se instalan con un comando dotnet tool install -g y viven en tu bandeja. Si quieres extender algo, el drop-in manual de habilidad es el punto de partida más gratificante: escribe un SKILL.md, guárdalo, ve al agente cambiar. ¿Preguntas?
-->
