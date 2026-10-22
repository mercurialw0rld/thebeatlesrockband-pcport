# The Beatles: Rock Band for PC — WIP 0.4.0

1. Extract the complete package to a writable folder.
2. Open **BeatlesRockBandLauncher.exe**.
3. Choose **default.xex** from your legally obtained, extracted copy of the disc. Keep **charnames.zbm** and the complete **gen** folder beside it, with its header and ten ARK files.
4. Choose your display settings and click **Play**. Accept song-cache creation if offered.

No XEX or disc data is included. Supported XEX SHA-256: A47CBAFBC7FC81E4E6CC9BD3CE317DB5EAA9E57FE46AA5EBE09E153C37CA898B.

## Controls and devices

Open **Edit controls** and choose a **Device**. Connect your Windows-compatible gamepad or guitar first; **Refresh devices** scans again. The list also refreshes automatically. Devices requiring a receiver or driver must already be recognized by Windows.

Click an action, then press a key or controller button, use a D-pad direction, or move an axis. For analog whammy, release the bar before clicking **Whammy**, then move it through its range. **Escape** cancels controller capture. **×** clears an assignment. **Save** stores a separate profile for each device and preserves your keyboard profile. Changes apply the next time you start the game.

Controller presets are starting points: check and map the inputs for your particular controller. The selected physical device appears in-game as one guitar. If it is disconnected, reconnect it or select **Keyboard**. Up to 64 buttons, 16 axes and four D-pads per device are supported. Identical devices without a serial number or distinct device path may need their profiles reselected after reconnecting in a different order.

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

## Status

The launcher and editor are in English. Existing keyboard assignments, settings and saved games are preserved. Software checks cover controller mapping and analog whammy; physical-controller gameplay still needs hardware testing.

The 0.3.2 performance improvements remain active: cooperative GPU waits and offline network backoff outside its internal lock. Audio uses Windows XAudio2 with regularly spaced mixing callbacks. Occasional frame-time spikes remain. This is an experimental port; microphone, drums, online play, all songs and all instrument models are not validated.

Windows 10/11 x64, Direct3D 12 and .NET Framework 4.8 are required. Tested with i3-8100, 16 GB RAM and GTX 1050 Ti. Visual C++ runtime libraries are included. Saves, settings, shader cache and logs stay beside the launcher. ReXGlue and build tools are not needed to play.

The original submarine artwork, Aero palette and embedded DM Serif Display are retained. Font and SDL/ReXGlue licenses are included.

SDK: https://github.com/rexglue/rexglue-sdk (bd833a2). Reference: https://github.com/HossEz/fifa-world-cup-2014-recompiled.
