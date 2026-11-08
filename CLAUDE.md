# Vigía — monitor de temperatura y FPS para jugar

Franja delgada pegada a cualquier borde de una pantalla (fila arriba o
abajo, columna a los costados), siempre encima, con estado, FPS, GPU, CPU y
RAM. Reserva su espacio como la barra de tareas. Doble clic abre
«Personalizar». App de escritorio para Windows; repo público
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
| Sin robar el foco | `WS_EX_NOACTIVATE`, `ShowActivated=False` en la franja; solo «Personalizar» toma el foco, porque se abre a propósito | `Activate()` en la franja, menús WPF en la franja |
| Sin competir por recursos | `RenderMode.SoftwareOnly`, prioridad `BelowNormal`, EcoQoS, una lectura por segundo | Animaciones, relojes cortos, render por GPU |

**Medir la CPU después de cada cambio de interfaz** (ver skill
`medir-consumo`). Referencia del 2026-11-07 con un juego abierto: 5–7 ms de
CPU por segundo en la v0.1.0 y ~8,5 ms/s con los cuatro bordes y los colores
dinámicos; unos 45 MB privados. Techo: 10 ms/s en reposo. Un reloj de 2 s que reafirmaba el
«siempre encima» costaba un tercio de eso; se reemplazó por un gancho de
`EVENT_SYSTEM_FOREGROUND`.

## Estructura

```
src/Vigia/
├── App.xaml(.cs)        arranque: instancia única, prioridad, cableado, --personalizar
├── Configuracion.cs     %APPDATA%\Vigia\config.json (umbrales, borde, colores, qué mostrar)
├── Evaluacion.cs        lecturas → nivel y frase de estado
├── Registro.cs          errores.log
├── Sensores/            una clase por fuente; Recolector las lee cada 1 s en otro hilo
├── Nativo/              P/Invoke: Pdh, D3dkmt, Sistema (ventanas, monitores, memoria),
│                        BarraDeAplicacion (reservar espacio, SHAppBarMessage)
├── Interfaz/            Franja (la ventana), Segmento (un componente; se ordena en
│                        fila o en bloque), Ajustes («Personalizar»), Bandeja, Paleta
└── Recursos/            vigia.svg (fuente del icono) y vigia.ico (generado)
```

## El icono

Es el isotipo de la marca de Jesús (`Curriculo/assets/img/marca/favicon.svg`)
sobre una baldosa azul marino, en los colores de la variante oscura. La
fuente es `Recursos/vigia.svg`, con los colores en atributos: ImageMagick
ignora el bloque `<style>`. Para regenerar el `.ico` tras cambiar el SVG:

```powershell
$m = "C:\Program Files\ImageMagick-7.1.2-Q16-HDRI\magick.exe"
& $m -background none -density 300 src\Vigia\Recursos\vigia.svg -resize 256x256 $env:TEMP\v256.png
& $m $env:TEMP\v256.png -define icon:auto-resize=256,128,64,48,40,32,24,20,16 src\Vigia\Recursos\vigia.ico
```

En la bandeja, `Bandeja.PintarVisto` repinta el visto del logo con el color
del estado; detecta sus píxeles por verde − rojo, así que si cambian los
colores del logo hay que revisar esas dos constantes.

## Trampas conocidas

- **Contadores de rendimiento en Windows en español**: los nombres están
  traducidos. Siempre `PdhAddEnglishCounter` con la ruta en inglés; con
  `PdhAddCounter` o `PerformanceCounter` fallan con `c0000bb8`.
- **Posición en píxeles físicos**: con dos pantallas de distinta escala (aquí:
  externa 100 %, notebook 125 %), la franja se coloca con `SetWindowPos` y no
  con `Left/Top` de WPF. Tras `OnDpiChanged` hay que reacomodar.
- **Reservar espacio (barra de aplicación)**: con la reserva, el área de
  trabajo del monitor ya excluye la franja; por eso `Reservar()` parte del
  monitor completo y deja que `ABM_QUERYPOS` la corra si hay otra barra. Al
  ocultarla se quita la reserva (si no, queda un hueco vacío) y también en el
  manejador de error fatal. `ABN_POSCHANGED` puede rebotar tras nuestro propio
  `ABM_SETPOS`: se ignora si llega antes de 300 ms. No usar
  `ABM_WINDOWPOSCHANGED`.
- **Una ventana que se abre sin foco queda detrás del juego**: Windows no deja
  que un proceso en segundo plano se ponga delante. «Personalizar» se abre
  `Topmost` y centrada en la pantalla de la franja.
- **Tema Fluent (`ThemeMode`) con render por software**: el fondo Mica no se
  pinta y la ventana queda semitransparente. «Personalizar» lleva
  `ThemeMode="Dark"` y fondo sólido `#202020`. `ThemeMode` es experimental en
  .NET 10 (aviso WPF0001 silenciado en el `.csproj`).
- **Colores por `DynamicResource`**: `Paleta.Aplicar` reescribe los pinceles
  de `Application.Resources`; en XAML nuevo, `DynamicResource`, o el color no
  cambiará al personalizar.
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
