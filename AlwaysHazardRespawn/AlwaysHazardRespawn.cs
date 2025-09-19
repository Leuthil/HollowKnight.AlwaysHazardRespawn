using Modding;
using System;
using UnityEngine;

namespace AlwaysHazardRespawn
{
    public class AlwaysHazardRespawn : Mod, ITogglableMod
    {
        public override string GetVersion() => "0.1.0.0";

        private const string DYNAMIC_HAZARD_RESPAWN_MARKER_NAME = "__DYNAMIC_HAZARD_RESPAWN__";
        private const string RESPAWN_POINT_TAG = "RespawnPoint";

        private static HazardRespawnData _hazardRespawnData;

        private static string _prevRespawnSceneName;
        private static string _prevRespawnMarkerName;

        private static bool _isRespawningAfterDeath;
        private static bool _isAlteringRespawnData;

        private static GameObject _dynamicHazardRespawnMarkerGO;

        public override void Initialize()
        {
            LogDebug("Initializing AlwaysHazardRespawn");

            ModHooks.BeforePlayerDeadHook += OnBeforePlayerDeadHook;
            ModHooks.SetPlayerVector3Hook += OnSetPlayerVector3Hook;
            ModHooks.SetPlayerStringHook += OnSetPlayerStringHook;
            ModHooks.BeforeSavegameSaveHook += OnBeforeSavegameSaveHook;
            ModHooks.SavegameSaveHook += OnAfterSavegameSaveHook;
            ModHooks.AfterSavegameLoadHook += OnAfterSavegameLoadHook;
            GameManager.instance.OnFinishedSceneTransition += OnFinishedSceneTransition;

            // Create a GameObject to serve as the dynamic hazard respawn marker.
            // During respawn, the game looks for all GameObjects with the "RespawnPoint" tag in the scene (GameObject.FindGameObjectsWithTag("RespawnPoint")).
            // From those, it finds the one with the matching name (gameObject.name == PlayerData.instance.GetString("respawnMarkerName")).
            _dynamicHazardRespawnMarkerGO = new GameObject(DYNAMIC_HAZARD_RESPAWN_MARKER_NAME);
            // Tag it as a respawn point so that the game recognizes it as such.
            _dynamicHazardRespawnMarkerGO.tag = RESPAWN_POINT_TAG;
            GameObject.DontDestroyOnLoad(_dynamicHazardRespawnMarkerGO);
        }

        public void Unload()
        {
            LogDebug("Unloading AlwaysHazardRespawn");

            // If we are in the middle of a respawn, restore the respawn data to avoid corrupting future saves.
            // This shouldn't happen since you can't go to the menu during death, but just in case.
            if (_isRespawningAfterDeath)
            {
                RestoreRespawnData();
            }

            ModHooks.BeforePlayerDeadHook -= OnBeforePlayerDeadHook;
            ModHooks.SetPlayerVector3Hook -= OnSetPlayerVector3Hook;
            ModHooks.SetPlayerStringHook -= OnSetPlayerStringHook;
            ModHooks.BeforeSavegameSaveHook -= OnBeforeSavegameSaveHook;
            ModHooks.SavegameSaveHook -= OnAfterSavegameSaveHook;
            GameManager.instance.OnFinishedSceneTransition -= OnFinishedSceneTransition;

            if (_dynamicHazardRespawnMarkerGO != null)
            {
                GameObject.Destroy(_dynamicHazardRespawnMarkerGO);
                _dynamicHazardRespawnMarkerGO = null;
            }

            _hazardRespawnData = null;
            _prevRespawnSceneName = null;
            _prevRespawnMarkerName = null;
            _isRespawningAfterDeath = false;
            _isAlteringRespawnData = false;
        }

        private void OnBeforeSavegameSaveHook(SaveGameData data)
        {
            if (!_isRespawningAfterDeath)
            {
                return;
            }

            // Restore respawn data before saving so that the save does not get corrupted.
            RestoreRespawnData();
        }

        private void OnAfterSavegameSaveHook(int saveSlot)
        {
            if (!_isRespawningAfterDeath)
            {
                return;
            }

            // Overwrite respawn data after saving to correctly hijack the death respawn.
            OverwriteRespawnData();
        }

        private void OnAfterSavegameLoadHook(SaveGameData data)
        {
            // Preserve the original spawn data so we can restore it when necessary.
            _prevRespawnSceneName = data.playerData.GetString("respawnScene");
            _prevRespawnMarkerName = data.playerData.GetString("respawnMarkerName");

            LogDebug($"Preserving prevRespawnScene={_prevRespawnSceneName}, prevRespawnMarkerName={_prevRespawnMarkerName}");
        }

        private Vector3 OnSetPlayerVector3Hook(string name, Vector3 orig)
        {
            switch (name)
            {
                // Capture the hazard respawn data whenever it is set.
                case "hazardRespawnLocation":
                    CaptureRespawnData(orig);
                    break;
                default:
                    break;
            }
            return orig;
        }

        private string OnSetPlayerStringHook(string name, string orig)
        {
            // During respawn data hijacking, ignore changes.
            if (_isAlteringRespawnData)
            {
                return orig;
            }

            switch (name)
            {
                // Preserve the original respawn data so we can restore it when necessary.
                case "respawnScene":
                    _prevRespawnSceneName = orig;
                    LogDebug($"Preserving prevRespawnScene: {_prevRespawnSceneName}");
                    break;
                case "respawnMarkerName":
                    _prevRespawnMarkerName = orig;
                    LogDebug($"Preserving prevRespawnMarkerName: {_prevRespawnMarkerName}");
                    break;
            }

            return orig;
        }

        private void OnBeforePlayerDeadHook()
        {
            // Do not consider this death if we don't have hazard respawn data. This should rarely be the case.
            if (_hazardRespawnData == null)
            {
                return;
            }

            _isRespawningAfterDeath = true;

            // Overwrite respawn data to hijack the death respawn.
            OverwriteRespawnData();
        }

        private void OnFinishedSceneTransition()
        {
            if (_isRespawningAfterDeath)
            {
                _isRespawningAfterDeath = false;
                // Restore respawn data after the respawn is complete. This is important to avoid corrupting future saves.
                RestoreRespawnData();
            }
        }

        private void CaptureRespawnData(Vector3 hazardRespawnLocation)
        {
            LogDebug($"Capturing hazard respawn scene and location: {GameManager.instance.sceneName}, {hazardRespawnLocation}");

            _hazardRespawnData = new HazardRespawnData
            {
                SceneName = GameManager.instance.sceneName,
                Location = hazardRespawnLocation
            };

            // Move the dynamic hazard respawn marker GameObject to the captured location.
            _dynamicHazardRespawnMarkerGO.transform.position = _hazardRespawnData.Location;
        }

        private void OverwriteRespawnData()
        {
            LogDebug($"Overwriting respawn data prevRespawnScene={_prevRespawnSceneName}, prevRespawnMarkerName={_prevRespawnMarkerName}" +
                $" with newRespawnScene={_hazardRespawnData.SceneName}, newRespawnMarkerName={DYNAMIC_HAZARD_RESPAWN_MARKER_NAME}");

            _isAlteringRespawnData = true;
            PlayerData.instance.SetString("respawnScene", _hazardRespawnData.SceneName);
            PlayerData.instance.SetString("respawnMarkerName", DYNAMIC_HAZARD_RESPAWN_MARKER_NAME);
            _isAlteringRespawnData = false;
        }

        private void RestoreRespawnData()
        {
            LogDebug($"Restoring respawn data to prevRespawnScene={_prevRespawnSceneName}, prevRespawnMarkerName={_prevRespawnMarkerName}");

            _isAlteringRespawnData = true;
            PlayerData.instance.SetString("respawnScene", _prevRespawnSceneName);
            PlayerData.instance.SetString("respawnMarkerName", _prevRespawnMarkerName);
            _isAlteringRespawnData = false;
        }

        private class HazardRespawnData
        {
            public string SceneName;
            public Vector3 Location;
        }
    }
}
