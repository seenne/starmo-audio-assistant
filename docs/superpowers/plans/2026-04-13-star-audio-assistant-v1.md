# Star Audio Assistant V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a git-based .NET repository for Star Audio Assistant and deliver the first tested scheduler slice plus WPF app skeleton.

**Architecture:** Use a multi-project .NET solution separating UI host, scheduler core, audio domain, and infrastructure. Implement the scheduler boundary logic first with TDD so playback orchestration can build on deterministic trigger results.

**Tech Stack:** .NET 8 (local SDK), WPF, xUnit, FluentAssertions (tests), JSON config

---

### Task 1: Repository And Baseline Docs

**Files:**
- Create: `README.md`
- Create: `.gitignore`
- Create: `docs/superpowers/specs/2026-04-13-star-audio-assistant-design.md`
- Create: `docs/superpowers/plans/2026-04-13-star-audio-assistant-v1.md`

- [ ] Step 1: Initialize git repository and baseline metadata files.
- [ ] Step 2: Add project intent, local build commands, and architecture summary to README.
- [ ] Step 3: Ensure tooling artifacts (`.dotnet/`, `bin/`, `obj/`) are ignored.

### Task 2: Solution And Project Scaffolding

**Files:**
- Create: `StarAudioAssistant.sln`
- Create: `src/StarAudioAssistant.App/*`
- Create: `src/StarAudioAssistant.Core/*`
- Create: `src/StarAudioAssistant.Audio/*`
- Create: `src/StarAudioAssistant.Infrastructure/*`
- Create: `tests/StarAudioAssistant.Core.Tests/*`

- [ ] Step 1: Create solution and all projects.
- [ ] Step 2: Wire project references according to architecture boundaries.
- [ ] Step 3: Ensure solution builds before feature implementation.

### Task 3: TDD Scheduler Next Trigger

**Files:**
- Create: `src/StarAudioAssistant.Core/Scheduling/ScheduleRule.cs`
- Create: `src/StarAudioAssistant.Core/Scheduling/ScheduleCalculator.cs`
- Create: `tests/StarAudioAssistant.Core.Tests/Scheduling/ScheduleCalculatorTests.cs`

- [ ] Step 1: Write failing tests for weekly and cross-day start trigger calculation.
- [ ] Step 2: Run tests and verify failing assertions.
- [ ] Step 3: Implement minimal calculator logic to satisfy tests.
- [ ] Step 4: Re-run tests and confirm all pass.

### Task 4: WPF Shell Skeleton

**Files:**
- Modify: `src/StarAudioAssistant.App/MainWindow.xaml`
- Modify: `src/StarAudioAssistant.App/MainWindow.xaml.cs`

- [ ] Step 1: Create main layout matching approved hand-drawn direction (header, toolbar, task table, status bar).
- [ ] Step 2: Bind sample rows from in-memory model.
- [ ] Step 3: Confirm app project builds.

### Task 5: Verification

**Files:**
- Modify: `README.md`

- [ ] Step 1: Run targeted tests for scheduler component.
- [ ] Step 2: Run full solution build.
- [ ] Step 3: Document exact commands and expected outputs in README.
