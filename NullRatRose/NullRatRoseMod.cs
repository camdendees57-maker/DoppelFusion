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
        private const string NullRatBarcode = "SLZ.BONELAB.Content.Spawnable.NullRat";
        private const int MaxRoseQuantityPerMessage = 10;

        private readonly ConcurrentQueue<int> _pendingRoseCounts = new ConcurrentQueue<int>();
        private TcpListener _listener;
        private Thread _listenerThread;
        private volatile bool _stopping;
        private Type _assetSpawnerType;
        private Type _spawnableType;
        private Type _crateReferenceType;
        private bool _spawnApiReady;
        private bool _spawnApiErrorLogged;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("[Null Rat Rose] Starting local gift bridge on TCP port " + BridgePort + ".");
            MelonLogger.Warning("[Null Rat Rose] Change SharedSecret in NullRatRoseMod.cs and TikTokBridge/.env before streaming.");

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

        public override void OnUpdate()
        {
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
                if (!_spawnApiReady && !TryResolveSpawnApi())
                    return;

                Camera camera = Camera.main;
                Vector3 origin = camera != null
                    ? camera.transform.position + camera.transform.forward * 2.0f
                    : new Vector3(0f, 1.5f, 2.0f);

                Vector3 offset = UnityEngine.Random.insideUnitSphere * 1.6f;
                offset.y = UnityEngine.Random.Range(-0.4f, 1.0f);
                Vector3 position = origin + offset;

                ConstructorInfo constructor = _crateReferenceType
                    .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .FirstOrDefault(c =>
                    {
                        ParameterInfo[] p = c.GetParameters();
                        return p.Length == 1 && p[0].ParameterType == typeof(string);
                    });

                if (constructor == null)
                    throw new MissingMethodException("Could not find SpawnableCrateReference(string).");

                object crateReference = constructor.Invoke(new object[] { NullRatBarcode });
                object spawnable = Activator.CreateInstance(_spawnableType);
                SetMember(spawnable, "crateRef", crateReference);

                MethodInfo register = _assetSpawnerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Register" &&
                                         m.GetParameters().Length == 1 &&
                                         m.GetParameters()[0].ParameterType.IsAssignableFrom(_spawnableType));
                register?.Invoke(null, new[] { spawnable });

                MethodInfo spawn = _assetSpawnerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .Where(m => m.Name == "Spawn")
                    .Where(m =>
                    {
                        ParameterInfo[] p = m.GetParameters();
                        return p.Length >= 7 &&
                               p[0].ParameterType.IsAssignableFrom(_spawnableType) &&
                               p[1].ParameterType == typeof(Vector3) &&
                               p[2].ParameterType == typeof(Quaternion);
                    })
                    .OrderByDescending(m => m.GetParameters().Length)
                    .FirstOrDefault();

                if (spawn == null)
                    throw new MissingMethodException("Could not find a compatible AssetSpawner.Spawn overload.");

                ParameterInfo[] parameters = spawn.GetParameters();
                object[] args = new object[parameters.Length];
                args[0] = spawnable;
                args[1] = position;
                args[2] = Quaternion.identity;

                bool callbackAssigned = false;
                MethodInfo callbackMethod = GetType().GetMethod(
                    nameof(ApplyRandomColor),
                    BindingFlags.Instance | BindingFlags.NonPublic);

                for (int i = 3; i < parameters.Length; i++)
                {
                    Type parameterType = parameters[i].ParameterType;

                    if (parameterType == typeof(bool))
                    {
                        args[i] = false;
                    }
                    else if (typeof(Delegate).IsAssignableFrom(parameterType))
                    {
                        Delegate callback = null;
                        MethodInfo invoke = parameterType.GetMethod("Invoke");
                        ParameterInfo[] callbackParameters = invoke?.GetParameters() ?? Array.Empty<ParameterInfo>();
                        if (!callbackAssigned && callbackParameters.Length == 1 &&
                            callbackParameters[0].ParameterType == typeof(GameObject))
                        {
                            try
                            {
                                callback = Delegate.CreateDelegate(parameterType, this, callbackMethod, false);
                            }
                            catch { }
                            if (callback != null)
                                callbackAssigned = true;
                        }
                        args[i] = callback;
                    }
                    else if (parameterType.IsValueType)
                    {
                        args[i] = Activator.CreateInstance(parameterType);
                    }
                    else
                    {
                        args[i] = null;
                    }
                }

                spawn.Invoke(null, args);
            }
            catch (Exception ex)
            {
                if (!_spawnApiErrorLogged)
                {
                    _spawnApiErrorLogged = true;
                    MelonLogger.Error("[Null Rat Rose] Spawn failed. Check the Null Rat barcode and BONELAB build compatibility. " + ex);
                }
            }
        }

        private bool TryResolveSpawnApi()
        {
            _assetSpawnerType = FindLoadedType("AssetSpawner");
            _spawnableType = FindLoadedType("Spawnable");
            _crateReferenceType = FindLoadedType("SpawnableCrateReference");

            _spawnApiReady = _assetSpawnerType != null && _spawnableType != null && _crateReferenceType != null;
            if (!_spawnApiReady && !_spawnApiErrorLogged)
            {
                _spawnApiErrorLogged = true;
                MelonLogger.Error("[Null Rat Rose] Could not find BONELAB's Marrow spawn types. Wait until a level is loaded, then try another Rose.");
            }
            return _spawnApiReady;
        }

        private static Type FindLoadedType(string simpleName)
        {
            string[] candidates =
            {
                "Il2CppSLZ.Marrow." + simpleName,
                "Il2CppSLZ.Marrow.Data." + simpleName,
                "Il2CppSLZ.Marrow.Warehouse." + simpleName,
                "Il2CppSLZ.Bonelab." + simpleName
            };

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                foreach (string candidate in candidates)
                {
                    try
                    {
                        Type type = assembly.GetType(candidate, false);
                        if (type != null)
                            return type;
                    }
                    catch { }
                }
            }

            foreach (Assembly assembly in assemblies)
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch
                {
                    continue;
                }

                Type found = types.FirstOrDefault(t => t != null && t.Name == simpleName);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static void SetMember(object target, string name, object value)
        {
            if (target == null)
                throw new InvalidOperationException("Cannot set " + name + " on a null object.");

            Type type = target.GetType();
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
                return;
            }

            PropertyInfo property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return;
            }

            throw new MissingMemberException(type.FullName, name);
        }

        private void ApplyRandomColor(GameObject spawnedObject)
        {
            if (spawnedObject == null)
                return;

            Color tint = Color.HSVToRGB(UnityEngine.Random.value, 0.85f, 1f);
            try
            {
                Renderer[] renderers = spawnedObject.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer == null)
                        continue;
                    try
                    {
                        Material[] materials = renderer.materials;
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
