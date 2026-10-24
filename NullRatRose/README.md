# Null Rat Rose (BONELAB Quest)

A BONELAB code mod for standalone Meta Quest. A TikTok LIVE Rose causes five vanilla Null Rats to spawn; each rat receives a separately randomized material tint when its materials permit recoloring.

## Architecture

- **Quest:** LemonLoader loads `NullRatRose.dll`. The mod listens on TCP port `28991` for authenticated messages and performs all Marrow spawning on BONELAB's main thread.
- **PC:** `TikTokBridge/tiktok_listener.py` listens to a TikTok LIVE stream and sends Rose events to the Quest over the same local Wi-Fi network.
- **Build:** GitHub Actions compiles the DLL and packages the mod plus the bridge into an artifact ZIP.

The TikTok bridge is intentionally separate because the headset does not need to run a Python process. Your PC must remain on and connected to the same network during the stream.

## Important compatibility note

This is an initial Quest-targeted implementation. GitHub Actions can compile the managed DLL, but that does not prove that spawning or tinting works in every BONELAB/LemonLoader version. The mod resolves Marrow spawn types at runtime to avoid bundling proprietary game assemblies. If the initial Null Rat barcode or Marrow overload differs in your installed build, the MelonLoader log will report the failure; update `NullRatBarcode` in `NullRatRoseMod.cs` and rebuild.

## Build

1. Open the **Actions** tab in this repository.
2. Select **Build Null Rat Rose for Quest**.
3. Click **Run workflow** on the `rose-nullrats` branch, or push a change to the project.
4. Download the `NullRatRose-Quest` artifact ZIP from the finished run.

The artifact contains the mod DLL and the PC-side TikTok bridge.

## Quest installation

1. Install and patch BONELAB with a compatible version of [LemonLoader](https://github.com/LemonLoader/MelonLoader). Back up your game data first.
2. Extract `NullRatRose.dll` from the artifact.
3. Put the DLL in the LemonLoader Mods folder for BONELAB. Depending on the loader version, this is commonly under `/sdcard/MelonLoader/<BONELAB package name>/Mods`; use the path shown by your LemonLoader setup.
4. Install the required BoneLib version separately if your broader mod setup needs it. This mod does not bundle BoneLib or game assemblies.
5. Launch BONELAB, load into an actual level, and keep the headset on the same Wi-Fi network as the PC.

## Configure the PC bridge

1. Extract the artifact's `TikTokBridge` folder on the PC.
2. Copy `.env.example` to `.env`.
3. Set `TIKTOK_USERNAME` to your public LIVE username and `QUEST_IP` to the Quest's local Wi-Fi IP address.
4. Create a long random secret. Set it as `ROSE_BRIDGE_SECRET` in `.env`, then set the exact same text as `SharedSecret` in `NullRatRoseMod.cs`.
5. Rebuild and reinstall the DLL after changing the C# secret. Never publish your real secret.
6. Run `start_windows.bat`. Keep the terminal open during the stream.

The Quest must be reachable from the PC on TCP port `28991`. If the PC cannot connect, check that both devices are on the same Wi-Fi network and that router/client isolation or firewall rules are not blocking LAN traffic. Do not expose this port to the public internet.

## Behavior and safety caps

- One Rose: 5 Null Rats.
- A single incoming event is capped at 10 Roses (at most 50 rats from that message).
- Gifts other than Rose are ignored.
- Each Rose batch is queued from the network thread, then spawned on the game thread.
- Random color is applied to child renderers' material colors when supported.

This build is a starting point for live headset testing, not a claim that every Quest version has already been tested in-game.
