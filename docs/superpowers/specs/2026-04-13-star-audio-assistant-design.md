# Star Audio Assistant Design Spec

## Scope
Build a lightweight Windows audio assistant that plays selected MP3 files based on weekly schedules.

## Confirmed Requirements
- Playback works when screen is locked (same user session still alive).
- Scheduler policy: new schedule starts must preempt current playback immediately.
- Playback backend: Windows system audio interface (not external player process).
- Transition mode: fade-out/fade-in.
- Missed-start behavior: skip if app was not active at start boundary.
- Config UX: visual tray app + task table editor.
- Installation can require admin if needed later.

## Architecture
- `StarAudioAssistant.App` (WPF tray host + main task table UI)
- `StarAudioAssistant.Core` (schedule model, next-boundary calculator, conflict rules)
- `StarAudioAssistant.Audio` (playback engine abstraction, fade state machine)
- `StarAudioAssistant.Infrastructure` (config persistence, logging, startup integration)

## Scheduling Rules
- Weekly window uses start day/time and end day/time.
- Cross-day windows are valid (for example Tue 23:00 -> Wed 05:00).
- Rule priority decides preemption winner; equal priority falls back to list order.
- Scheduler only starts playback on exact start boundary; late app wake-up does not backfill.

## V1 Deliverables
1. Git-initialized repository with solution/project layout.
2. Core scheduler engine with tests for next trigger calculation.
3. WPF shell UI with task table and status bar skeleton.
4. JSON config schema and sample config.
5. Build/test pipeline commands documented in README.
