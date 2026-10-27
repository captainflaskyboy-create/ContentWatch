# Dependency notices

ContentWatch is MIT licensed. Dependencies retain their own licenses:

- .NET / ASP.NET Core: MIT, https://github.com/dotnet/runtime and https://github.com/dotnet/aspnetcore
- Avalonia 11.3.6: MIT, https://github.com/AvaloniaUI/Avalonia
- SkiaSharp 2.88.9: MIT, https://github.com/mono/SkiaSharp. Native Skia and bundled libraries retain their respective notices shipped with the NuGet assets.
- SharpHook 8.0.1: MIT, https://github.com/TolikPylypchuk/SharpHook. libuiohook is LGPL-3.0; it remains a separate dynamic native library. Exact corresponding source and build instructions: https://github.com/TolikPylypchuk/libuiohook/tree/3890543a1aa09f20966b1784650487093daad97e (downloadable source: https://github.com/TolikPylypchuk/libuiohook/archive/3890543a1aa09f20966b1784650487093daad97e.tar.gz). Users may replace/relink that dynamic library for their platform. No reverse-engineering restriction is imposed for debugging modifications to that library. Complete GPL/LGPL notices are in `docs/licenses/`.
- Tmds.DBus.Protocol 0.21.3: MIT, https://github.com/tmds/Tmds.DBus
- Spout2 SDK: BSD-2-Clause, https://github.com/leadedge/Spout2, pinned revision `6b68a5b4fdeca6130b851e5f9366467a844860e5`. Its full notice is in `docs/Spout2-LICENSE.txt` and Windows packages.
- xUnit and Microsoft.NET.Test.Sdk are test-only dependencies.

The OBS Spout2 receiver plugin is installed separately and is not bundled with ContentWatch: https://github.com/Off-World-Live/obs-spout2-plugin.
