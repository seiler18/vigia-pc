---
name: instalar-local
description: Instalar o actualizar Vigía en este PC desde el código (%LOCALAPPDATA%\Programs\Vigia). Úsala cuando Jesús pida "instálalo", "actualiza Vigía", "déjalo para usarlo", o después de un cambio que quiera probar jugando.
---

# Instalar Vigía en este PC

Se instala la versión liviana (4 MB, usa el runtime de .NET 10 que ya está
instalado con el SDK) en una ruta fija. La ruta fija importa: «Iniciar con
Windows» guarda la ruta del `.exe` en el registro, y si apunta a `bin\` se
rompe con cada `dotnet clean`.

## Pasos

1. Cerrar la instancia que esté corriendo (si no, el `.exe` está bloqueado):

   ```powershell
   Get-Process Vigia -ErrorAction SilentlyContinue | Stop-Process -Force
   ```

2. Restaurar con el candado y publicar **sin** restaurar de nuevo, directo a
   la carpeta de instalación. El orden importa: un `publish` que restaura por
   su cuenta (con `PublishSingleFile`) mete `Microsoft.NET.ILLink.Tasks` en
   `packages.lock.json`, y ese candado rompe CI con `NU1004` (pasó dos veces
   el 2026-11-07). Es lo mismo que hace `publicar.yml`.

   ```powershell
   dotnet restore src/Vigia --locked-mode
   dotnet publish src/Vigia -c Release -r win-x64 --self-contained false --no-restore `
     -p:PublishSingleFile=true -p:DebugType=none `
     -o "$env:LOCALAPPDATA\Programs\Vigia"
   ```

   Si aun así `git status` muestra `packages.lock.json` cambiado sin haber
   tocado paquetes: `git checkout -- src/Vigia/packages.lock.json`.

3. Abrirlo y comprobar que sigue vivo a los 10 s:

   ```powershell
   $p = Start-Process "$env:LOCALAPPDATA\Programs\Vigia\Vigia.exe" -PassThru
   Start-Sleep 10; -not $p.HasExited
   ```

4. Decirle a Jesús qué cambió para que lo mire él: la revisión visual es suya.

La configuración (`%APPDATA%\Vigia\config.json`) no se toca al reinstalar.
