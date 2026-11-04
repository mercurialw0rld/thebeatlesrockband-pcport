# Source bundle verification — 4 November 2026

- PowerShell scripts parsed successfully and tool discovery located Visual Studio, Clang, CMake and Ninja without author-specific paths in the bundle.
- Both graphics patches were verified against the pinned SDK checkout.
- A fresh source copy generated the game C++ from the supported local XEX and configured CMake successfully. Automatic code generation uses the ignored local manifest; stable CMake integration lives outside the generator's output folder.
- Native input bridge, DLC helper and audio-test targets compiled in that source copy; all 24 native audio checks passed without submitting sound.
- Launcher built from the source bundle using the matching existing game binary. Checks passed: keyboard 26, controller 21, instrument/layout 4922, DLC layout 442, repaint 18, Performance mode 35, synthetic DLC importer 24.
- Seven scene-submission synchronization checks passed. The playable-package script was smoke-tested with its 23-file application allowlist.
- All packaged native source files and SDK patch files match the 0.7.2 preview working source. No disc files, generated game C++, local paths, player data, build products or downloaded toolchains are included.

The entire SDK was not downloaded/rebuilt again, and every generated game translation unit was not recompiled for this source-packaging check. The existing playable build passed its earlier build and regression checks. No new gameplay, listening, GPU performance or physical controller test is claimed by this source archive.
