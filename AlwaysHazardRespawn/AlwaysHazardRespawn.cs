using System;
using UnityEngine;

namespace AlwaysHazardRespawn
{
    public class AlwaysHazardRespawn
    {
        private const string DYNAMIC_HAZARD_RESPAWN_MARKER_NAME = "__DYNAMIC_HAZARD_RESPAWN__";
        private const string RESPAWN_POINT_TAG = "RespawnPoint";

        private readonly IModClient modClient;

        private RespawnData dynamicHazardRespawnData;
        private RespawnData originalRespawnData;

        private bool isRespawningAfterDeath;

        private GameObject dynamicHazardRespawnMarkerGO;
        private RespawnMarker dynamicHazardRespawnMarker;

        public AlwaysHazardRespawn(IModClient modClient)
        {
            this.modClient = modClient ?? throw new ArgumentNullException(nameof(modClient));
        }

        public void Load()
        {
            modClient.LogDebug("Loading AlwaysHazardRespawn");

            modClient.OnBeforeSavegameSave += OnBeforeSavegameSave;
            modClient.OnAfterSavegameSave += OnAfterSavegameSave;
            modClient.OnAfterSavegameLoad += OnAfterSavegameLoad;
            modClient.OnHazardRespawnLocationSet += OnHazardRespawnLocationSet;
            modClient.OnBeforePlayerDead += OnBeforePlayerDead;
            modClient.OnFinishedSceneTransition += OnFinishedSceneTransition;

            // Create a GameObject to serve as the dynamic hazard respawn marker.
            // During respawn, the game looks for all GameObjects with the "RespawnPoint" tag in the scene (GameObject.FindGameObjectsWithTag("RespawnPoint")).
            // From those, it finds the one with the matching name (gameObject.name == PlayerData.instance.GetString("respawnMarkerName")).
            dynamicHazardRespawnMarkerGO = new GameObject(DYNAMIC_HAZARD_RESPAWN_MARKER_NAME);
            // Tag it as a respawn point so that the game recognizes it as such.
            dynamicHazardRespawnMarkerGO.tag = RESPAWN_POINT_TAG;
            // Add RespawnMarker component has its used in spawning (prevents issues and contains the facing direction).
            dynamicHazardRespawnMarker = dynamicHazardRespawnMarkerGO.AddComponent<RespawnMarker>();
            GameObject.DontDestroyOnLoad(dynamicHazardRespawnMarkerGO);
        }

        public void Unload()
        {
            modClient.LogDebug("Unloading AlwaysHazardRespawn");

            modClient.OnBeforeSavegameSave -= OnBeforeSavegameSave;
            modClient.OnAfterSavegameSave -= OnAfterSavegameSave;
            modClient.OnAfterSavegameLoad -= OnAfterSavegameLoad;
            modClient.OnHazardRespawnLocationSet -= OnHazardRespawnLocationSet;
            modClient.OnBeforePlayerDead -= OnBeforePlayerDead;
            modClient.OnFinishedSceneTransition -= OnFinishedSceneTransition;

            // If we are in the middle of a respawn, restore the respawn data to avoid corrupting future saves.
            // This shouldn't happen since you can't go to the menu during death, but just in case.
            if (isRespawningAfterDeath)
            {
                RestoreRespawnData();
            }

            if (dynamicHazardRespawnMarkerGO != null)
            {
                GameObject.Destroy(dynamicHazardRespawnMarkerGO);
                dynamicHazardRespawnMarkerGO = null;
            }

            dynamicHazardRespawnMarker = null;
            dynamicHazardRespawnData = null;
            originalRespawnData = null;
            isRespawningAfterDeath = false;
        }

        private void OnBeforeSavegameSave()
        {
            if (!isRespawningAfterDeath)
            {
                return;
            }

            // Restore respawn data before saving so that the save does not get corrupted.
            RestoreRespawnData();
        }

        private void OnAfterSavegameSave()
        {
            if (!isRespawningAfterDeath)
            {
                return;
            }

            // Overwrite respawn data after saving to correctly hijack the death respawn.
            OverwriteRespawnData();
        }

        private void OnAfterSavegameLoad(PlayerData playerData)
        {
            // Preserve the original spawn data so we can restore it when necessary.
            PreserveOriginalRespawnData(playerData);

            modClient.LogDebug($"Preserving prevRespawnScene={originalRespawnData.SceneName}, prevRespawnMarkerName={originalRespawnData.MarkerName}");
        }

        private void OnHazardRespawnLocationSet(Vector3 hazardRespawnLocation, bool facingRight)
        {
            // Capture the hazard respawn data whenever it is set.
            CaptureHazardRespawnData(hazardRespawnLocation, facingRight);
        }

        private void OnBeforePlayerDead()
        {
            // Do not consider this death if we don't have hazard respawn data. This should rarely be the case.
            if (dynamicHazardRespawnData == null)
            {
                return;
            }

            isRespawningAfterDeath = true;

            // Overwrite respawn data to hijack the death respawn.
            OverwriteRespawnData();
        }

        private void OnFinishedSceneTransition()
        {
            if (isRespawningAfterDeath)
            {
                isRespawningAfterDeath = false;
                // Restore respawn data after the respawn is complete. This is important to avoid corrupting future saves.
                RestoreRespawnData();
            }
        }

        private void PreserveOriginalRespawnData(PlayerData playerData)
        {
            originalRespawnData = new RespawnData
            {
                MapZone = playerData.mapZone,
                SceneName = playerData.respawnScene,
                MarkerName = playerData.respawnMarkerName,
                FacingRight = playerData.respawnFacingRight,
                SpawnType = playerData.respawnType,
            };
        }

        private void CaptureHazardRespawnData(Vector3 hazardRespawnLocation, bool facingRight)
        {
            modClient.LogDebug($"Capturing hazard respawn scene and location: {GameManager.instance.sceneName}, {hazardRespawnLocation}, {facingRight}");

            dynamicHazardRespawnData = new RespawnData
            {
                MapZone = GameManager.instance.sm.mapZone,
                SceneName = GameManager.instance.sceneName,
                MarkerName = DYNAMIC_HAZARD_RESPAWN_MARKER_NAME,
                FacingRight = facingRight,
                SpawnType = 0, // Normal spawn type.
            };

            // Move the dynamic hazard respawn marker GameObject to the captured location.
            dynamicHazardRespawnMarkerGO.transform.position = hazardRespawnLocation;
            dynamicHazardRespawnMarker.respawnFacingRight = facingRight;
        }

        private void OverwriteRespawnData()
        {
            if (dynamicHazardRespawnData == null || PlayerData.instance?.respawnMarkerName == DYNAMIC_HAZARD_RESPAWN_MARKER_NAME)
            {
                // No hazard respawn data or already hijacking respawn data; nothing to do.
                return;
            }

            modClient.LogDebug($"Overwriting respawn data prevRespawnScene={PlayerData.instance.respawnScene}, prevRespawnMarkerName={PlayerData.instance.respawnMarkerName}" +
                $" with newRespawnScene={dynamicHazardRespawnData.SceneName}, newRespawnMarkerName={DYNAMIC_HAZARD_RESPAWN_MARKER_NAME}");

            PreserveOriginalRespawnData(PlayerData.instance);
            SetPlayerDataRespawnData(PlayerData.instance, dynamicHazardRespawnData);
        }

        private void SetPlayerDataRespawnData(PlayerData playerData, RespawnData respawnData)
        {
            if (playerData == null || respawnData == null)
            {
                return;
            }

            playerData.mapZone = respawnData.MapZone;
            playerData.respawnScene = respawnData.SceneName;
            playerData.respawnMarkerName = respawnData.MarkerName;
            playerData.respawnFacingRight = respawnData.FacingRight;
            playerData.respawnType = respawnData.SpawnType;
        }

        private void RestoreRespawnData()
        {
            if (originalRespawnData == null)
            {
                // No original respawn data to restore.
                return;
            }

            modClient.LogDebug($"Restoring respawn data to prevRespawnScene={originalRespawnData.SceneName}, prevRespawnMarkerName={originalRespawnData.MarkerName}");

            SetPlayerDataRespawnData(PlayerData.instance, originalRespawnData);
        }

        private class RespawnData
        {
            public GlobalEnums.MapZone MapZone;
            public string SceneName;
            public string MarkerName;
            public bool FacingRight;
            public int SpawnType;
        }
    }

    public interface IModClient
    {
        void Log(string msg);
        void LogDebug(string msg);

        event Action OnBeforeSavegameSave;
        event Action OnAfterSavegameSave;
        event Action<PlayerData> OnAfterSavegameLoad;
        event Action<Vector3, bool> OnHazardRespawnLocationSet;
        event Action OnBeforePlayerDead;
        event Action OnFinishedSceneTransition;
    }
}
