# EVENTVWR Parser

**Español** | [English](README.en.md)

Tool para ss que lee los eventos de windows y te los muestra todos juntos en un solo lugar, asi no tenes que andar abriendo el visor de eventos y filtrando log por log como un enfermo

## que lee

**powershell**
- `Windows PowerShell` osea 400, 600, 800
- `PowerShell/Operational` → 4104 y 4103
- Historial de ConsoleHost_history.txt de todos los usuarios de la pc (PSReadLine)
- 4688 Si el proceso es powershell

**journal**
- `Application` 3079
- `Ntfs/Operational` 501. Si aparece fsutil.exe lo dice en el tipo, si no es algun proceso de windows normal (SearchIndexer y eso)

**tareas programadas** (`TaskScheduler/Operational`)
- Inicio: 100, 103, 107, 108, 110, 118, 119
- Fin: 201, 102
- Creada/modificada/borrada: 106, 140, 141
- Tira log si está apagado en la pc asi no miran al pedo

**otros**
- Log de eventos borrado 104 de system, 1102 de Security
- Servicios instalados 7045

## fileless

Tiene un detector que flaggea los scripts raris en rojo. Salta con cosas como:
- `Assembly::Load`
- APIs de inyeccion (`VirtualAlloc`, `CreateThread`, etc)
- Varias cosas chicas juntas: base64, `-enc`, `-w hidden`, ofuscacion, o que powershell mismo lo haya marcado como warning

En el panel de abajo te dice por que lo flaggeo

No sean boludos, es muy facil false flaguear esto

## como se usa

- Abrilo como admin (si lo abris sin admin no lee security)
- Arriba tenes la hora de boot y de logon. todo lo de antes del logon aparece medio transparente, si queres ver solo lo de la instancia activa el coso de abajo a la izquierda
- El buscador busca en todo, podes poner `comando ejecutado`, `iex`, `prefetch`, lo que sea
- en IDs podes poner varios separados por coma `400,600,800` o un rango `4100-4104`
- "Ocultar duplicados" junta los 400/600/800 de una misma ejecucion y saca la basura interna de powershell
- El boton `EN`/`ES` de arriba a la derecha cambia el idioma al toque, sin volver a leer nada. Arranca en español si el windows esta en español, si no en ingles. No guarda nada en la pc, asi que lo elegis cada vez que lo abris. Lo que viene directo de windows (las descripciones de los eventos y eso) queda en el idioma de esa pc

La hora de logon la saca de cuando arranco explorer.exe, asi que si el pibe reinicio el explorer te va a dar una hora mas tarde. tenerlo en cuenta

Los comandos del historial de psreadline no tienen hora propia, la que ves con `~` es la ultima vez que se modifico el archivo

## DISCLAIMER

Antes de que digan nada, soy partidario de codear poniendo los corchetes en la misma linea de la instruccion asi que no me rompan las pelotas, no me van a hacerlo como se hace acá, wachines.
