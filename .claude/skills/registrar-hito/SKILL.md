---
name: registrar-hito
description: Dejar constancia en .claude/hitos/ de un cambio relevante de Vigía. Úsala al terminar un trabajo con sustancia (versión publicada, sensor nuevo, cambio de interfaz, decisión técnica con alternativas descartadas) o cuando Jesús pida "registra esto", "anota el hito" o pregunte "¿en qué estábamos?".
---

# Registrar un hito

Los hitos son la memoria: **qué** se hizo, **cuándo** y sobre todo **por qué**.
El `CLAUDE.md` dice cómo se trabaja hoy; los hitos, cómo se llegó hasta aquí.
Un hito no se edita ni se borra: si una decisión cambia, se escribe otro que
referencie al anterior.

## Cuándo sí

- Una versión publicada en Releases.
- Un sensor o fuente de datos nueva, o cambiar cómo se lee una.
- Cambios de interfaz con alternativas descartadas.
- Cualquier cosa que toque la regla «poco invasivo» del `CLAUDE.md`.

## Cuándo no

Typos, un color, un texto. Lo que el `git log` ya cuenta igual de bien.

## Cómo

1. **Número**: `ls .claude/hitos/` y el siguiente correlativo de cuatro
   dígitos. Nombre en minúsculas, sin acentos: `0002-slug-en-kebab.md`.
2. **Escribir** con estas secciones, todas obligatorias («Ninguna» si no
   aplica):

```markdown
# NNNN — Título corto y concreto

- **Fecha:** YYYY-MM-DD
- **Estado:** completado | en curso | revertido
- **Commits:** `hash` (o "pendiente de commit")

## Contexto
## Qué se hizo
## Decisiones y alternativas descartadas
## Consecuencias
## Pendiente
```

   En español, en pasado, con fechas absolutas. Si hay mediciones (CPU,
   memoria), van con el número: «bajó de 39 a 27 ms/s», no «bajó bastante».

3. **Índice**: una línea arriba de la tabla de `.claude/hitos/README.md`, con
   el nombre del archivo copiado exacto.
4. **¿Cambió cómo se trabaja?** Actualiza `CLAUDE.md`. ¿Cambió un
   procedimiento? Actualiza su skill.
