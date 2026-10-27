# ContentWatch

A compact desktop timer and stopwatch for recording timed challenges. Built with .NET 10, Avalonia and SkiaSharp, with a transparent OBS browser source and a real Windows Spout2 sender.

## Run on Windows

Extract the whole `ContentWatch-win-x64.zip`, then run `ContentWatch.Desktop.exe`. Keep all accompanying DLLs beside it. The portable package includes .NET and the native Spout2 bridge. The build is unsigned.

The four navigation buttons are **Timer, Stopwatch, OBS, Settings**.

- **Timer** starts at five minutes. Click the time to enter hours, minutes, seconds and milliseconds. Before the countdown starts, editing changes its starting duration. After it starts, adjustments leave the original Reset duration intact. The +30 seconds, +1 minute and +5 minutes buttons provide quick additions; precise positive/negative adjustments live in the time editor. All adjustments pause.
- **Stopwatch** starts at zero and counts upwards without a target or time limit. Click its time to set elapsed time. Reset returns it to zero.
- **Play/Pause** starts, pauses or resumes the selected clock. **Reset** pauses and returns to the starting point. Switching between Timer and Stopwatch pauses the mode you leave and preserves each mode's independent time. OBS follows the selected mode.
- **Checkpoint** pauses and saves the exact current session. Close the app and return whenever you want: the checkpoint restores paused automatically. Press Play when ready.

There is one checkpoint per mode. Only another press of Checkpoint replaces it; Reset, edits, settings changes and automatic recovery saves do not. If a mode has no checkpoint, it restores its latest automatic recovery snapshot paused. Closed time never counts. To start fresh on a later launch, Reset and then save a new Checkpoint.

Settings, checkpoints and recovery data are stored in the platform application-data directory under `ContentWatch/session.json`. Commands, settings changes and exit save immediately; recovery snapshots are saved every second. Unexpected termination can lose up to the last second of unsaved recovery, while an explicit checkpoint stays at the saved point.

Opening OBS or Settings does not pause or switch the output. Minimise the app to focus on your challenge; timing and output continue. Machine sleep/shutdown is not background operation.

## Appearance and behaviour

In Settings change the shared font family, font size, text colour, outline colour/width and shadow. Choose hours and fractions separately for Timer and Stopwatch. Defaults are whole seconds for Timer and hundredths for Stopwatch. Click **Save settings** to apply changes to the app and both OBS outputs.

The progress ring belongs only to the app. OBS always receives **time digits on transparency**, with no background, ring, controls, title, effects or status labels. Countdown completion displays `00:00`. Optional overtime continues upwards as `+MM:SS`; enable it in Settings. Hours do not wrap at 24.

Existing installations retain compatible appearance and time-format preferences. Former effect, background and title settings no longer affect output.

## OBS browser source

Open OBS in ContentWatch, then copy the URL. In OBS Studio add **Sources → Browser**:

- URL: `http://127.0.0.1:17845/overlay` by default.
- Width **1280**, height **360**, custom FPS **30**.
- Keep transparent background CSS. Resize or crop the source as needed.

The server binds only `127.0.0.1`; all endpoints are read-only. Reconnects and source refreshes restore the current time without resetting it. A slow browser cannot stall timing. If the port is occupied, change it on the OBS screen, save, restart the app and update the OBS URL.

## OBS Spout2 — Windows

Install the [Off World Live OBS Spout2 receiver plugin](https://github.com/Off-World-Live/obs-spout2-plugin/releases) matching your OBS Studio version, with OBS closed during installation. The receiver is separate from ContentWatch.

1. Enable Spout2 on ContentWatch's OBS screen and save. Confirm **Spout transmitting** appears.
2. In OBS Studio add **Spout2 Capture** and choose **ContentWatch**, or your configured sender name.
3. Choose **Premultiplied Alpha** composition. Both applications must use the same GPU.

The included native bridge uses the pinned official Spout2 SDK to send transparent BGRA DirectX 11 textures. Both outputs use exactly the same shared rendered frame, with delivery latency varying by receiver. Missing native DLLs and DirectX errors appear on the OBS screen. Toggle Spout off/save then on/save to retry a failed sender. On macOS/Linux use the browser source; Spout2 is Windows-only.

## Global shortcuts and sound

Configure shortcuts in Settings. Defaults are `Ctrl+Alt+F8` (Play), `Ctrl+Alt+F9` (Pause/resume) and `Ctrl+Alt+F10` (Reset). Use Ctrl, Alt, Shift, Win/Cmd/Super with A–Z, 0–9 or F1–F24. Conflicts appear in Settings; choose another binding when necessary.

macOS may require Accessibility/Input Monitoring permissions. Linux uses a native X11/Wayland keyboard-hook backend; some backends require input-device permissions. See [SharpHook's platform requirements](https://sharphook.tolik.io/articles/os-constraints.html). The app never changes permissions or requests root. Non-Windows hooks do not reserve shortcuts exclusively, so another application may also react.

The optional end sound is disabled by default. Capture system/application audio in OBS to record it; the browser is silent. macOS/Linux use the system bell, which may be muted or unavailable. Test sound capture before recording.

## Build and test

Install .NET SDK 10. Windows native builds also need Visual Studio 2022 Build Tools with **Desktop development with C++**, a Windows SDK and CMake.

```powershell
./scripts/build-native.ps1
dotnet run --project src/ContentWatch.Desktop
dotnet test ContentWatch.slnx
./scripts/package-windows.ps1
./artifacts/native/SpoutSmoke.exe
./artifacts/native/SpoutInspect.exe ContentWatch
```

For real GPU integration tests set `$env:CONTENTWATCH_TEST_SPOUT='1'` before `dotnet test`. They compare actual received Spout pixels with the shared renderer. Other hosts/CI explicitly skip hardware tests.

The native build downloads the official SDK to ignored `.deps/spout-sdk` and checks out `6b68a5b4fdeca6130b851e5f9366467a844860e5`. The C++ runtime is linked statically. `SpoutSmoke` validates GPU channels/orientation/alpha; `SpoutInspect` checks the running app's sender.

On macOS/Linux skip the native build, install Avalonia's [Linux platform dependencies](https://docs.avaloniaui.net/docs/deployment/linux) where applicable, and run `dotnet run --project src/ContentWatch.Desktop`. Publish using `dotnet publish src/ContentWatch.Desktop -c Release -r linux-x64 --self-contained true` or `-r osx-arm64` / `osx-x64`. macOS builds are unsigned raw executables rather than notarised app bundles.

GitHub Actions builds/tests Windows x64, Linux x64 and macOS ARM64 and uploads portable artifacts. Interactive platform support has to be tested on each OS. See [validation](docs/VALIDATION.md) and [architecture](docs/ARCHITECTURE.md).

MIT licensed; see [third-party notices](THIRD-PARTY-NOTICES.md).
