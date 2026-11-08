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

2. Publicar directo a la carpeta de instalación:

   ```powershell
   dotnet publish src/Vigia -c Release -r win-x64 --self-contained false `
     -p:PublishSingleFile=true -p:DebugType=none `
     -o "$env:LOCALAPPDATA\Programs\Vigia"
   ```

3. Abrirlo y comprobar que sigue vivo a los 10 s:

   ```powershell
   $p = Start-Process "$env:LOCALAPPDATA\Programs\Vigia\Vigia.exe" -PassThru
   Start-Sleep 10; -not $p.HasExited
   ```

4. Decirle a Jesús qué cambió para que lo mire él: la revisión visual es suya.

La configuración (`%APPDATA%\Vigia\config.json`) no se toca al reinstalar.
