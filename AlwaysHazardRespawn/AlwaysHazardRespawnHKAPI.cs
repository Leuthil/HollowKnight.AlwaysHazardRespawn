using Modding;
using System;
using System.Collections;
using UnityEngine;

namespace AlwaysHazardRespawn
{
    public class AlwaysHazardRespawnHKAPI : Mod, ITogglableMod, IModClient
    {
        private AlwaysHazardRespawn mod;

        public event Action OnBeforeSavegameSave;
        public event Action OnAfterSavegameSave;
        public event Action<PlayerData> OnAfterSavegameLoad;
        public event Action<Vector3, bool> OnHazardRespawnLocationSet;
        public event Action OnBeforePlayerDead;
        public event Action OnFinishedSceneTransition;

        public override string GetVersion() => "0.2.0.0";

        public override void Initialize()
        {
            On.GameManager.SaveGame_int_Action1 += OnGameManagerSaveGameHook;
            On.GameManager.LoadGame += OnGameManagerLoadGameHook;
            On.PlayerData.SetHazardRespawn_HazardRespawnMarker += OnPlayerDataSetHazardRespawnHook_HazardRespawnMarker;
            On.PlayerData.SetHazardRespawn_Vector3_bool += OnPlayerDataSetHazardRespawnHook_Position;
            On.GameManager.PlayerDead += OnGameManagerPlayerDeadHook;

            GameManager.instance.OnFinishedSceneTransition += OnFinishedSceneTransitionHook;

            mod ??= new AlwaysHazardRespawn(this);
            mod.Load();
        }

        public void Unload()
        {
            if (mod != null)
            {
                mod.Unload();
                mod = null;
            }

            On.GameManager.SaveGame_int_Action1 -= OnGameManagerSaveGameHook;
            On.GameManager.LoadGame -= OnGameManagerLoadGameHook;
            On.PlayerData.SetHazardRespawn_HazardRespawnMarker -= OnPlayerDataSetHazardRespawnHook_HazardRespawnMarker;
            On.PlayerData.SetHazardRespawn_Vector3_bool -= OnPlayerDataSetHazardRespawnHook_Position;
            On.GameManager.PlayerDead -= OnGameManagerPlayerDeadHook;

            GameManager.instance.OnFinishedSceneTransition -= OnFinishedSceneTransitionHook;
        }

        private void OnGameManagerSaveGameHook(On.GameManager.orig_SaveGame_int_Action1 orig, GameManager self, int saveSlot, Action<bool> callback)
        {
            if (saveSlot >= 0 && !self.gameConfig.disableSaveGame)
            {
                OnBeforeSavegameSave?.Invoke();
            }

            orig(self, saveSlot, callback);

            if (saveSlot >= 0 && !self.gameConfig.disableSaveGame)
            {
                OnAfterSavegameSave?.Invoke();
            }
        }

        private void OnGameManagerLoadGameHook(On.GameManager.orig_LoadGame orig, GameManager self, int saveSlot, Action<bool> callback)
        {
            orig(self, saveSlot, callback);

            OnAfterSavegameLoad?.Invoke(PlayerData.instance);
        }

        private void OnPlayerDataSetHazardRespawnHook_HazardRespawnMarker(On.PlayerData.orig_SetHazardRespawn_HazardRespawnMarker orig, PlayerData self, HazardRespawnMarker location)
        {
            orig(self, location);

            HandleHazardRespawnLocationSet(location.transform.position, location.respawnFacingRight);
        }

        private void OnPlayerDataSetHazardRespawnHook_Position(On.PlayerData.orig_SetHazardRespawn_Vector3_bool orig, PlayerData self, Vector3 position, bool facingRight)
        {
            orig(self, position, facingRight);

            HandleHazardRespawnLocationSet(position, facingRight);
        }

        private void HandleHazardRespawnLocationSet(Vector3 position, bool facingRight)
        {
            OnHazardRespawnLocationSet?.Invoke(position, facingRight);
        }

        private IEnumerator OnGameManagerPlayerDeadHook(On.GameManager.orig_PlayerDead orig, GameManager self, float waitTime)
        {
            OnBeforePlayerDead?.Invoke();

            yield return orig(self, waitTime);
        }

        private void OnFinishedSceneTransitionHook()
        {
            OnFinishedSceneTransition?.Invoke();
        }
    }
}
