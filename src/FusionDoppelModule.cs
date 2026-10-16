using System;
using System.Collections.Generic;
using LabFusion.SDK.Modules;
using LabFusion.Network;
using LabFusion.Network.Serialization;
using MelonLoader;
using UnityEngine;

namespace DoppelMod.Fusion
{
    public class FusionDoppelModule : Module
    {
        public override string Name => "Doppel Fusion";
        public override string Author => "RUDYHILL";
        public override Version Version => new(1, 0, 1);
        public override ConsoleColor Color => ConsoleColor.Cyan;

        protected override void OnModuleRegistered()
        {
            ModuleMessageManager.RegisterHandler<BotPoseMessage>();
            ModuleMessageManager.RegisterHandler<BotStateMessage>();
            MelonLogger.Msg("Doppel Fusion module loaded");
        }

        protected override void OnModuleUnregistered() { }
    }

    public class BotPoseData : INetSerializable
    {
        public int? GetSize() => 4 + 12 + 16 + 12 + 16 + 12 + 16 + 1 + 1;

        public int BotId;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 LeftHand;
        public Quaternion LeftRot;
        public Vector3 RightHand;
        public Quaternion RightRot;
        public byte Mode;
        public bool HasGun;

        public void Serialize(INetSerializer s)
        {
            s.SerializeValue(ref BotId);
            s.SerializeValue(ref Position);
            s.SerializeValue(ref Rotation);
            s.SerializeValue(ref LeftHand);
            s.SerializeValue(ref LeftRot);
            s.SerializeValue(ref RightHand);
            s.SerializeValue(ref RightRot);
            s.SerializeValue(ref Mode);
            s.SerializeValue(ref HasGun);
        }
    }

    public class BotPoseMessage : ModuleMessageHandler
    {
        protected override void OnHandleMessage(ReceivedMessage received)
        {
            var data = received.ReadData<BotPoseData>();
            FusionBotSync.ApplyPose(data);
        }
    }

    public class BotStateData : INetSerializable
    {
        public int? GetSize() => 4 + 1 + 1;
        public int BotId;
        public byte Goal;
        public bool Enemy;

        public void Serialize(INetSerializer s)
        {
            s.SerializeValue(ref BotId);
            s.SerializeValue(ref Goal);
            s.SerializeValue(ref Enemy);
        }
    }

    public class BotStateMessage : ModuleMessageHandler
    {
        protected override void OnHandleMessage(ReceivedMessage received)
        {
            var data = received.ReadData<BotStateData>();
            FusionBotSync.ApplyState(data);
        }
    }

    public static class FusionBotSync
    {
        private static readonly Dictionary<int, GameObject> Proxies = new();
        private static int NextId = 1;
        private static readonly Dictionary<object, int> Ids = new();

        public static void BroadcastPose(object bot, Vector3 headPos, Quaternion headRot, Vector3 leftPos, Quaternion leftRot, Vector3 rightPos, Quaternion rightRot, byte mode, bool hasGun)
        {
            if (!LabFusion.Network.NetworkInfo.HasServer) return;
            var data = new BotPoseData
            {
                BotId = GetOrAssignId(bot),
                Position = headPos,
                Rotation = headRot,
                LeftHand = leftPos,
                LeftRot = leftRot,
                RightHand = rightPos,
                RightRot = rightRot,
                Mode = mode,
                HasGun = hasGun
            };
            MessageRelay.RelayModule<BotPoseMessage, BotPoseData>(data, new MessageRoute(RelayType.ToOtherClients, NetworkChannel.Unreliable));
        }

        public static void BroadcastState(object bot, byte goal, bool enemy)
        {
            if (!LabFusion.Network.NetworkInfo.HasServer) return;
            var data = new BotStateData
            {
                BotId = GetOrAssignId(bot),
                Goal = goal,
                Enemy = enemy
            };
            MessageRelay.RelayModule<BotStateMessage, BotStateData>(data, new MessageRoute(RelayType.ToOtherClients, NetworkChannel.Reliable));
        }

        private static int GetOrAssignId(object bot)
        {
            if (!Ids.TryGetValue(bot, out var id))
            {
                id = NextId++;
                Ids[bot] = id;
            }
            return id;
        }

        public static void ApplyPose(BotPoseData data)
        {
            if (!Proxies.TryGetValue(data.BotId, out var proxy))
            {
                proxy = new GameObject($"DoppelProxy_{data.BotId}");
                Proxies[data.BotId] = proxy;
            }
            proxy.transform.position = data.Position;
            proxy.transform.rotation = data.Rotation;
        }

        public static void ApplyState(BotStateData data) { }
    }
}
