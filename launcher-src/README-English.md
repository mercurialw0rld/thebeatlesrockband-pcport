# The Beatles: Rock Band for PC — WIP 0.7.2 preview

Windows audio now initializes and retains its own COM MTA lifetime before creating XAudio2. This addresses startup error **800401F0 (CO_E_NOTINITIALIZED)** on PCs where another component has not already initialized COM. Audio setup errors identify the failing stage in Logs and stop before the game uses an invalid audio handle.

## Performance mode

The **Performance mode** switch in the launcher enables the scene-transition shader fix. It is on by default, including when upgrading an existing profile, and is saved immediately. Changes apply the next time you start the game. When enabled, unfinished shader pipelines stay in the background instead of blocking each rendering submission; a new effect may appear a few frames later. Turn it off to restore the previous shader completion waits if you notice missing or delayed effects. This switch does not change audio speed, note timing, resolution or your control profiles. The other 0.7.0 graphics fixes remain active in both modes.

This preview targets scene-transition stalls. Background shader compilation no longer drains and blocks the render thread at every submission; unloading or replacing the shader cache still waits for pending work. Shader readiness and cache-flush requests now use atomic publication. Pending draws retain the existing async behavior: a newly encountered visual effect may appear after its pipeline finishes compiling.

Empty achievement notifications no longer force continuous overlay redraws: achievement popups are off by default, while achievement tracking and F7 remain available. Presentation stays on the UI thread. The prior audio, controls, DLC and responsive UI fixes are retained.

**The reported meadow-to-kaleidoscope transition and musical replay symptom have not been retested by the user in this preview.** Automated checks do not prove that every transition is fixed or that audio works on every device. The previous ZIPs are retained for rollback. Set `achievement_popups = true` in `runtime/beatlesrockband.toml` before launching if you prefer achievement popups. Use **Performance mode** to select shader completion behavior; the launcher explicitly overrides `d3d12_nonblocking_pipeline_creation` in the TOML file.

0.7.0 adds a rebuilt graphics runtime with more precise GPU timing, a waitable presentation queue and fewer copies for recognized MSAA clear passes. In the tester's one-minute A/B comparison of Within You Without You, guest frame intervals above 20 ms fell from 106 to 9; average GPU utilization was about 90% versus 80%. Both runs stayed near 60 guest frames per second. This is an exploratory comparison on one PC, not a guarantee for every machine or a measurement of monitor scanout. Isolated longer frames remain, and the short CPU sample increased.

Replace the complete application package together: the executable and its runtime DLLs must match. Keep **launcher-settings.json**, **userdata** and **cache**, including installed DLC and saved games. Performance tracing is disabled in the normal package. Existing audio fixes, guitar/drum profiles, **Import DLC**, adaptive layout and the 0.6.1 repaint fix are retained.

1. Extract the complete package to a writable folder.
2. Open **BeatlesRockBandLauncher.exe**.
3. Choose **default.xex** from your legally obtained, extracted copy of the disc. Keep **charnames.zbm** and the complete **gen** folder beside it, with its header and ten ARK files.
4. Choose your display settings and click **Play**. Accept song-cache creation if offered.

No XEX or disc data is included. Supported XEX SHA-256: A47CBAFBC7FC81E4E6CC9BD3CE317DB5EAA9E57FE46AA5EBE09E153C37CA898B.

## Import DLC

Click **Import DLC** at the bottom of the launcher, then **Choose DLC files…**. Select one or more original Xbox 360 LIVE, PIRS or CON packages for The Beatles: Rock Band; many have long names with no extension. Extract ZIP/RAR archives first and select the package files inside them, not the archive or the XEX.

The importer lists installed packages, skips identical packages even if renamed, and leaves the original files intact. It prepares each package separately before installation; a failed or cancelled extraction does not replace an existing package. Restart the game after importing and allow its song cache to update if prompted. Installed DLC stays in **userdata** and is not included in the distributable ZIP.

Only data-only DLC for title ID **454108B1** is accepted. Title updates and packages containing new executable modules require separate support. The supplied Abbey Road, Rubber Soul, All You Need Is Love and Sgt. Pepper packages passed extraction and installation tests. The tester confirmed All You Need Is Love appears and plays with audio and notes; this does not validate every DLC track.

## Controls and devices

Open **Edit controls**, choose an **Instrument**, then a **Device**. Connect your Windows-compatible gamepad, guitar or drum controller first; **Refresh devices** scans again. The list also refreshes automatically. Devices requiring a receiver or driver must already be recognized by Windows. Direct MIDI input is not implemented.

Click an action, then press a key or controller button, hit a pad, use a D-pad direction, or move an axis. For analog whammy, release the bar before clicking **Whammy**, then move it through its range. **Escape** cancels controller capture. **×** clears an assignment. **Save** stores separate profiles for each instrument and device and preserves your existing guitar assignments. Changes apply the next time you start the game; restart to change instruments.

Controller presets are starting points: check and map the inputs for your particular controller. The selected device appears in-game as one guitar or one drum kit, according to your Instrument selection. This does not add simultaneous multiplayer instruments. If it is disconnected, reconnect it or select **Keyboard**. Up to 64 buttons, 16 axes and four D-pads per device are supported. Identical devices without a serial number or distinct device path may need their profiles reselected after reconnecting in a different order.

| Default keyboard input | Action |
| --- | --- |
| 1 / Space | Green / select |
| 2 / Backspace | Red / back |
| 3, 4, 5 | Yellow, blue, orange |
| Up / Down | Strum / navigate |
| Enter | Start / pause |
| Tab | Beatlemania / Star Power |
| W | Whammy, hold on sustained notes |
| S | Solo frets, hold in solo sections |
| Left / Right | Navigate menus |

Frets, strum and whammy work together. Keyboard whammy is digital; a mapped controller axis preserves its analog range. Beatlemania needs sufficient energy. Solo frets work in sections recognized by the game. Give the game window focus.

### Drums

| Default keyboard input | Action |
| --- | --- |
| D | Red pad / snare |
| F | Yellow pad |
| J | Blue pad |
| K | Green pad / select |
| Space | Kick pedal |
| 1 / 2 | Alternate select / back |
| Arrow keys | Navigate menus |
| Enter | Start / pause |
| Tab | Back button |

Hit pads directly; no strumming is needed. For Beatlemania, play the drum fill and hit the final green note. Pad and pedal inputs are digital; this version does not add velocity sensitivity or a separate pro-cymbal mode. Physical controllers and axis-based pedals must be mapped to the inputs reported by their Windows driver.

## Status

The launcher and editor are in English. Existing keyboard assignments, settings and saved games are preserved. Keyboard drum recognition, the drum chart, all four pads and the kick pedal were confirmed in-game by the tester. Software checks cover separate guitar/drum profiles, controller mapping and analog whammy. Layout previews cover 100%, 125%, 150% and 200% scaling and compact windows; this does not replace testing every monitor configuration. Physical-controller gameplay still needs hardware testing.

The 0.3.2 performance improvements remain active: cooperative GPU waits and offline network backoff outside its internal lock. Audio uses Windows XAudio2 with regularly spaced mixing callbacks. The tester confirmed improved smoothness with the 0.7.0 graphics configuration. Occasional frame-time spikes and possible manual audio/video calibration remain. This is an experimental port; microphone, online play, all songs and all instrument models are not validated.

Windows 10/11 x64, Direct3D 12 and .NET Framework 4.8 are required. Tested with i3-8100, 16 GB RAM and GTX 1050 Ti. Visual C++ runtime libraries are included. Saves, settings, shader cache and logs stay beside the launcher. ReXGlue and build tools are not needed to play.

The original submarine artwork, Aero palette and embedded DM Serif Display are retained. Font and SDL/ReXGlue licenses are included.

SDK: https://github.com/rexglue/rexglue-sdk (bd833a2). Reference: https://github.com/HossEz/fifa-world-cup-2014-recompiled.
