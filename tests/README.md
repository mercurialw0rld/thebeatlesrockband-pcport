# Regression tests

`Test-Launcher.ps1` builds an isolated portable copy from the native build. Pass `-GameBuildDirectory`, `-VsInstallDirectory` or `-CrtDirectory` when needed. Always use a fresh output folder; the suite writes synthetic settings and userdata beside its executables and must never run in a player's installation.

The suite covers keyboard/controller mapping, guitar/drum profiles, multiple DPI scales, scroll access, DLC UI, opaque painting and Performance mode migration/persistence/arguments. Generate synthetic packages with `python Make-DlcFixtures.py <fixture-folder>` and pass `-FixtureDirectory <fixture-folder>` to test extraction. Fixtures contain text, not commercial songs. Keep test paths short if .NET Framework's existing path-length limit causes an error.

`Test-SceneSubmission.ps1` extracts actual SDK method bodies and compiles worker/event doubles. It requires C++ tools and Python, but no game data or graphics window. `-Original -SdkSourceDirectory <clean-base-checkout>` expects to reproduce the old blocking path.

Build with `Build-Game.ps1 -AudioTests` for `windows-audio-tests.exe`. It includes the production driver and checks COM initialization, existing STA/MTA callers, cross-thread lifetime, independent cookies, injected engine failure and reinitialization. It opens the default device without submitting sound and requires a usable playback device. Run it with matching rexruntime and Visual C++ runtime DLLs; the root README shows how to use the portable runtime folder. A missing runtime DLL can prevent the test from starting before it produces a log.

`windows-audio-tests.exe --fail-driver` intentionally injects an engine failure. It must exit nonzero at the fatal audio factory path and log XAudio2Create error 80004005, rather than return an invalid guest handle. This is an intentional failure test, not a normal launch mode.

These checks do not replace actual gameplay, physical controllers, listening tests or multi-monitor validation.
