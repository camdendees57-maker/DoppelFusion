using MelonLoader;
using DoppelMod.Fusion;
using LabFusion.SDK.Modules;

[assembly: MelonInfo(typeof(DoppelMod.Mod), "Doppel Fusion", "1.0.1", "RUDYHILL")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace DoppelMod
{
    public class Mod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Doppel Fusion starting");
            if (MelonBase.FindMelon("LabFusion", "Lakatrazz") != null)
            {
                ModuleManager.RegisterModule<FusionDoppelModule>();
            }
            // Call into your existing Entry.Start / BoneMenu here
        }

        public override void OnUpdate()
        {
            // Your existing Update logic + call FusionBotSync.BroadcastPose from Bot.Update
        }
    }
}
