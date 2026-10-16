# Doppel Fusion

BoneLab bot mod (Doppel) with Fusion multiplayer visibility and smarter AI.

## Setup
1. Clone this repo.
2. Put the DLLs from your uploaded source.zip into the `refs/` folder (LabFusion.dll, BoneLib.dll, Assembly-CSharp.dll, Il2CppSLZ.Marrow.dll, MelonLoader.dll, 0Harmony.dll, Il2CppInterop.Runtime.dll, etc.).
3. Open DoppelFusion.csproj in Visual Studio or build with `dotnet build`.
4. Drop the output DLL into your BONELAB/Mods/ folder.
5. Make sure Fusion is installed and running for the bots to sync to other players.

The Fusion module broadcasts bot head/hand poses and state so anyone in the lobby can see the bots. AI constants are tightened for faster reactions, earlier engages, and longer cover.

Original Doppel logic goes in the Bot class; call FusionBotSync.BroadcastPose / BroadcastState from Update and mode changes.
