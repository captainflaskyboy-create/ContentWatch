# Architecture

- **TimerEngine** uses an injectable monotonic clock and stores elapsed/remaining seconds independently of rendering. A completed countdown holds at zero; optional overtime uses a negative remaining position. Legacy snapshot fields remain readable for migration.
- **TimerSessions** owns separate countdown and stopwatch engines. Switching modes pauses the outgoing engine. It tracks whether a countdown has started so editing a fresh countdown changes its Reset baseline, while later adjustments preserve that baseline.
- **Checkpoints** are explicit paused snapshots with timestamps, one per mode. The persisted document also has separate recovery snapshots. Loading chooses each mode's checkpoint first, falling back to its recovery snapshot. Reset and settings changes never replace explicit checkpoints.
- **SessionStore** writes a temporary JSON file and atomically replaces `session.json`. Commands, settings changes and clean shutdown save; a background worker saves recovery data once a second. Old single-session settings migrate with compatible font/colour/format preferences. Removed effects, titles and backgrounds are ignored.
- **OverlayRenderer** draws only time digits into a transparent 1280×360 Skia bitmap. Numeric cell widths remain fixed. The same PNG/BGRA frame goes to the app's clock, OBS preview, browser source and real Spout2 sender. Each mode has its own time-format preferences and shares font, size, colour, outline and shadow.
- **MainWindow** provides four pill navigation buttons and separate Timer, Stopwatch, OBS and Settings views. The application-only `ProgressRing` is never rendered into OBS. Editing time opens a small paused dialog. Navigating to OBS/Settings does not change timer state.
- **TimerApplication** serialises commands, mode changes and persistence with a state lock. Its dedicated rendering thread runs independently of window visibility. UI refreshes do not advance time or transmit frames.
- **BrowserOutput** serves read-only loopback endpoints. Each WebSocket client acknowledges its last decoded PNG; slow clients cannot build an unbounded queue. Reconnect sends the latest current frame without resetting the timer.
- **SpoutOutput** uses the native bridge over the pinned official SpoutDX SDK. A single worker creates, sends and destroys the DirectX context. Optional receiver exports support real GPU integration tests.
- **GlobalShortcuts** uses Windows RegisterHotKey or SharpHook's native macOS/Linux backends. It acts on the selected timer even while minimised.

For isolated manual tests set `CONTENTWATCH_DATA_DIR` to a temporary profile directory before launching. Give its settings a separate browser port, sender name and shortcuts when another instance is running.
