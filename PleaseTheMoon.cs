using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using PleaseTheMoon.Patches;
using System;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

namespace PleaseTheMoon
{
    [BepInPlugin("Neksuro.PleaseTheMoon", "Please The Moon", "1.0.0")]
    public class PleaseTheMoonPlugin : BaseUnityPlugin, IOnEventCallback
    {
        public static ConfigEntry<bool> EnableMod { get; private set; }
        public static ConfigEntry<bool> EnableRandomSpawn { get; private set; }
        public static ConfigEntry<int> SpawnChance { get; private set; }

        private const byte SyncSpawnEventCode = 181;

        public static bool canSpawn;

        private Harmony harmony;

        private void Awake()
        {
            EnableMod = Config.Bind(
                "Spawn Settings",
                "Enable Extraction Pillars",
                true,
                "Enables the Halloween extraction pillars."
            );
            EnableRandomSpawn = Config.Bind(
                "Spawn Settings",
                "Enable Random Spawn",
                false,
                "Enables the random spawn chance of the extraction pillars on each level."
            );
            SpawnChance = Config.Bind(
                "Spawn Settings",
                "Spawn Chance",
                50,
                new ConfigDescription(
                "Percent chance of the extraction pillars spawning on each level.",
                new AcceptableValueRange<int>(0, 100)
                )
            );

            Patch();

            Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} is loaded!");

            SceneManager.sceneLoaded += OnSceneLoaded;
            PhotonNetwork.AddCallbackTarget(this);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            PhotonNetwork.RemoveCallbackTarget(this);
        }

        private void Patch()
        {
            harmony = new Harmony(Info.Metadata.GUID);
            harmony.PatchAll();

            // Apply universal prefix/postfix to all methods of classes associated with the extraction point
            PatchAllMethodsOnType(typeof(ExtractionPoint));
            PatchAllMethodsOnType(typeof(ExtractionHalloweenAudio));
            PatchAllMethodsOnType(typeof(ExtractionHalloweenBeams));
            PatchAllMethodsOnType(typeof(ExtractionHalloweenContact));
            PatchAllMethodsOnType(typeof(ExtractionHalloweenPillars));
            PatchAllMethodsOnType(typeof(ExtractionHalloweenPumpkin));
            PatchAllMethodsOnType(typeof(ExtractionHalloweenReward));
            PatchAllMethodsOnType(typeof(ExtractionHalloweenTower));
            PatchAllMethodsOnType(typeof(ValuableObject));
            PatchAllMethodsOnType(typeof(PhysGrabObject));
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!SemiFunc.IsMasterClientOrSingleplayer())
                return;

            if (SemiFunc.RunIsLevel() && scene.name != "Reload")
            {
                SafeReloadConfig();
                canSpawn = DetermineSpawnOnLevel();
                SpawnPillarsRPC(SemiFunc.IsMultiplayer());
            }
        }
        private void SafeReloadConfig()
        {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("nickklmao.repoconfig"))
            {
                Logger.LogDebug($"Detected REPOConfig mod; skipping config reload.");
                return;
            }

            bool previousSaveState = Config.SaveOnConfigSet;
            try
            {
                Config.SaveOnConfigSet = false;
                Config.Reload();
                Logger.LogDebug("Config reloaded on scene load.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to reload config on scene load: {ex}");
            }
            finally
            {
                Config.SaveOnConfigSet = previousSaveState;
            }
        }

        private bool DetermineSpawnOnLevel()
        {
            if (!EnableMod.Value)
                return false;

            if (EnableRandomSpawn.Value)
            {
                var spawn = UnityEngine.Random.Range(1, 101);
                if (spawn <= SpawnChance.Value)
                {
                    Logger.LogInfo("Spawning Halloween extraction pillars on map!");
                    return true;
                }
                else
                {
                    return false;
                }
            }
            return true;
        }

        private void SpawnPillarsRPC(bool multiplayer)
        {
            if (!multiplayer)
                return;

            PhotonNetwork.RaiseEvent(
                SyncSpawnEventCode,
                null,
                new RaiseEventOptions { CachingOption = EventCaching.RemoveFromRoomCache },
                SendOptions.SendReliable
            );

            object[] content = new object[] { canSpawn };
            RaiseEventOptions options = new RaiseEventOptions
            {
                Receivers = ReceiverGroup.Others,
                CachingOption = EventCaching.AddToRoomCache
            };

            PhotonNetwork.RaiseEvent(SyncSpawnEventCode, content, options, SendOptions.SendReliable);

            Logger.LogDebug($"[Network] Sending spawn results to lobby: {canSpawn}");
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code != SyncSpawnEventCode)
                return;

            if (photonEvent.CustomData is object[] data && data.Length > 0 && data[0] is bool receivedFlag)
            {
                canSpawn = receivedFlag;
                Logger.LogDebug($"[Network] Received spawn results from host.");
                if (canSpawn) Logger.LogInfo("[Network] Lobby host spawned Halloween extraction pillars on map!");
            }
        }

        private void PatchAllMethodsOnType(Type targetType)
        {
            var prefix = new HarmonyMethod(typeof(ExtractionPatches), nameof(ExtractionPatches.Prefix));
            var postfix = new HarmonyMethod(typeof(ExtractionPatches), nameof(ExtractionPatches.Postfix));

            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(targetType))
            {
                if (method.IsAbstract || method.ContainsGenericParameters || method.IsGenericMethod)
                    continue;

                try
                {
                    harmony.Patch(method, prefix: prefix, postfix: postfix);
                }
                catch (Exception ex)
                {
                    Logger.LogDebug($"Skipped {targetType.Name}.{method.Name}: {ex.Message}");
                }
            }
        }
        public static class TrackedExecutionContext
        {
            [ThreadStatic]
            private static int _callDepth;

            public static bool IsActive => _callDepth > 0;

            public static void Enter() => _callDepth++;

            public static void Exit()
            {
                if (_callDepth > 0)
                    _callDepth--;
            }
        }
    }
}