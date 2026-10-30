# EVENTVWR Parser

[Español](README.md) | **English**

Screenshare tool that reads the Windows event logs and shows them all together in one place, so you don't have to open Event Viewer and filter log by log like a maniac

## what it reads

**powershell**
- `Windows PowerShell`, so 400, 600, 800
- `PowerShell/Operational` → 4104 and 4103
- ConsoleHost_history.txt history for every user on the PC (PSReadLine)
- 4688 if the process is powershell

**journal**
- `Application` 3079
- `Ntfs/Operational` 501. If fsutil.exe shows up the type says so, otherwise it's just some normal Windows process (SearchIndexer and the like)

**scheduled tasks** (`TaskScheduler/Operational`)
- Start: 100, 103, 107, 108, 110, 118, 119
- End: 201, 102
- Created/modified/deleted: 106, 140, 141
- It warns you if this log is turned off on the PC, so nobody wastes time staring at an empty list

**other**
- Event log cleared: 104 from System, 1102 from Security
- Installed services: 7045

## fileless

It has a detector that flags sketchy scripts in red. It goes off on things like:
- `Assembly::Load`
- Injection APIs (`VirtualAlloc`, `CreateThread`, etc)
- Several small things together: base64, `-enc`, `-w hidden`, obfuscation, or PowerShell itself logging it as a warning

The bottom panel tells you why it got flagged

Don't be dumb about it, this is really easy to false flag

## how to use it

- Run it as admin (without admin it can't read Security)
- Boot and logon times are at the top. Everything before logon shows up half transparent, if you only want the current instance turn on the switch at the bottom left
- The search box searches everything, you can type `command executed`, `iex`, `prefetch`, whatever
- In IDs you can put several separated by commas `400,600,800` or a range `4100-4104`
- "Hide duplicates" merges the 400/600/800 from a single run and drops PowerShell's internal garbage
- The `ES`/`EN` button at the top right switches language instantly, without rereading anything. It starts in Spanish if Windows is in Spanish, English otherwise. Nothing gets saved on the PC, so you pick it again every time you open it. Whatever comes straight from Windows (event descriptions and such) stays in that PC's language

The logon time comes from when explorer.exe started, so if the guy restarted explorer you'll get a later time. keep that in mind

PSReadLine history commands don't have their own time, the one you see with `~` is the last time the file was modified

## DISCLAIMER

Before anyone says anything, I code with braces on the same line as the statement, so don't bust my balls about it, you're not getting me to do it the way it's done around here, kiddos.
