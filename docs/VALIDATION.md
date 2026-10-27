# Validation record

Updated for the simplified Timer / Stopwatch / OBS / Settings interface on 2026-10-27. Host: Windows x64, .NET SDK 10.0.100, MSVC 19.44, DirectX 11.

## Current automated checks

- Release suite: **27 passed, 0 failed**, including the opt-in actual Windows GPU integration test (`CONTENTWATCH_TEST_SPOUT=1`). Standard CI explicitly skips that hardware test and runs the other 26.
- Fake-clock tests cover monotonic timing independent of rendering, repeated pause/resume, durations above 24 hours, countdown completion/hold, overtime, reset, adjustments and paused restoration.
- Session tests cover default five-minute Timer and zero Stopwatch, independent state, pausing on mode switches, repeated selection without pausing, editing/reset baselines and clamping adjustments at zero.
- Explicit checkpoints pause and survive resets, edits, recovery saves and time spent closed. Tests verify replacement only by another explicit checkpoint and recovery fallback only when a mode has no checkpoint.
- Legacy JSON migration preserves font/colour/format settings while ignoring removed effects, titles, pause labels and backgrounds. Pixel tests verify transparent space outside the time digits and no animation-dependent output.
- Loopback WebSocket tests compare exact PNG bytes, reconnect to the current timer, handle slow clients without blocking publication, and report port conflicts. A restart/reconnect test verifies checkpoint precedence over newer recovery data and switching the OBS output between modes.
- The actual Spout GPU integration test compares received BGRA bytes exactly with the shared renderer after pause, adjustment, reset, restore, resume, countdown completion and overtime. It uses a real unique native sender and receiver, not mocks.
- Background-worker tests run without an Avalonia window and verify advancing time, identical paused frames, adjustment publication and automatic recovery restoration.

## Single-file Windows build

- Published a self-contained Windows x64 executable named `ContentWatch.exe`, with compressed managed assemblies and native libraries bundled for automatic extraction. No trimming is used.
- Re-ran all 27 release tests after renaming the assembly; all passed, including real GPU readback.
- Copied only `ContentWatch.exe` into an otherwise empty folder, launched it with an isolated profile and a fresh native-extraction cache, and verified browser output became ready.
- The actual Spout receiver received 89 frames from this single-file process, with transparent and visible pixels. Loaded-module inspection confirmed Skia and the Spout bridge came from the fresh bundle cache, not loose files beside the executable.
- The publish output contains only the executable, approximately 60 MB. Runtime configuration includes the self-contained .NET and ASP.NET Core frameworks. License texts are embedded and available through Settings → Third-party notices.

## Current Windows UI and output checks

- Launched an isolated profile using `CONTENTWATCH_DATA_DIR`, port 17846 and sender `ContentWatch.UI Test`, leaving the existing profile untouched.
- Visually inspected the compact Timer, Stopwatch, manual-time editor, Settings and OBS screens. Corrected time-button hover fill and cramped numeric editors found during inspection.
- Verified Play changes to Pause, the app-only progress ring appears, Checkpoint pauses and shows a saved timestamp, and switching modes preserves independent times.
- Saved a checkpoint at approximately 4:55, reset to 5:00, closed and restarted the app: it restored the saved 4:55 checkpoint paused, rather than the later Reset state.
- Opening the OBS screen left the Timer running. Its preview contained only time digits.
- `SpoutInspect.exe 'ContentWatch.UI Test'` received 89 real app frames with transparent and visible pixels. Browser health/frame checks and native reception were also exercised with the control window minimised.

## Earlier integration checks retained

The initial implementation was tested with OBS Studio 31.0.3 and the installed Spout2 plugin: both Browser and Spout2 Capture properties displayed the same paused timer. Existing OBS source settings were restored. The native SDK smoke test verified 27/30 frames exactly after startup readback latency, including BGRA channels, vertical orientation and half alpha. The simplified revision retains those output integrations; current tests additionally compare its new time-only renderer through the real GPU bridge.

## Limits

- macOS/Linux interactive UI, keyboard-hook permissions and audio remain untested locally. Native CI builds/tests are configured; check GitHub Actions results separately.
- No full multi-scene OBS recording, long recording/load test, sleep/hibernation test or recorded end-sound test was performed for this revision. Reconnect behaviour has automated coverage.
- Packages are unsigned. macOS builds are raw executables rather than notarised app bundles. Fonts may fall back when absent.
- Automatic recovery may lose up to the last one-second interval on forced termination; explicit checkpoints are independent. File-write failures are reported, and brief Windows file-replacement locks are retried.
