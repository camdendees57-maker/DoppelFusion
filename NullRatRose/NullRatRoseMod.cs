using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using MelonLoader;
using UnityEngine;
using BoneLib.BoneMenu;

[assembly: MelonInfo(typeof(NullRatRose.NullRatRoseMod), "Null Rat Rose", "0.1.0", "TagtusVR")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace NullRatRose
{
    public sealed class NullRatRoseMod : MelonMod
    {
        private const int BridgePort = 28991;
        // Set this same value in TikTokBridge/.env. Change it before streaming.
        private const string SharedSecret = "CHANGE_THIS_TO_A_LONG_RANDOM_SECRET";
        // Override here if your installed BONELAB build uses a different barcode.
        private const string NullRatBarcode = "c1534c5a-ef15-44c0-88ae-aebc4e756c6c";
        private const int MaxRoseQuantityPerMessage = 10;

        private readonly ConcurrentQueue<int> _pendingRoseCounts = new ConcurrentQueue<int>();
        private TcpListener _listener;
        private Thread _listenerThread;
        private volatile bool _stopping;
        private bool _spawnFailureLogged;
        private bool _boneMenuReady;
        private Page _boneMenuPage;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("[Null Rat Rose] Starting local gift bridge on TCP port " + BridgePort + ".");
            MelonLogger.Msg("[Null Rat Rose] Creating BoneMenu test controls.");
            SetupBoneMenu();

            try
            {
                _listener = new TcpListener(IPAddress.Any, BridgePort);
                _listener.Start(8);
                _listenerThread = new Thread(ListenForBridgeMessages)
                {
                    IsBackground = true,
                    Name = "NullRatRose-TCP"
                };
                _listenerThread.Start();
                MelonLogger.Msg("[Null Rat Rose] Bridge is listening. Start the TikTok listener on a PC on the same Wi-Fi.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[Null Rat Rose] Could not start the local bridge: " + ex.Message);
            }
        }

        private void SetupBoneMenu()
        {
            try
            {
                if (Page.Root == null)
                    return;

                _boneMenuPage = Page.Root.CreatePage("Null Rat Rose", Color.magenta);
                _boneMenuPage.CreateFunction("TEST: Spawn 1 Null Rat", Color.green, new Action(() =>
                {
                    MelonLogger.Msg("[Null Rat Rose] Manual single-rat test requested.");
                    SpawnNullRat();
                }));
                _boneMenuPage.CreateFunction("TEST: Spawn 5 Null Rats", Color.cyan, new Action(() =>
                {
                    MelonLogger.Msg("[Null Rat Rose] Manual five-rat test requested.");
                    for (int i = 0; i < 5; i++)
                        SpawnNullRat();
                }));
                _boneMenuPage.CreateFunction("TEST: Simulate Rose (5 Rats)", Color.yellow, new Action(() =>
                {
                    MelonLogger.Msg("[Null Rat Rose] Simulated Rose gift queued.");
                    _pendingRoseCounts.Enqueue(1);
                }));

                _boneMenuReady = true;
                MelonLogger.Msg("[Null Rat Rose] BoneMenu page added with three test buttons.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[Null Rat Rose] Failed to create BoneMenu page. Confirm BoneLib v3.x is installed. " + ex);
            }
        }

        public override void OnUpdate()
        {
            // BoneLib's menu root may be initialized after this mod, so retry until the menu exists.
            if (!_boneMenuReady && Page.Root != null)
                SetupBoneMenu();

            // Unity/Marrow spawning must happen on the game thread, not the TCP worker thread.
            if (_pendingRoseCounts.TryDequeue(out int roseCount))
            {
                roseCount = Mathf.Clamp(roseCount, 1, MaxRoseQuantityPerMessage);
                for (int rose = 0; rose < roseCount; rose++)
                {
                    for (int rat = 0; rat < 5; rat++)
                    {
                        SpawnNullRat();
                    }
                }
                MelonLogger.Msg("[Null Rat Rose] Processed " + roseCount + " Rose gift(s): requested " + (roseCount * 5) + " Null Rats.");
            }
        }

        public override void OnDeinitializeMelon()
        {
            _stopping = true;
            try { _listener?.Stop(); } catch { }
            try
            {
                if (_listenerThread != null && _listenerThread.IsAlive)
                    _listenerThread.Join(500);
            }
            catch { }
            MelonLogger.Msg("[Null Rat Rose] Bridge stopped.");
        }

        private void ListenForBridgeMessages()
        {
            while (!_stopping)
            {
                try
                {
                    using TcpClient client = _listener.AcceptTcpClient();
                    client.ReceiveTimeout = 3000;
                    client.SendTimeout = 3000;
                    using NetworkStream stream = client.GetStream();
                    using var reader = new System.IO.StreamReader(stream, Encoding.UTF8, false, 256, true);
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] parts = line.Trim().Split('|');
                    if (parts.Length != 3 || !string.Equals(parts[0], "ROSE", StringComparison.Ordinal))
                        continue;
                    if (!SecretsMatch(parts[1], SharedSecret))
                    {
                        MelonLogger.Warning("[Null Rat Rose] Rejected bridge message: secret did not match.");
                        continue;
                    }
                    if (!int.TryParse(parts[2], out int quantity))
                        continue;

                    quantity = Mathf.Clamp(quantity, 1, MaxRoseQuantityPerMessage);
                    _pendingRoseCounts.Enqueue(quantity);
                    byte[] response = Encoding.UTF8.GetBytes("OK\n");
                    stream.Write(response, 0, response.Length);
                }
                catch (SocketException) when (_stopping)
                {
                    break;
                }
                catch (ObjectDisposedException) when (_stopping)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!_stopping)
                        MelonLogger.Warning("[Null Rat Rose] Bridge connection problem: " + ex.Message);
                }
            }
        }

        private static bool SecretsMatch(string supplied, string expected)
        {
            if (supplied == null || expected == null)
                return false;
            byte[] a = Encoding.UTF8.GetBytes(supplied);
            byte[] b = Encoding.UTF8.GetBytes(expected);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }

        private void SpawnNullRat()
        {
            try
            {
                Camera camera = Camera.main;
                if (camera == null)
                    throw new InvalidOperationException("Camera.main is not available yet. Load into a level first.");

                Vector3 origin = camera.transform.position + camera.transform.forward * 2.0f;
                Vector3 offset = UnityEngine.Random.insideUnitSphere * 1.6f;
                offset.y = UnityEngine.Random.Range(-0.4f, 1.0f);
                Vector3 position = origin + offset;

                // Resolve BoneLib's public SpawnCrate(string, Vector3, Quaternion, Vector3, bool,
                // Action<GameObject>, Action<GameObject>) overload at runtime so CI doesn't need
                // proprietary Il2CppSLZ.Marrow game assemblies.
                Type helperType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("BoneLib.HelperMethods", false))
                    .FirstOrDefault(type => type != null);

                if (helperType == null)
                    throw new InvalidOperationException("BoneLib.HelperMethods was not found. Install BoneLib v3.2.2.");

                MethodInfo spawnCrate = helperType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(method =>
                    {
                        if (method.Name != "SpawnCrate")
                            return false;
                        ParameterInfo[] parameters = method.GetParameters();
                        return parameters.Length == 7 &&
                               parameters[0].ParameterType == typeof(string) &&
                               parameters[1].ParameterType == typeof(Vector3) &&
                               parameters[2].ParameterType == typeof(Quaternion) &&
                               parameters[3].ParameterType == typeof(Vector3) &&
                               parameters[4].ParameterType == typeof(bool) &&
                               parameters[5].ParameterType == typeof(Action<GameObject>) &&
                               parameters[6].ParameterType == typeof(Action<GameObject>);
                    });

                if (spawnCrate == null)
                    throw new MissingMethodException("BoneLib SpawnCrate(string, Vector3, Quaternion, Vector3, bool, Action<GameObject>, Action<GameObject>) was not found.");

                spawnCrate.Invoke(null, new object[]
                {
                    NullRatBarcode,
                    position,
                    Quaternion.identity,
                    Vector3.one,
                    false,
                    new Action<GameObject>(ApplyRandomColor),
                    null
                });

                MelonLogger.Msg("[Null Rat Rose] Null Rat spawn request submitted at " + position + ". Barcode: " + NullRatBarcode);
            }
            catch (Exception ex)
            {
                if (!_spawnFailureLogged)
                {
                    _spawnFailureLogged = true;
                    MelonLogger.Error("[Null Rat Rose] Spawn failed. Make sure a level is loaded and BoneLib is installed. " + ex);
                }
            }
        }

        private void ApplyRandomColor(GameObject spawnedObject)
        {
            if (spawnedObject == null)
                return;

            Color tint = Color.HSVToRGB(UnityEngine.Random.value, 0.85f, 1f);
            try
            {
                var renderers = spawnedObject.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;
                    try
                    {
                        var materials = renderer.materials;
                        foreach (Material material in materials)
                        {
                            if (material != null)
                                material.color = tint;
                        }
                    }
                    catch
                    {
                        try { renderer.material.color = tint; } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[Null Rat Rose] The rat spawned, but its materials could not be tinted: " + ex.Message);
            }
        }
    }
}
