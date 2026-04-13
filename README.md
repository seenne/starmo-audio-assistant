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
