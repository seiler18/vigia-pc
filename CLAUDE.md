# Vigía — monitor de temperatura y FPS para jugar

Franja delgada a todo lo ancho de una pantalla, siempre encima, con estado,
FPS, GPU, CPU y RAM. App de escritorio para Windows; repo público
`seiler18/vigia-pc`. Nivel A del estándar de `C:\Desarrollo_JS`.

## Stack

- **C# / WPF, .NET 10** (`net10.0-windows`). Un proyecto: `src/Vigia`.
- Única dependencia: `Microsoft.Diagnostics.Tracing.TraceEvent` (ETW, para FPS).
- WinForms activado solo por `NotifyIcon` (la bandeja).

## La regla que manda: poco invasivo

Lo pidió Jesús explícitamente. Antes de agregar algo, comprobar que no rompe
ninguna de estas:

| Regla | Cómo se cumple | Qué la rompería |
|---|---|---|
| Sin driver de kernel ni administrador | Temperatura CPU por zona ACPI (PDH), GPU por D3DKMT | LibreHardwareMonitor, WinRing0, PawnIO, `requireAdministrator` |
| Sin tocar el proceso del juego | FPS por ETW (DXGI/D3D9 `Present_Start`) | Hooks/inyección tipo RTSS: los anti-cheat banean |
| Sin robar el foco | `WS_EX_NOACTIVATE`, `ShowActivated=False` | `Activate()`, menús WPF en la franja |
| Sin competir por recursos | `RenderMode.SoftwareOnly`, prioridad `BelowNormal`, EcoQoS, una lectura por segundo | Animaciones, relojes cortos, render por GPU |

**Medir la CPU después de cada cambio de interfaz** (ver skill
`medir-consumo`). Referencia del 2026-11-07: 6,8 ms de CPU por segundo y
45 MB privados con un juego abierto. Un reloj de 2 s que reafirmaba el
«siempre encima» costaba un tercio de eso; se reemplazó por un gancho de
`EVENT_SYSTEM_FOREGROUND`.

## Estructura

```
src/Vigia/
├── App.xaml(.cs)        arranque: instancia única, prioridad, cableado
├── Configuracion.cs     %APPDATA%\Vigia\config.json (umbrales, borde, pantalla)
├── Evaluacion.cs        lecturas → nivel y frase de estado
├── Sensores/            una clase por fuente; Recolector las lee cada 1 s en otro hilo
├── Nativo/              P/Invoke: Pdh, D3dkmt, Sistema (ventanas, monitores, memoria)
└── Interfaz/            Franja (la ventana), Segmento (un componente), Bandeja, Paleta
```

## Trampas conocidas

- **Contadores de rendimiento en Windows en español**: los nombres están
  traducidos. Siempre `PdhAddEnglishCounter` con la ruta en inglés; con
  `PdhAddCounter` o `PerformanceCounter` fallan con `c0000bb8`.
- **Posición en píxeles físicos**: con dos pantallas de distinta escala (aquí:
  externa 100 %, notebook 125 %), la franja se coloca con `SetWindowPos` y no
  con `Left/Top` de WPF. Tras `OnDpiChanged` hay que reacomodar.
- **`LibraryImport` no pasa delegados**: `SetWinEventHook` va con `DllImport`
  y el delegado se guarda en un campo (si el GC lo recoge, el gancho se cae).
- **Muestras vacías de «GPU Engine»**: cuando un proceso abre o cierra la GPU,
  la lista de instancias sale vacía un segundo; se repite la muestra anterior.
- **TraceEvent copia DLL nativas** (`amd64\`, `x86\`) que Vigía no usa; el
  `.csproj` las quita con el target `QuitarNativosDeTraceEvent`.
- Los FPS necesitan grupo «Usuarios del registro de rendimiento» o admin. El
  usuario de este equipo ya está en el grupo.
- **Sesión ETW huérfana**: si Vigía se mata a la fuerza (Administrador de
  tareas), la sesión `Vigia-FPS` sigue viva hasta el próximo arranque de
  Vigía (que la reemplaza) o el reinicio. A mano: `logman stop Vigia-FPS -ets`.
  Al salir por la bandeja se cierra sola.
- **Nada debe tumbar la franja**: `config.json` se normaliza al cargar (umbral
  nulo o invertido, opacidad fuera de rango, borde desconocido → por
  defecto), `Guardar()` no lanza, y los errores de interfaz y de sensores van
  a `%APPDATA%\Vigia\errores.log` (recortado a 256 KB) en vez de cerrar la app.
- **Dependencias con candado**: `src/Vigia/packages.lock.json` va en git y CI
  restaura con `--locked-mode`. Tras cambiar un paquete, borrar `obj/`,
  `dotnet restore Vigia.slnx --force-evaluate` y commitear el candado junto
  con el `.csproj`. Con un `obj/` sucio de un `publish` anterior el candado
  arrastra referencias implícitas (`Microsoft.NET.ILLink.Tasks`) que el
  servidor no tiene, y CI falla con `NU1004` (pasó el 2026-11-07).

## Comandos

```powershell
dotnet build Vigia.slnx -c Release
dotnet run --project src/Vigia
```

Instalar en este PC: skill `instalar-local`. Publicar versión: skill
`publicar-version` (el tag `vX.Y.Z` dispara `publicar.yml`, que sube
`Vigia.exe` autónomo, su `.sha256` y una atestación de procedencia).

El `.exe` no va firmado; la atestación lo compensa: prueba que ese archivo lo
compiló el workflow de este repo. Los dos jobs de `publicar.yml` van
separados (el que compila no escribe, el que publica no ejecuta código del
repo): no juntarlos.

## Verificación

Aquí no hay navegador ni capturas automatizadas de la interfaz: lo
verificable es el build, que el proceso siga vivo, el consumo medido y una
captura de pantalla con `CopyFromScreen` (en píxeles físicos: el proceso que
captura debe declararse `PerMonitorV2`). La revisión visual final la hace
Jesús.
