# Null Rat Rose (BONELAB Quest)

A BONELAB code mod for standalone Meta Quest. TikTok LIVE events can trigger batches of five vanilla Null Rats, each with a separately randomized material tint when its materials permit recoloring. BoneMenu toggles let you choose which event types trigger spawns.

## Architecture

- **Quest:** LemonLoader loads `NullRatRose.dll`. The mod listens on TCP port `28991` for authenticated messages and performs all Marrow spawning on BONELAB's main thread.
- **PC:** `TikTokBridge/tiktok_listener.py` listens to TikTok LIVE gift, like, follow, share, and viewer-join events and sends them to the Quest over the same local Wi-Fi network.
- **Build:** GitHub Actions compiles the DLL and packages the mod plus the bridge into an artifact ZIP.

The TikTok bridge is intentionally separate because the headset does not need to run a Python process. Your PC must remain on and connected to the same network during the stream.

## Important compatibility note

This is an initial Quest-targeted implementation. It uses BoneLib's `HelperMethods.SpawnCrate` and BoneLib's vanilla Null Rat barcode. GitHub Actions can compile the managed DLL, but that does not prove spawning or tinting works in every BONELAB/LemonLoader version. If a test button logs a spawn failure, share the relevant MelonLoader log so the runtime issue can be diagnosed.

## BoneMenu event controls

Open **BoneMenu → Null Rat Rose** after loading into a level.

- **Trigger: Rose Gifts** — enabled by default; every Rose sends five rats (up to ten Roses per event).
- **Trigger: Likes** — disabled by default; when enabled, each configured like threshold requests a five-rat batch.
- **Likes per 5 Rats** — choose a threshold from 10 to 1,000 likes in increments of 10. TikTok may group many taps into one LikeEvent.
- **Trigger: Follows** — requests five rats for each follow event.
- **Trigger: Shares** — requests five rats for each share event.
- **Trigger: Viewer Joins** — requests five rats for each join event. This is disabled by default because joining can happen frequently.
- **TEST: Simulate Rose / Like Burst / Follow / Share / Viewer Join** — test the corresponding trigger without connecting the TikTok listener; make sure the trigger is enabled first.
- **TEST: Spawn 1 / Spawn 5 Null Rats** — directly test the spawn API, independently of event toggles.

Like-triggered batches are rate-limited to two batches per game frame and capped at 50 queued batches to reduce the chance of freezing standalone Quest.

## Build

1. Open the **Actions** tab in this repository.
2. Select **Build Null Rat Rose for Quest**.
3. Click **Run workflow** on the `rose-nullrats` branch, or push a change to the project.
4. Download the `NullRatRose-Quest` artifact ZIP from the finished run.

The artifact contains the mod DLL and the PC-side TikTok bridge.

## Quest installation

1. Install and patch BONELAB with a compatible version of [LemonLoader](https://github.com/LemonLoader/MelonLoader). Back up your game data first.
2. Extract `NullRatRose.dll` from the artifact.
3. Install BoneLib v3.2.2 separately into the same BONELAB code-mod Mods folder. BoneLib is required for the menu and is not bundled into this ZIP.
4. Put `NullRatRose.dll` in the same LemonLoader Mods folder. Depending on your loader version, use the path shown by your LemonLoader setup; common layouts include `/sdcard/MelonLoader/<BONELAB package name>/Mods` or the BONELAB `files/Mods` folder.
5. Launch BONELAB, load into an actual level, and open BoneMenu from your in-game watch/menu controls.
6. Open **Null Rat Rose** and test **TEST: Spawn 1 Null Rat** first. If that works, try **TEST: Spawn 5 Null Rats**. **TEST: Simulate Rose (5 Rats)** exercises the gift queue without requiring the Python listener.

## Configure the PC bridge

1. Extract the artifact's `TikTokBridge` folder on the PC.
2. Copy `.env.example` to `.env`.
3. The example is prefilled with TikTok username `tagtusvr_ac` and Quest IP `192.168.1.35`. Keep those values if they still match your setup.
4. Generate a fresh long random secret on your PC with `py -c "import secrets; print(secrets.token_urlsafe(32))"`.
5. In GitHub, open **Settings → Secrets and variables → Actions** and replace or create the repository secret named `ROSE_BRIDGE_SECRET` using the fresh value. The workflow injects it into the build workspace only; it is not stored in source code.
6. Run the **Build Null Rat Rose for Quest** workflow again and install the new DLL from that run.
7. Run `start_windows.bat`. It will prompt for the same secret without displaying the typed characters. The secret is not required in `.env` and should never be committed.

Never put the actual secret in source files, `.env.example`, or a committed `.env` file.

The Quest must be reachable from the PC on TCP port `28991`. If the PC cannot connect, check that both devices are on the same Wi-Fi network and that router/client isolation or firewall rules are not blocking LAN traffic. Do not expose this port to the public internet.

## Behavior and safety caps

- One enabled event batch: 5 Null Rats.
- Rose gift quantities are capped at 10 per event (at most 50 rats from that message).
- Likes accumulate until the configured threshold; follows, shares, and viewer joins are separate event types.
- Event messages are queued from the network thread, then spawned on the game thread.
- Random color is applied to child renderers' material colors when supported.

This build is a starting point for live headset testing, not a claim that every Quest version has already been tested in-game.
