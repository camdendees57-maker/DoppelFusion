using MelonLoader;
using DoppelMod.Fusion;
using LabFusion.SDK.Modules;
using UnityEngine;

[assembly: MelonInfo(typeof(DoppelMod.Mod), "Doppel Fusion", "1.0.2", "RUDYHILL")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace DoppelMod
{
    public class Mod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Doppel Fusion starting — smarter AI + real clones");
            if (MelonBase.FindMelon("LabFusion", "Lakatrazz") != null)
            {
                ModuleManager.RegisterModule<FusionDoppelModule>();
            }
            // Call into your existing Entry.Start / BoneMenu here
            // Wire a BoneMenu button to BotManager.SpawnRealClone or similar
        }

        public override void OnUpdate()
        {
            // Your existing Update logic
            // From Bot.Update after pose: FusionBotSync.BroadcastPose(...)
        }
    }
}
