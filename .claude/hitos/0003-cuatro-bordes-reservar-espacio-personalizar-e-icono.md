# 0003 — Cuatro bordes, reservar espacio, «Personalizar» e icono de la marca

- **Fecha:** 2026-11-07
- **Estado:** completado (pendiente de publicar como v0.2.0)
- **Commits:** el commit «cuatro bordes, reserva de espacio…»

## Contexto

Jesús usó la v0.1.0 y pidió cuatro cosas:

1. Que la franja también se pueda poner a la izquierda o a la derecha.
2. Arriba le tapaba los botones de minimizar y cerrar de los programas
   maximizados.
3. Poder personalizar colores y detalle (sugirió el doble clic).
4. Un icono propio: el de su marca «JS · ISO mejora continua» del Curriculo,
   en vez del icono por defecto.

## Qué se hizo

- **Cuatro bordes** (`Franja.Orientar`, `Segmento.Orientar`): arriba y abajo
  una fila; a los costados una columna de 172 DIP con un bloque por
  componente. Al soltar el arrastre se pega al borde más cercano al cursor.
  Probado en los cuatro bordes del notebook: posición y grosor correctos.
- **Reserva de espacio** (`Nativo/BarraDeAplicacion.cs`): la franja se
  registra como barra de aplicación (`SHAppBarMessage`), igual que la barra
  de tareas. Medido: con la franja arriba, el área de trabajo pasó de
  empezar en y=0 a y=38; en cada borde se reservó exactamente su grosor.
  Activada por defecto y desactivable.
- **«Personalizar»** (`Interfaz/Ajustes.xaml`): borde, reserva, tamaño
  (80–160 %), color de fondo y de texto (muestras o `#RRGGBB`), colores de
  aviso (semáforo o juego para daltonismo), opacidad, qué mostrar, modo
  mínimo y umbrales con deslizadores. Cambios en vivo; se guarda al cerrar.
  Se abre con doble clic, desde la bandeja o con `--personalizar`.
- **Colores dinámicos** (`Paleta.Aplicar`): los pinceles salen de la
  configuración y los XAML los leen como `DynamicResource`.
- **Icono**: `Recursos/vigia.svg` (isotipo de la marca sobre baldosa azul
  marino, colores de la variante oscura) → `vigia.ico` de 16 a 256 px. Va en
  el `.exe`, en «Personalizar» y en la bandeja, donde el visto del logo se
  repinta del color del estado (`Bandeja.PintarVisto`).
- Bandeja simplificada: «Personalizar…» reemplaza a opacidad, modo mínimo y
  «Editar umbrales…».

## Decisiones y alternativas descartadas

- **Barra de aplicación y no auto-ocultar ni «dejar pasar clics».** «Fijar y
  dejar pasar los clics» ya existía y técnicamente deja hacer clic debajo,
  pero a ciegas: los botones siguen tapados. Ocultarla al acercar el mouse
  la haría desaparecer justo cuando se quiere mirar. Reservar el espacio es
  lo que hace la barra de tareas y lo que cualquiera espera. Un juego sin
  bordes ignora el área de trabajo, así que encima del juego se sigue
  viendo.
- **Doble clic → «Personalizar»; el modo mínimo pasa a esa ventana.** El
  doble clic antes alternaba el modo mínimo; Jesús pidió el doble clic para
  personalizar y el modo mínimo es una opción más.
- **Ventana de ajustes `Topmost` y en la pantalla de la franja.** Probado:
  abierta como ventana normal quedó detrás de WoW, porque Windows no deja
  que un proceso en segundo plano se ponga delante.
- **Tema Fluent con fondo sólido.** El tema de Windows 11 da controles
  nativos sin estilos propios, pero su fondo Mica necesita render por GPU;
  con el render por software de Vigía la ventana quedaba semitransparente.
- **Icono: el visto cambia de color, sin punto encima.** La primera versión
  ponía un punto de estado en la esquina y tapaba justo el visto, que es lo
  que identifica el logo. Se repintan sus píxeles por la diferencia
  verde − rojo, que separa el turquesa del visto del blanco de las letras.
- **Baldosa azul marino con el logo claro** y no el logo oscuro sobre
  transparente: el oscuro desaparece en una barra de tareas oscura.

## Consecuencias

- La franja reserva espacio: si Vigía muere a la fuerza, Windows puede dejar
  el hueco hasta que el Explorador lo note. El cierre normal y el manejador
  de error fatal lo liberan.
- La skill `instalar-local` ahora restaura con candado y publica con
  `--no-restore`: el `publish` con restauración propia ensuciaba
  `packages.lock.json` (pasó dos veces este día).
- Consumo en reposo tras los cambios: ~8,5 ms/s de CPU y 44 MB. Mover los
  deslizadores de «Personalizar» sube el consumo mientras se mueven (redibuja
  la franja en vivo); es esperado.

## Pendiente

- La revisión visual de Jesús: la parte de abajo de «Personalizar»
  (umbrales) no se pudo capturar sin abrirle ventanas encima del juego.
- Publicar como v0.2.0 y actualizar la ficha del Curriculo («arriba o abajo»
  → cualquier borde, captura nueva si cambia el aspecto).
