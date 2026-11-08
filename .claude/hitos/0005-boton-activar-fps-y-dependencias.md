# 0005 — Botón «Activar FPS», TraceEvent 4.0.0 y setup-dotnet 6.1.0

- **Fecha:** 2026-11-07
- **Estado:** completado
- **Commits:** `fd16abe` (TraceEvent), `59d919f` (setup-dotnet) y el commit
  «botón Activar FPS…»; tag `v0.3.0`

## Contexto

Al explicar cómo usaría Vigía un cliente cualquiera salió que, en una
cuenta normal de Windows, los FPS no se ven: la sesión ETW pide ser
administrador o estar en el grupo «Usuarios del registro de rendimiento», y
la única forma documentada era pegar un comando en PowerShell como
administrador. Jesús pidió el botón que lo resuelve y, además, aplicar las
dos actualizaciones que Dependabot había abierto, revisando si otros repos
necesitaban lo mismo.

## Qué se hizo

- `Sensores/PermisoFps.cs`: `Tiene()` mira el token de la sesión;
  `ConcederAsync()` corre `Add-LocalGroupMember -SID S-1-5-32-559` en una
  PowerShell elevada (UAC) y distingue listo, cancelado y fallo.
- Sección «FPS» al principio de «Personalizar», visible solo si falta el
  permiso; la franja dice «sin permiso · doble clic para activarlo».
- PR #2 (TraceEvent 3.2.8 → 4.0.0): compilado sin avisos, las DLL nativas
  siguen fuera, y probado contra WoW abierto: 49–50 FPS y 1 % bajo de
  41–42, igual que con la 3.2.8. Fusionado.
- PR #1 (setup-dotnet 6.0.0 → 6.1.0): su CI había fallado con `NU1004`
  porque Dependabot lo armó sobre el candado viejo (hito 0002); tras pedirle
  rebase pasó. Fusionado.
- Otros repos: ninguno usa setup-dotnet ni TraceEvent, y todos tienen sus
  acciones en la versión vigente (checkout 7.0.1, setup-node 7.1.0,
  setup-java 6.1.0…). Los que no tienen Dependabot (herramientas-dev,
  iso-toolkit, NegociosDigitales, OPCIONES, organizador-iso,
  SkillsToImprove, WebMaker) no tienen dependencias que vigilar; WebMaker
  solo en `plantilla/`, cuyas copias traen su propio Dependabot.

## Decisiones y alternativas descartadas

- **Agregar al grupo y no pedir administrador.** Un manifest
  `requireAdministrator` mostraría el UAC en cada arranque (también al
  iniciar con Windows) y correría todo Vigía con privilegios que no
  necesita. El grupo es el permiso mínimo y se da una vez.
- **El usuario por SID, no por nombre.** Si quien confirma el UAC es otra
  cuenta de administrador, `$env:USERNAME` dentro de la PowerShell elevada
  sería esa cuenta y el permiso quedaría en el usuario equivocado.
- **El grupo por SID (S-1-5-32-559), no por nombre**: el nombre cambia con
  el idioma de Windows.
- **`-EncodedCommand`** en vez del script como texto: ninguna comilla ni
  llave depende de cómo Windows parte los argumentos.
- **«Ya es miembro» por el id del error.** La primera versión usaba
  `catch [Microsoft.PowerShell.Commands.MemberExistsException]`; la
  comprobación de solo lectura mostró que ese tipo no existe en una
  PowerShell recién abierta (aparece al cargar el módulo), y un catch con un
  tipo desconocido rompe el script.
- **Nada se ejecutó para probar el botón.** El sistema de permisos de la
  sesión bloqueó correr `Add-LocalGroupMember` incluso sin elevar, y es lo
  correcto: se verificó la sintaxis con el parser de PowerShell, el ida y
  vuelta del base64 y la existencia del cmdlet y del grupo, todo de solo
  lectura.

## Consecuencias

- En este equipo la sección «FPS» no aparece nunca (el usuario ya está en el
  grupo); para verla hay que probar en otra cuenta de Windows.

## Pendiente

- Probar «Activar FPS» de punta a punta en una cuenta sin el permiso.
