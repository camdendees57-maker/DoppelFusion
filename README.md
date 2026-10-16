# Doppel Fusion

BoneLab bot mod (Doppel) with Fusion multiplayer visibility and smarter AI.

## Setup
1. Install MelonLoader on BONELAB.
2. Run the game once so Il2CppAssemblies generate.
3. Drop these DLLs into the `refs/` folder (or set BONELAB_DIR env):
   - From `BONELAB/MelonLoader/Il2CppAssemblies/`: Assembly-CSharp.dll, Il2CppSLZ.Marrow.dll, Il2CppSLZ.Bonelab.dll, etc.
   - BoneLib.dll from https://thunderstore.io/c/bonelab/p/bonelib/BoneLib/
   - LabFusion.dll (you said you'll drop this)
   - MelonLoader.dll, 0Harmony.dll, Il2CppInterop.* from MelonLoader/net6/
4. Open in VS, restore, build.
5. Drop the output DLL into BONELAB/Mods/.

## DLL Sources
- BoneLib: https://thunderstore.io/c/bonelab/p/bonelib/BoneLib/
- LabFusion: https://github.com/Lakatrazz/BONELAB-Fusion/releases
- Game assemblies: generated in your game install after MelonLoader run, or use stripped ones from NuGet Bonelab.GameLibs.Steam
- MelonLoader: https://github.com/LavaGang/MelonLoader/releases

Bots broadcast poses/states over Fusion Module messages so other players see them. AI reacts faster, engages earlier, covers longer.
