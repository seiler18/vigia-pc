# 0001 — Franja de temperatura y FPS, sin driver ni inyección

- **Fecha:** 2026-11-07
- **Estado:** completado
- **Commits:** el commit inicial del repo

## Contexto

Jesús juega en un notebook (Ryzen 5 4600H, RTX 3050 Laptop, Radeon integrada,
dos pantallas: externa 1920×1080 al 100 % y la del notebook al 125 %) y
quería ver mientras juega la temperatura de la GPU y del CPU, los FPS y la
RAM, para evitar un sobrecalentamiento. Pidió dos cosas explícitas durante el
trabajo: que fuera **lo menos invasiva posible** con buen detalle y fácil de
entender, y luego que no fuera un recuadro sino **una franja a todo lo
ancho**, para tapar menos pantalla.

La primera lectura de prueba lo agarró jugando WoW: GPU a 86 °C con 88 % de
carga, CPU entre 88 y 97 °C (zona ACPI), 26–38 FPS. La RTX 3050 Laptop se
baja la frecuencia sola cerca de 87 °C: el problema era real.

## Qué se hizo

- App WPF en .NET 10 (`src/Vigia`), una franja de 30 DIP pegada al borde
  superior o inferior de la pantalla elegida, a todo el ancho del área de
  trabajo. Arrastrarla elige pantalla y borde; al soltar se acomoda.
- Sensores (`Sensores/`): CPU por PDH (`% Processor Utility`, frecuencia,
  zona térmica ACPI), GPU por D3DKMT (temperatura, ventilador) y PDH «GPU
  Engine»/«GPU Adapter Memory» (carga, VRAM), RAM por `GlobalMemoryStatusEx`,
  FPS y 1 % bajo por ETW (DXGI y D3D9 `Present_Start`).
- `Evaluacion.cs`: una frase de estado con color (verde/ámbar/rojo), con los
  umbrales en `%APPDATA%\Vigia\config.json`; se recargan al guardar.
- Aviso si el juego corre en la gráfica integrada (carga 3D del proceso por
  adaptador).
- Bandeja con icono del color del estado, modo mínimo (doble clic), fijar con
  clics pasantes, opacidad, iniciar con Windows y aviso de Windows al entrar
  en crítico (uno cada 5 minutos por motivo). Atajo Ctrl+Shift+F11.
- Estructura nivel A: `CLAUDE.md`, este hito, skills `registrar-hito`,
  `instalar-local`, `publicar-version`, `medir-consumo`; Dependabot (NuGet y
  acciones), `verificar-pr.yml` y `publicar.yml` (Release con `Vigia.exe`).

## Decisiones y alternativas descartadas

- **C#/WPF en vez de Electron o Tauri.** Electron consume 200–300 MB, justo
  lo que se quiere vigilar. Tauri obligaba a un proceso .NET aparte para los
  sensores: dos procesos y dos lenguajes para una franja.
- **.NET 10 y no .NET 8**, aunque se le ofreció el 8: el soporte del 8 termina
  el 2026-11-10, tres días después de empezar.
- **Sin LibreHardwareMonitor.** Lee el sensor interno del Ryzen, pero carga un
  driver de kernel (WinRing0, marcado por Defender; ahora PawnIO) y exige
  administrador. Se usa la zona térmica ACPI (`\_TZ.THRM`), que en este
  notebook sigue al CPU segundo a segundo. El costo: no es Tctl exacto y en
  escritorios puede no existir.
- **GPU por D3DKMT y no por NVML/NVAPI**: es lo que usa el Administrador de
  tareas, sirve para NVIDIA, AMD e Intel y no necesita nada del fabricante.
  `KMTQAITYPE_ADAPTERPERFDATA_CAPS` no devolvió la temperatura de aviso del
  fabricante en esta GPU; quedan los umbrales propios.
- **FPS por ETW y no por inyección (RTSS/MSI Afterburner)**: inyectarse en el
  juego es lo que miran los anti-cheat. ETW necesita el grupo «Usuarios del
  registro de rendimiento», y Jesús ya estaba en él. Se descartó también
  embeber `PresentMon.exe`: un binario de terceros más por una cuenta de
  eventos que son 40 líneas.
- **Recuadro → franja.** La primera versión fue un panel de 272 px de ancho
  con filas; Jesús pidió una franja a todo lo ancho. La posición se guarda
  como un punto en píxeles físicos dentro del monitor, no como nombre de
  monitor (Windows los renombra al reconectar), y se coloca con
  `SetWindowPos` porque con escalas distintas `Left/Top` de WPF no alinean
  al borde.
- **Render por software.** Medido: por hardware bajaba de 39 a 31 ms/s de CPU
  pero subía la memoria privada de 46 a 98 MB y ocupaba la GPU. No compensa.
- **«Siempre encima» por evento y no por reloj.** Un reloj de 2 s que
  reafirmaba el topmost costaba 12 de 39 ms/s. Se cambió por un gancho de
  `EVENT_SYSTEM_FOREGROUND`. Con la franja final, medido en reposo con WoW
  abierto: **6,8 ms/s de CPU (0,06 % del equipo) y 45 MB privados**.
- **Sin las DLL nativas de TraceEvent** (`KernelTraceControl`, `msdia140`):
  probado que los FPS se leen igual sin ellas; el exe liviano quedó en 4 MB.

## Consecuencias

- La regla «poco invasivo» del `CLAUDE.md` manda sobre cualquier función
  nueva, y cada cambio de interfaz se mide con `medir-consumo`.
- Los contadores PDH van siempre por nombre en inglés (`PdhAddEnglishCounter`):
  este Windows está en español.

## Pendiente

- Juegos Vulkan/OpenGL que no presenten por DXGI no muestran FPS (cubrirlos
  exige los eventos de `DxgKrnl`, que es lo que hace PresentMon).
- El `.exe` no está firmado: SmartScreen advierte al abrirlo.
- Una captura de la franja para el README (sin datos del juego a la vista).
- Decidir si entra al portafolio (hub, `Curriculo`): es la primera app de
  escritorio en C#.
- Revisión visual de Jesús en las dos pantallas y en el borde inferior.
