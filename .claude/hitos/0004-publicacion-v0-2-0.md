# 0004 — Publicación de la v0.2.0

- **Fecha:** 2026-11-07
- **Estado:** completado
- **Commits:** `062a3ff` (código, hito 0003), tag `v0.2.0`

## Contexto

Jesús probó lo del hito 0003 (cuatro bordes, reserva de espacio,
«Personalizar», icono de la marca) en su equipo y lo dio por bueno: «ya lo
encuentro perfecto, publícalo».

## Qué se hizo

- Tag `v0.2.0` → `publicar.yml` en verde (compilar y publicar).
- Comprobado como lo haría un cliente: el enlace del portafolio
  (`releases/latest/download/Vigia.exe`) redirige a la v0.2.0, el hash
  coincide con `Vigia.exe.sha256` y la atestación apunta al tag `v0.2.0` y al
  commit `062a3ff`. `.exe` autónomo de 68,3 MB.
- Ficha del Curriculo (`projects/Vigia/index.html`) y tarjeta del hub
  actualizadas: «cualquier borde», reserva de espacio y personalizar. Las dos
  verificadas en producción.
- Copia local reinstalada con la skill `instalar-local` (ya restaura con
  candado): `packages.lock.json` sin cambios.

## Decisiones y alternativas descartadas

- **Publicar solo tras la prueba de Jesús.** El código estaba en `main` con
  CI verde desde el hito 0003, pero la parte de abajo de «Personalizar» no se
  había podido ver sin abrirle ventanas encima del juego; se esperó su visto
  bueno en vez de dar la interfaz por buena.
- **La captura de la ficha no se cambió.** La franja horizontal se ve igual
  que en la v0.1.0; la columna lateral y «Personalizar» se describen en el
  texto.

## Consecuencias

Ninguna nueva en la forma de trabajar.

## Pendiente

- Dos PR de Dependabot abiertos desde el 2026-11-08: TraceEvent 4.0.0
  (mayor: probar que los FPS se sigan leyendo antes de fusionar) y
  `actions/setup-dotnet` 6.1.0.
- Firma de código (SmartScreen sigue advirtiendo).
