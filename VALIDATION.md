# Source verification — 0.7.3 preview, 4 November 2026

The native game executable and audio/GPU/input runtime binaries are unchanged from 0.7.2. The DLC helper and launcher were rebuilt. The existing portable build scaffold is retained.

The bounded catalog parser passed 20 checks. Launcher checks passed: keyboard 26, controller 21, instrument/layout 4922, DLC layout 442, repaint 18, Performance mode 35 and synthetic DLC importer 44. Five additional long-path checks passed, covering import, indexing, duplicates and scratch cleanup with an actual header path over 260 characters. The first fixture was corrected to guarantee that length on short checkout paths.

The four supplied custom packages imported successfully in isolated userdata: Here, There And Everywhere (9 files), If I Fell (5), Lady Madonna (7), and We Can Work It Out (9). The first two declare generic title FFFE07D1 and were rejected before the fix. Lady Madonna also reproduced the old incomplete-preparation error on a long root, then imported successfully with the fix. Source packages were opened read-only.

Only the import path was tested for these four songs; playback in the game remains unverified. No new FPS/audio-performance result is claimed. The previous clean-source generation/configuration checks remain applicable; no full regeneration or complete SDK rebuild was repeated for this importer-only change.

No game data, commercial audio, generated game C++, player profiles or build outputs are included.

Release packaging update: LEEME.md is omitted from the portable ZIP and the launcher build scripts no longer copy it. Documentation remains in the GitHub repository. The resulting 23-file application ZIP retains its startup .NET configuration. A fresh portable launcher/package build verified the omission; executable game behavior is unchanged.
