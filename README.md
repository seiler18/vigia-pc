# Vigía

Una franja delgada pegada a un borde de la pantalla, con la temperatura de la
GPU y del CPU, los FPS del juego y el uso de RAM. Sirve para vigilar el equipo
mientras se juega y ver venir el sobrecalentamiento antes de que el equipo
empiece a bajar su propio rendimiento.

```
● GPU y CPU calientes    FPS 43  1 % bajo 31 · Wow    GPU 86° ╌╌▁▂▃ 91 %  máx 87° · VRAM 3,4/3,9 GB    CPU 94° ╌▂▃▅ 32 %  máx 97° · 3,8 GHz    RAM 51 %  16,0 / 31 GB
```

## Qué muestra

| | Detalle |
|---|---|
| **Estado** | Una frase y un color: verde, ámbar o rojo según los umbrales. Es lo que se alcanza a leer de reojo |
| **FPS** | Cuadros por segundo del juego en primer plano, el **1 % bajo** (los tirones que el promedio esconde) y el nombre del proceso |
| **GPU** | Temperatura, tendencia de los últimos 2 minutos, carga, máxima de la sesión y VRAM |
| **CPU** | Temperatura, tendencia, carga, máxima de la sesión y frecuencia |
| **RAM** | Porcentaje usado y GB |

Avisa también si el juego corre en la gráfica **integrada** en vez de la
dedicada, un error común en notebooks que se nota como FPS bajos sin motivo.

## Poco invasivo, a propósito

- **No se inyecta en el juego.** Los FPS se cuentan con los eventos que
  Windows ya emite al presentar cada cuadro (ETW, el método de PresentMon).
  Ningún anti-cheat lo ve.
- **No instala drivers ni pide administrador.** La temperatura de la GPU sale
  de la misma interfaz que usa el Administrador de tareas, y la del CPU, de la
  zona térmica ACPI.
- **No le roba el foco al juego.** Arrastrarla no minimiza el juego ni
  suelta el mouse.
- **No tapa otras ventanas.** Reserva su espacio como la barra de tareas: una
  ventana maximizada termina donde empieza la franja y sus botones de
  minimizar y cerrar quedan a la vista.
- **No compite por recursos.** Dibuja con la CPU (no toca la GPU del juego),
  corre con prioridad baja y en modo de eficiencia de Windows. Medido en un
  Ryzen 5 4600H con un juego abierto: menos del 0,1 % de CPU y unos 45 MB
  de RAM.

## Uso

- **Arrastrar**: al soltarla se pega al borde más cercano de esa pantalla.
  Arriba y abajo es una fila; a la izquierda y a la derecha, una columna con
  un bloque por componente.
- **Doble clic**: abre **Personalizar**, donde se elige:
  - borde, tamaño y si reserva el espacio;
  - color de fondo y de texto (muestras o cualquier `#RRGGBB`), opacidad, y
    colores de aviso: verde · ámbar · rojo, o azul · naranja · rosa para
    daltonismo;
  - qué mostrar (FPS, GPU, CPU, RAM, tendencias) o el modo mínimo;
  - los umbrales de atención y crítico.

  Los cambios se ven al instante en la franja.
- **Ctrl+Shift+F11**: mostrar u ocultar desde dentro del juego.
- **Icono de la bandeja**: el logo con el visto pintado del color del estado.
  Clic derecho: personalizar, fijar la franja para que deje pasar los clics,
  volver a la pantalla principal e iniciar con Windows.
- `Vigia.exe --personalizar` abre directo en Personalizar (útil para un
  acceso directo).

Umbrales por defecto:

| | Atención | Crítico |
|---|---|---|
| GPU | 80 °C | 87 °C |
| CPU | 85 °C | 95 °C |
| RAM | 85 % | 95 % |

Al entrar en crítico, Windows muestra un aviso (uno cada 5 minutos por
motivo). La configuración vive en `%APPDATA%\Vigia\config.json`; si se edita
a mano, se aplica al guardar.

## Requisitos

- Windows 10 u 11 de 64 bits.
- Para los FPS: ser administrador **o** estar en el grupo «Usuarios del
  registro de rendimiento» (`Performance Log Users`). Sin eso, Vigía funciona
  igual y la casilla de FPS lo dice.
- Los juegos se ven encima de la franja solo en **ventana sin bordes**. En
  pantalla completa exclusiva el juego la tapa: ahí conviene dejarla en otra
  pantalla.

## Instalar

Descarga `Vigia.exe` de [Releases](../../releases) y ábrelo; no necesita nada
más ni se instala: es un solo archivo. Para quitarlo, se cierra desde la
bandeja y se borra el archivo (y `%APPDATA%\Vigia` si se quiere borrar la
configuración).

### Comprobar la descarga

El `.exe` no está firmado con un certificado de código, así que Windows
SmartScreen puede advertir al abrirlo («Más información» → «Ejecutar de todas
formas»). En su lugar, cada versión trae dos formas de comprobar que el
archivo es el que compiló GitHub desde este repositorio y que nadie lo
cambió:

```powershell
# 1. El hash coincide con el publicado en la release
(Get-FileHash .\Vigia.exe -Algorithm SHA256).Hash   # comparar con Vigia.exe.sha256

# 2. La atestación de procedencia (requiere la CLI de GitHub)
gh attestation verify .\Vigia.exe -R seiler18/vigia-pc
```

## Privacidad

Vigía no se conecta a internet, no envía datos y no guarda historial: lo que
lee se queda en la memoria mientras está abierto. En disco solo escribe su
configuración y, si algo falla, un registro de errores, ambos en
`%APPDATA%\Vigia\`.

## Límites conocidos

- La temperatura del CPU es la de la zona ACPI que informa el firmware. En
  notebooks sigue de cerca al sensor del procesador, pero se actualiza más
  lento y en algunos equipos de escritorio no existe.
- Los juegos Vulkan u OpenGL que no presentan por DXGI no muestran FPS. Con
  NVIDIA, la opción «Vulkan/OpenGL present method» en automático sí presenta
  por DXGI.

## Compilar

Requiere el SDK de .NET 10.

```powershell
dotnet build Vigia.slnx -c Release
dotnet run --project src/Vigia
```
