<p align="center">
  <img src="assets/readme/hero.svg" alt="Starmo Audio Assistant Banner" width="100%" />
</p>

<p align="center">
  <a href="https://github.com/seenne/starmo-audio-assistant/releases/latest">
    <img alt="Release" src="https://img.shields.io/github/v/release/seenne/starmo-audio-assistant?label=Release&color=2A5CAA">
  </a>
  <a href="./LICENSE">
    <img alt="License" src="https://img.shields.io/badge/License-MIT-1F8B4C">
  </a>
  <img alt="Platform" src="https://img.shields.io/badge/Platform-Windows%2010%2F11-0A66C2">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8-512BD4">
</p>

<h1 align="center">Starmo Audio Assistant</h1>

<p align="center">
  <a href="#中文介绍">中文</a> ·
  <a href="#english-overview">English</a> ·
  <a href="https://github.com/seenne/starmo-audio-assistant/releases/latest">Download</a>
</p>

## 中文介绍
`Starmo Audio Assistant`（星晨音频助手）是一款面向 Windows 的轻量级定时音频助手。  
它支持按周循环或单次时间段触发播放，支持跨天任务、淡入淡出、托盘后台运行，并优先保证“可预期”和“稳定”。

### 核心能力
- 定时策略：每周循环、单次执行、跨天时间段
- 播放体验：系统音频接口播放（NAudio）、淡入淡出切换
- 冲突处理：新任务到点可抢占当前播放任务
- 运行方式：最小化到托盘、双击托盘恢复、后台持续调度
- 健康检查：音频路径/排程合法性检查与错误中心诊断

### 适用场景
- 早晚固定语音播报
- 夜间白噪音/助眠音频轮播
- 值班提醒、班次提示与时间窗广播

## English Overview
`Starmo Audio Assistant` is a lightweight Windows scheduler for MP3 audio playback.  
It supports weekly and one-time schedules, cross-day windows, smooth fade transitions, tray-first workflow, and reliability-focused orchestration.

### Key Capabilities
- Scheduling: weekly recurring and one-time execution with cross-day windows
- Playback: system audio playback via NAudio with fade in/out
- Conflict handling: newly triggered tasks can preempt active playback
- Runtime model: tray background mode with resilient scheduler loop
- Diagnostics: task health checks and an in-app error center

### Typical Use Cases
- Morning/evening routine voice playback
- Night-time white-noise scheduling
- Shift reminders and timed audio announcements

## Quick Start
Use the bundled local SDK in this repository:

```powershell
$dotnet = '.\\.dotnet\\dotnet.exe'
& $dotnet --info
& $dotnet test
& $dotnet build StarAudioAssistant.sln -c Debug
& $dotnet run --project src/StarAudioAssistant.App/StarAudioAssistant.App.csproj
```

## Portable Package
Each package build generates a brand-new timestamped zip:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\pack-portable.ps1
```

Or double-click:
- `pack-portable.cmd`

Output:
- `dist\packages\StarmoAudioAssistant-portable-win-x64-<timestamp>.zip`

## Architecture
- `src/StarAudioAssistant.App`: WPF desktop UI, tray shell, task editor
- `src/StarAudioAssistant.Core`: schedule rule model and trigger calculator
- `src/StarAudioAssistant.Audio`: playback abstraction and fade controller
- `src/StarAudioAssistant.Infrastructure`: JSON config storage and runtime persistence
- `tests/StarAudioAssistant.Core.Tests`: scheduling and config regression tests

## Runtime Notes
- Config path: `%AppData%\\StarmoAudioAssistant\\config.json`
- Lock-screen playback is supported under the same logged-in Windows session
- Invalid/missing audio files are surfaced in health checks and error center

## License
MIT © 2026 Starmo Audio Assistant Contributors
