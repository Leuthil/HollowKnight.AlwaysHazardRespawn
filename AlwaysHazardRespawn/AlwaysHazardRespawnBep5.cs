using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using UnityEngine;

namespace AlwaysHazardRespawn
{
    [BepInPlugin("com.leuthil.alwayshazardrespawn", "Always Hazard Respawn", "0.2.0.0")]
    public class AlwaysHazardRespawnBep5 : BaseUnityPlugin, IModClient
    {
        public static AlwaysHazardRespawnBep5 instance;

        public event Action OnBeforeSavegameSave;
        public event Action OnAfterSavegameSave;
        public event Action<PlayerData> OnAfterSavegameLoad;
        public event Action<Vector3, bool> OnHazardRespawnLocationSet;
        public event Action OnBeforePlayerDead;
        public event Action OnFinishedSceneTransition;

        private AlwaysHazardRespawn mod;
        private Harmony harmony;
        private Coroutine waitForGameManagerCoroutine;

        private void Awake()
        {
            Logger.LogDebug("AlwaysHazardRespawnBep5 Awake");

            if (instance != null)
            {
                Destroy(this);
                return;
            }

            instance = this;

            harmony ??= new Harmony("com.leuthil.alwayshazardrespawn");
            PatchRequiredTypes(harmony);

            waitForGameManagerCoroutine = StartCoroutine(WaitForGameManagerInstance((gameManager) =>
            {
                gameManager.OnFinishedSceneTransition += OnFinishedSceneTransitionHook;
            }));

            mod ??= new AlwaysHazardRespawn(this);
            mod.Load();
        }

        private void OnDestroy()
        {
            Logger.LogDebug("AlwaysHazardRespawnBep5 OnDestroy");

            if (harmony != null)
            {
                harmony.UnpatchSelf();
                harmony = null;
            }

            if (waitForGameManagerCoroutine != null)
            {
                StopCoroutine(waitForGameManagerCoroutine);
                waitForGameManagerCoroutine = null;
            }

            if (GameManager.instance != null)
            {
                GameManager.instance.OnFinishedSceneTransition -= OnFinishedSceneTransitionHook;
            }

            if (mod != null)
            {
                mod.Unload();
                mod = null;
            }
        }

        IEnumerator WaitForGameManagerInstance(Action<GameManager> action)
        {
            while (GameManager.instance == null)
            {
                yield return null;
            }

            action(GameManager.instance);
        }

        private void PatchRequiredTypes(Harmony harmony)
        {
            // List all types that require Harmony patches manually.
            // If we patch all types, we will get warnings about HK API types.
            Type[] types = [
                typeof(GM_SaveGame_Patch),
                typeof(GM_LoadGame_Patch),
                typeof(PD_SetHazardRespawnLocation_HazardRespawnMarker_Patch),
                typeof(PD_SetHazardRespawnLocation_Position_Patch),
                typeof(GM_PlayerDead_Patch),
                ];

            foreach (Type type in types)
            {
                harmony.CreateClassProcessor(type).Patch();
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SaveGame))]
        [HarmonyPatch(new Type[] { typeof(int), typeof(Action<bool>) })]
        class GM_SaveGame_Patch
        {
            static void Prefix(int saveSlot, GameManager __instance)
            {
                if (saveSlot >= 0 && !__instance.gameConfig.disableSaveGame)
                {
                    instance.OnBeforeSavegameSave?.Invoke();
                }
            }

            static void Postfix(int saveSlot, GameManager __instance)
            {
                if (saveSlot >= 0 && !__instance.gameConfig.disableSaveGame)
                {
                    instance.OnAfterSavegameSave?.Invoke();
                }
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadGame))]
        [HarmonyPatch(new Type[] { typeof(int), typeof(Action<bool>) })]
        class GM_LoadGame_Patch
        {
            static void Postfix()
            {
                instance.OnAfterSavegameLoad?.Invoke(PlayerData.instance);
            }
        }

        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetHazardRespawn))]
        [HarmonyPatch(new Type[] { typeof(HazardRespawnMarker) })]
        class PD_SetHazardRespawnLocation_HazardRespawnMarker_Patch
        {
            static void Postfix(HazardRespawnMarker location)
            {
                instance.HandleHazardRespawnLocationSet(location.transform.position, location.respawnFacingRight);
            }
        }

        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.SetHazardRespawn))]
        [HarmonyPatch(new Type[] { typeof(Vector3), typeof(bool) })]
        class PD_SetHazardRespawnLocation_Position_Patch
        {
            static void Postfix(Vector3 position, bool facingRight)
            {
                instance.HandleHazardRespawnLocationSet(position, facingRight);
            }
        }

        private void HandleHazardRespawnLocationSet(Vector3 position, bool facingRight)
        {
            OnHazardRespawnLocationSet?.Invoke(position, facingRight);
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.PlayerDead))]
        [HarmonyPatch(new Type[] { typeof(float) })]
        class GM_PlayerDead_Patch
        {
            static void Prefix(GameManager __instance, float waitTime, ref IEnumerator __result)
            {
                instance.OnBeforePlayerDead?.Invoke();
            }
        }

        private void OnFinishedSceneTransitionHook()
        {
            OnFinishedSceneTransition?.Invoke();
        }

        public void Log(string msg)
        {
            Logger.LogInfo(msg);
        }

        public void LogDebug(string msg)
        {
            Logger.LogDebug(msg);
        }
    }
}
