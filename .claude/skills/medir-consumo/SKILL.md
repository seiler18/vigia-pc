---
name: medir-consumo
description: Medir cuánta CPU y memoria usa Vigía, para comprobar que sigue siendo poco invasivo. Úsala después de cualquier cambio en la interfaz o en los sensores, y cuando Jesús pregunte "¿cuánto consume?".
---

# Medir el consumo

La regla del proyecto es no competir con el juego (ver `CLAUDE.md`). Un
cambio que se ve inocente —un reloj, una animación, rehacer un gráfico— puede
multiplicar el consumo: el reloj que reafirmaba el «siempre encima» cada 2 s
costaba un tercio de toda la CPU de Vigía.

## Referencia

2026-11-07, Ryzen 5 4600H, WoW abierto, franja en modo detallado:
**6,8 ms de CPU por segundo (0,06 % del equipo) y 45 MB privados.**

Si un cambio pasa de ~10 ms/s, hay que justificarlo o deshacerlo.

## Cómo

Compilar en Release (Debug mide de más) y dejar 15 s de calentamiento para
que el JIT no cuente. **No mover ni tocar la franja durante la medición**:
arrastrarla dispara el consumo y la cifra no sirve.

```powershell
Get-Process Vigia -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build Vigia.slnx -c Release
$p = Start-Process "src\Vigia\bin\Release\net10.0-windows\Vigia.exe" -PassThru
Start-Sleep 15
$p.Refresh(); $a = $p.TotalProcessorTime; Start-Sleep 30; $p.Refresh()
"{0:N1} ms CPU/s · {1:N0} MB privados" -f (($p.TotalProcessorTime - $a).TotalMilliseconds / 30), ($p.PrivateMemorySize64 / 1MB)
```

Para aislar qué parte consume, se compara con el recolector solo, sin
interfaz: un proyecto de consola temporal (fuera del repo) que incluya
`Nativo\*.cs` y `Sensores\*.cs` con `<Compile Include>` y llame a
`Recolector.Iniciar()`. El 2026-11-07 el recolector solo midió 6,8 ms/s, lo
mismo que la app completa: dibujar la franja cuesta casi nada y el gasto está
en leer sensores. Si la app completa sube y el recolector no, el problema
está en la interfaz.
