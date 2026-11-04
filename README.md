# The Beatles: Rock Band for PC — source code

**WIP 0.7.2 preview · Windows x64 · ReXGlue / Direct3D 12**

<img width="518" height="291" alt="image" src="https://github.com/user-attachments/assets/749af186-da8a-4531-8e42-165602ebefd0" />


This repository contains the native compatibility code, launcher, SDK patches and tests for the PC port. It is intended for contributors who want to inspect, edit and rebuild the project. For playing, use the complete portable application from Releases.

No original game executable, disc files, music, DLC, generated game C++, saves or shader caches are included. Code generation requires your own legally obtained, extracted Xbox 360 copy. Keep generated output and your local game files out of commits.

## Current features

- Guitar and drums, with separate keyboard and physical-device profiles.
- Controller button, D-pad and axis mapping, including analog guitar whammy.
- English launcher with DPI-aware layout, scrollable compact views and DLC import.
- Windows XAudio2 output with an owned COM MTA lifetime, addressing startup error `800401F0`.
- **Performance mode**: an optional scene-transition shader fix, enabled by default and applied on the next game start. A new effect may appear a few frames later while its pipeline compiles.

This is an experimental port. Playable songs have been completed by the tester, but that does not validate every song, device or PC. The reported meadow-to-kaleidoscope transition and the audio startup fix still need confirmation on the reporting machines.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/` | Native C++: audio, instruments, input, timing, networking, compatibility and DLC helper |
| `launcher-src/` | WinForms C# launcher, responsive UI, profiles, DLC manager and UI assets |
| `sdk-patches/` | Two ordered patches against the pinned ReXGlue SDK |
| `config/` | Function discovery configuration and pointer hints for the supported XEX |
| `cmake/ReXGlue.cmake` | Stable build scaffold; generated game sources are excluded |
| `tests/` | Native synchronization/audio tests and launcher/importer regression tests |
| `scripts/DevTools.ps1` | Visual Studio, Clang, CMake and Ninja discovery |
| `tools/` | Optional function-pointer and PDATA inspection scripts |
| `licenses/` | Existing third-party notices |

## Requirements

- Windows x64 with Direct3D 12 for running the port.
- Git available on `PATH`.
- Visual Studio 2022 Build Tools or Visual Studio with **Desktop development with C++**, the Windows SDK, **C++ Clang tools for Windows** and **C++ CMake tools for Windows**. The existing native build used Clang 19.1.5. CMake 3.25 or newer and Ninja must be discoverable or installed in Visual Studio's bundled locations.
- .NET Framework 4.x, including the x64 C# compiler, for the WinForms launcher.
- Python 3.10 or newer for optional fixture and scene-submission tests.
- Your extracted disc files. The supported executable is:

```text
Title ID:    454108B1
Media ID:    40430A45
XEX version: 0.0.0.1
SHA-256:     A47CBAFBC7FC81E4E6CC9BD3CE317DB5EAA9E57FE46AA5EBE09E153C37CA898B
```

For playing, `default.xex` must remain beside `charnames.zbm` and the complete `gen` folder, with its header and ten ARK files. The launcher checks the supported revision and the presence and size of disc files.

## Build

Open PowerShell in this repository. The scripts discover Visual Studio with `VSINSTALLDIR` or `vswhere`; pass `-VsInstallDirectory` if your installation is elsewhere.

### 1. Build the patched SDK

```powershell
.\Build-Sdk.ps1 -Jobs 3
```

This downloads the [ReXGlue SDK](https://github.com/rexglue/rexglue-sdk) at commit `bd833a2ab69d305f99ddf57418fc38ffec8af93d`, initializes required submodules, applies both patches and builds the D3D12 Release SDK. Vulkan, Tracy and performance counters are disabled. The SDK uses the x86-64-v2 CPU baseline and does not register a CMake package globally.

Default source: `external/rexglue-sdk`. Default installation: `external/rexglue-sdk/out/install/tbrb`. Dependencies are downloaded from upstream; they are not bundled in this ZIP. The script refuses to reset a different SDK revision or overwrite conflicting patches.

An existing checkout/install may be selected explicitly:

```powershell
.\Build-Sdk.ps1 -SdkSourceDirectory 'D:\Dev\rexglue-sdk' -SdkInstallDirectory 'D:\Dev\rexglue-install'
.\Build-Sdk.ps1 -SdkSourceDirectory 'D:\Dev\rexglue-sdk' -VerifyOnly
```

### 2. Generate and build the native port

```powershell
.\Build-Game.ps1 -GameDirectory 'D:\Games\TBRB' -Jobs 3
```

This checks the XEX hash, writes `beatlesrockband_manifest.local.toml`, runs code generation **before** configuring CMake, and builds into `out/build/game`. The local manifest and `generated/default/` are ignored by Git. Disc files are read in place, not copied into the repository.

After the first build, the local manifest can be reused:

```powershell
.\Build-Game.ps1 -Jobs 3
```

Use `-SdkDirectory` for an existing patched SDK installation, `-BuildDirectory` for another native output folder, and `-AudioTests` to include the native audio test executable. `-GenerateOnly` and `-ConfigureOnly` diagnose setup without compiling the complete game.

The SDK uses `clang`/`clang++` targeting Windows MSVC; the native game uses `clang-cl`. Keep the executable and runtime DLLs from a matching build together.

### 3. Build the launcher and portable application

```powershell
.\Build-Launcher.ps1
```

Output: `out/BeatlesRockBand-PC`. The script compiles the C# launcher, copies the native executable/helpers and matching runtime DLLs, includes Visual C++ runtime and license files, and writes a configuration without personal data paths.

Use `-GameBuildDirectory`, `-OutputDirectory`, `-VsInstallDirectory` or `-CrtDirectory` to override discovery. The CRT directory must contain the x64 `Microsoft.VC143.CRT` redistributable files for your Visual Studio toolset.

### 4. Package a playable release

```powershell
.\Package.ps1
```

Output: `out/BeatlesRockBand-PC-v0.7.2-preview-portable.zip`. Packaging uses an application-file allowlist and excludes player profiles, saves, imported DLC, caches, logs, PDBs and disc data. Do not distribute the complete native build directory. For usage and DLC import, see [the player guide](launcher-src/README-English.md).

## Performance mode

The first SDK patch contains optional precise GPU timing, waitable presentation and recognized MSAA-clear redirection. The second removes forced background shader/pipeline completion at each submission in asynchronous mode. Cache unload and shutdown still wait for compiler work; shader readiness and persistence use atomic publication.

The launcher always passes an explicit `d3d12_nonblocking_pipeline_creation` value for **Performance mode**. Unchecking it restores the previous shader completion waits. It does not change song speed, note timing or input profiles. Other configured graphics fixes remain enabled in either mode. See [patch details](sdk-patches/README.md).

Achievement popups remain disabled by default to avoid continuous empty overlay redraws; tracking and F7 remain available. `achievement_popups = true` in the runtime TOML restores popups. Presentation stays on the UI thread.

## Tests

Run tests in a fresh, isolated output folder. The launcher suite writes synthetic profiles beside its test executable and must never be pointed at a player's installation.

```powershell
# After building the native port:
.\tests\Test-Launcher.ps1

# Synthetic DLC packages, containing no commercial song data:
python .\tests\Make-DlcFixtures.py .\out\fixtures
.\tests\Test-Launcher.ps1 -FixtureDirectory .\out\fixtures

# Patched SDK synchronization checks; no game or GPU window required:
.\tests\Test-SceneSubmission.ps1

# Native audio tests; opens the default device without submitting sound:
.\Build-Game.ps1 -AudioTests
.\Build-Launcher.ps1
Copy-Item .\out\build\game\windows-audio-tests.exe .\out\BeatlesRockBand-PC\runtime\
.\out\BeatlesRockBand-PC\runtime\windows-audio-tests.exe
```

Keep test output paths short if you encounter the existing .NET Framework path-length limit during DLC tests. Native audio tests require a usable default playback device and matching runtime/Visual C++ DLLs; running the test from the portable runtime folder supplies them. The test executable is excluded by the release packaging allowlist. See [tests/README.md](tests/README.md) for coverage and the intentional failure-path test.

## Contributing

Edit compatibility and host behavior in `src/`, launcher behavior in `launcher-src/`, and generation hints in `config/`. Keep SDK changes as reproducible patches against the pinned commit. Regenerate game C++ locally instead of committing it.

For performance changes, compare the same song, difficulty, resolution, foreground state and cache state. Check audio continuity and chart timing as well as frame intervals. Include build settings, hardware and reproduction steps with bug reports, and remove personal paths from logs.

## Notices

This bundle preserves existing ReXGlue/Xenia, FIFA-reference, SDL and font notices. Those files apply to their respective components; this archive does not assign a new project-wide license to original port contributions. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

The Beatles: Rock Band names and original game content remain with their respective owners. This is an unofficial work in progress.
