# 0002 — Auditoría de seguridad y publicación de la v0.1.0

- **Fecha:** 2026-11-07
- **Estado:** completado
- **Commits:** el commit «auditoría de seguridad…» y el tag `v0.1.0`

## Contexto

Antes de publicar la primera versión, Jesús pidió «una auditoría que todo
esté bien, siempre teniendo cuidado de cualquier elemento de seguridad». El
repo ya era público (hito 0001) y el `.exe` iba a ir a Releases para que lo
descargue cualquiera desde el portafolio.

## Qué se hizo

**Revisado y sin hallazgos:**

- `dotnet list package --vulnerable --include-transitive`: ningún paquete
  vulnerable. TraceEvent 3.2.8 tiene una 4.0.0 mayor; queda para Dependabot.
- Historial de git sin credenciales ni claves. Secret scanning y protección
  de push activos (vienen por defecto en repos públicos); alertas y arreglos
  automáticos de Dependabot activados al crear el repo.
- Workflows: acciones fijadas por SHA (comprobados contra GitHub, no de
  memoria), `persist-credentials: false`, permisos mínimos, el nombre del tag
  entra por variable de entorno y no interpolado en el script (evita
  inyección de comandos).
- Manifest `asInvoker`: nunca pide administrador. La app no abre conexiones
  de red. La clave `Run` del registro guarda la ruta entre comillas.

**Corregido:**

- `config.json` editado mal (umbral `null`, umbrales invertidos, opacidad 5,
  borde desconocido) tumbaba la app: se evalúa cada segundo. Ahora se
  normaliza al cargar (`Configuracion.Normalizada`). Probado con un JSON roto
  a propósito: siguió viva y reescribió los valores por defecto.
- Una excepción no controlada cerraba la franja en silencio. Ahora van a
  `%APPDATA%\Vigia\errores.log` (`Registro.cs`, recortado a 256 KB) y la app
  sigue; `Guardar()` ya no lanza.
- Al salir se liberaban los sensores con una lectura nativa en curso; ahora
  `Recolector.Dispose` espera hasta 2 s a que termine.
- `Process.GetProcessById` se llamaba cada segundo sin `Dispose`: un handle de
  proceso por segundo esperando al GC. Medido después: 607 → 605 handles en
  20 s, estable.
- Dependencias con candado: `packages.lock.json` versionado y CI con
  `--locked-mode` (incluye los paquetes de runtime de `win-x64`).
- `publicar.yml` en dos jobs: el que compila (ejecuta código de las
  dependencias) no puede escribir en el repo; el que publica no ejecuta nada
  del repo. La release lleva `Vigia.exe.sha256` y una atestación de
  procedencia (`actions/attest-build-provenance`).
- `verificar-pr.yml` corre también en cada push a `main`.

## Decisiones y alternativas descartadas

- **Atestación en vez de firma de código.** Un certificado de firma cuesta
  cada año y exige validación de identidad; para un proyecto de portafolio no
  compensa. La atestación es gratis en un repo público y prueba lo que
  importa: que el `.exe` salió de este código, compilado por GitHub. No quita
  el aviso de SmartScreen; eso solo lo hace una firma con reputación.
- **No se lanzó `/thermos`** (dos revisores en paralelo): la skill solo la
  lanza Jesús a mano. La revisión fue manual, archivo por archivo, más las
  pruebas de arriba.
- Las acciones de artefactos se escribieron primero en v4 de memoria; al
  comprobar los SHA contra GitHub se vio que la vigente era v7/v8 y se
  cambiaron. Por eso la regla: un SHA de acción se consulta, no se recuerda.

## Consecuencias

- Un cambio de paquete NuGet va acompañado de su `packages.lock.json`, o CI
  falla.
- Matar Vigía a la fuerza deja la sesión ETW `Vigia-FPS` viva hasta el
  próximo arranque o reinicio (documentado en `CLAUDE.md`).
- Consumo tras los cambios, con WoW en primer plano: 4,7–5,5 ms/s de CPU y
  45 MB privados.

## Pendiente

- Firma de código, si algún día se distribuye más allá del portafolio.
- TraceEvent 4.0.0 (mayor): probar los FPS antes de aceptar el PR.
