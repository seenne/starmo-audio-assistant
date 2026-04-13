# Star Audio Assistant

Windows audio assistant for scheduled MP3 playback with reliable weekly rules, lock-screen support (same logged-in session), and smooth fade transitions.

## Current Architecture
- `src/StarAudioAssistant.App`: WPF host UI and tray shell.
- `src/StarAudioAssistant.Core`: schedule models and trigger calculations.
- `src/StarAudioAssistant.Audio`: playback abstractions and transition control.
- `src/StarAudioAssistant.Infrastructure`: persistence/logging/system integrations.
- `tests/StarAudioAssistant.Core.Tests`: scheduler tests.

## Local Build Commands
Use the bundled SDK in this repository:

```powershell
$dotnet = '.\\.dotnet\\dotnet.exe'
& $dotnet --info
& $dotnet test
& $dotnet build StarAudioAssistant.sln -c Debug
& $dotnet run --project src/StarAudioAssistant.App/StarAudioAssistant.App.csproj
```

## Product Rules (Confirmed)
- New task start preempts currently playing task immediately.
- Missed start boundaries are skipped (no catch-up playback).
- System audio API playback (no external player process dependency).
- Fade-out/fade-in transitions.

## What Is Implemented In MVP
- WPF main window with task table, toolbar actions, conflict hint, and runtime status bar.
- Task add/edit dialog with file picker and field validation.
- Tray behavior: minimize-to-tray, restore on double-click, tray menu stop playback/exit.
- JSON config persistence with auto-load/save.
- Scheduler loop with:
  - weekly/cross-day start and end boundaries
  - immediate preemption on start conflict
  - skip missed starts after long sleep/wake gaps
- NAudio-based MP3 looping playback with fade-out/fade-in.

## Runtime Notes
- Config file location: `%AppData%\\StarAudioAssistant\\config.json`
- Lock-screen playback is supported while the same user session remains active.
- If configured audio paths do not exist, scheduler status will show an error until paths are fixed.
