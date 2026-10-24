# The Beatles: Rock Band for PC (Xbox 360 recomp) — WIP 0.6.1

<img width="1600" height="800" alt="The Beatles: Rock Band PC launcher" src="https://github.com/user-attachments/assets/b99f0e65-bb90-49ff-b582-8cbda9cce488" />

## How to play

1. Download the latest portable package from the **Releases** section and extract it to a writable folder.
2. Open **BeatlesRockBandLauncher.exe**.
3. Choose **default.xex** from your legally obtained, extracted copy of the Xbox 360 disc. Keep **charnames.zbm** and the complete **gen** folder beside it, including its header and ten ARK files.
4. Choose your display settings and click **Play**. Accept song-cache creation if offered.

The release package does not include the XEX, disc data or DLC.

## Import DLC

1. Close the game and open the launcher.
2. Click **Import DLC** at the bottom of the launcher.
3. Click **Choose DLC files…** and select your Xbox 360 DLC packages. You can select several files at once.
4. Wait for the import to finish. Successfully imported packages appear in the installed-content list.
5. Start the game again and allow its song cache to update if prompted. The additional songs should appear in **Quickplay**.

Select the original **LIVE, PIRS or CON** package files. These often have long hexadecimal names with **no file extension**. If your backup uses the Xbox 360 content-folder structure, select the package files inside the game's **00000002** folder.

**ZIP and RAR archives must be extracted first.** Select the DLC packages inside them, not the archive, a folder or the game's XEX.

The importer checks that packages belong to The Beatles: Rock Band, skips identical packages even if renamed, and leaves your original files intact. Existing installed packages are not overwritten. Failed or cancelled extractions are not installed as partial packages; packages that finished earlier in the batch remain installed.

Imported DLC is stored inside **userdata** beside the launcher. Keep that folder when updating the port. You do not need to recompile anything when importing supported DLC.

Only data-only DLC is supported. **Title updates and DLC containing additional executable code are not supported by this importer.**

## Controls and devices

Open **Edit controls** and choose an **Instrument**: **Guitar** or **Drums**. Then choose your **Device**: keyboard or a connected Windows-compatible controller, guitar or drum kit.

Connect your device first and use **Refresh devices** if needed. Devices requiring a receiver or driver must already be recognized by Windows. Direct MIDI input is not implemented.

Click an action, then press a key or controller button, hit a pad, use a D-pad direction, or move an axis. For analog whammy, release the bar before selecting **Whammy**, then move it through its range. **Escape** cancels controller capture; **×** clears an assignment.

**Save** keeps separate profiles for each instrument and device, preserving your existing guitar bindings. Changes apply the next time you start the game. The selected device is presented to the game as one guitar or one drum kit.

Controller presets are starting points: check the mappings for your particular hardware. If the selected device is disconnected, reconnect it or choose **Keyboard**.

### Guitar — default keyboard controls

| Input | Action |
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

Frets, strum and whammy work together. Keyboard whammy is digital; a mapped controller axis preserves its analog range. Beatlemania requires sufficient energy. Give the game window focus.

### Drums — default keyboard controls

| Input | Action |
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

Hit the pads directly; no strumming is needed. To activate Beatlemania, play a drum fill and hit its final green note. Pads and pedal use digital inputs; velocity sensitivity and a separate pro-cymbal mode are not implemented.

## Updating

Replace the application files with the complete new release. Keep **launcher-settings.json**, **userdata** and **cache** to preserve your mappings, saves, imported DLC and shader cache.

Version **0.6.1** fixes black gaps in the launcher's panels while retaining automatic screen-size adaptation, Windows display scaling and scrolling on smaller screens.

## Status

The game is playable, with around 60 FPS observed in tested gameplay. Occasional stutters may remain, performance depends on your hardware, and manual audio/video calibration may be needed.

Keyboard guitar and drum gameplay have been tested. DLC import was tested with **Abbey Road**, **Rubber Soul**, **Sgt. Pepper's Lonely Hearts Club Band** and **All You Need Is Love**. **All You Need Is Love** was confirmed playable with audio and notes; not every DLC track or physical instrument model has been tested.

