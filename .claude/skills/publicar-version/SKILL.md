---
name: publicar-version
description: Publicar una versión nueva de Vigía en GitHub Releases. Úsala cuando Jesús pida "publica la versión", "saca una release" o "sube el exe".
---

# Publicar una versión

El workflow `.github/workflows/publicar.yml` se dispara con un tag `vX.Y.Z`:
compila en Windows, genera `Vigia.exe` autónomo (≈ 68 MB, no requiere .NET
instalado), su `Vigia.exe.sha256` y una atestación de procedencia, y crea la
release con los dos archivos.

## Pasos

1. Rama `main` limpia y con el build en verde:

   ```powershell
   git status
   dotnet build Vigia.slnx -c Release
   ```

2. Subir `<Version>` en `src/Vigia/Vigia.csproj` (semver: parche para
   arreglos, menor para algo nuevo visible).
3. Commit en español, una línea, sin atribución a Claude:
   `git commit -am "versión 0.2.0: <qué trae>"`.
4. Tag y push:

   ```powershell
   git tag v0.2.0
   git push origin main --tags
   ```

5. Seguir el workflow hasta que termine y comprobar el archivo:

   ```powershell
   gh run list --workflow publicar.yml --limit 1
   gh release view v0.2.0
   ```

6. Comprobar lo publicado, descargándolo como lo haría cualquiera:

   ```powershell
   gh release download v0.2.0 -R seiler18/vigia-pc -D $env:TEMP\vigia-check
   gh attestation verify $env:TEMP\vigia-check\Vigia.exe -R seiler18/vigia-pc
   ```

7. Registrar el hito (skill `registrar-hito`).

El número del tag debe coincidir con `<Version>`: el workflow pasa el del tag
a `dotnet publish`, y si no coinciden, el «Acerca de» del `.exe` y la release
dicen cosas distintas.
