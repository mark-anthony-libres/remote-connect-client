# Bundled FFmpeg (win-x64)

`win-x64/*.dll` are FFmpeg 8.1 shared libraries, LGPL-only build (no GPL
codecs such as x264/x265), from:

https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-n8.1-latest-win64-lgpl-shared-8.1.zip

Version pinned to FFmpeg **8.1.x** to match the `FFmpeg.AutoGen` 8.1.0
dependency of the `SIPSorceryMedia.FFmpeg` 10.0.16 NuGet package used by
`RemoteDesktopClient` (see its .csproj) — `FFmpeg.AutoGen` resolves native
functions by exact DLL filename/version (e.g. `avformat-62.dll`), so a
different FFmpeg major/minor line will not load correctly even if present.

Only the 7 DLLs `RemoteDesktopClient` actually needs are kept (not
`ffmpeg.exe`/`ffplay.exe`/`ffprobe.exe`, headers, or docs from the upstream
zip): `avcodec-62.dll`, `avdevice-62.dll`, `avfilter-11.dll`,
`avformat-62.dll`, `avutil-60.dll`, `swresample-6.dll`, `swscale-9.dll`.

LGPL (not GPL) was chosen deliberately: `RemoteDesktopClient` only ever
negotiates VP8 for its screen-share video track (see
`SIPSorceryMedia.FFmpeg`'s `Helper.GetSupportedVideoFormats()` combined with
`WebRtcSessionManager.TrySetupVideo`'s `formats.First()` selection), and VP8
(libvpx) is BSD-licensed and present in the LGPL build — there is no need for
the GPL build's x264/x265/etc., which would add ~100MB for codecs this app
never uses and carry GPL redistribution obligations. `LICENSE.txt` alongside
this file is FFmpeg's own LGPL license text from that build, required to
ship alongside the binaries.

To update: download the matching `ffmpeg-n<major.minor>-latest-win64-lgpl-shared-<major.minor>.zip`
for whatever FFmpeg.AutoGen version `SIPSorceryMedia.FFmpeg` depends on next,
replace the 7 DLLs in `win-x64/`, and update this file's version reference.
