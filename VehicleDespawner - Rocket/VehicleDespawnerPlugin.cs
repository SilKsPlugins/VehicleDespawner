using Rocket.Core.Plugins;
using SDG.Unturned;
using SilK.VehicleDespawner.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SilK.VehicleDespawner
{
    public class VehicleDespawnerPlugin : RocketPlugin<VehicleDespawnerConfiguration>
    {
        public static VehicleDespawnerPlugin Instance { get; private set; }

        public Dictionary<uint, uint> LatestUpdates { get; private set; }

        private void LoadUpdates()
        {
            LatestUpdates = new Dictionary<uint, uint>();

            var path = Path.Combine(Directory, "Updates.dat");

            if (!File.Exists(path))
                return;

            var lines = File.ReadAllLines(path);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2) continue;

                if (!uint.TryParse(parts[0], out var instanceId) || !uint.TryParse(parts[1], out var lastUpdated))
                    continue;

                if (LatestUpdates.ContainsKey(instanceId))
                    LatestUpdates[instanceId] = lastUpdated;
                else
                    LatestUpdates.Add(instanceId, lastUpdated);
            }
        }

        private void SaveUpdates()
        {
            var path = Path.Combine(Directory, "Updates.dat");

            using var writer = new StreamWriter(path);

            foreach (var pair in LatestUpdates)
            {
                writer.WriteLine(pair.Key + " " + pair.Value);
            }
        }

        protected override void Load()
        {
            Instance = this;

            LoadUpdates();

            VehicleManager.onEnterVehicleRequested += OnEnterVehicleRequested;
            VehicleManager.onExitVehicleRequested += OnExitVehicleRequested;

            Level.onPostLevelLoaded += OnPostLevelLoaded;
            if (Level.isLoaded)
                OnPostLevelLoaded(0);
        }

        private void OnPostLevelLoaded(int level)
        {
            StartCoroutine("CheckLoop");
        }

        protected override void Unload()
        {
            StopCoroutine("CheckLoop");

            VehicleManager.onEnterVehicleRequested -= OnEnterVehicleRequested;
            VehicleManager.onExitVehicleRequested -= OnExitVehicleRequested;

            SaveUpdates();

            Instance = null;
        }

        private void OnEnterVehicleRequested(Player player, InteractableVehicle vehicle, ref bool shouldAllow) =>
            OnVehicleUpdated(vehicle);

        private void OnExitVehicleRequested(Player player, InteractableVehicle vehicle, ref bool shouldAllow,
            ref Vector3 pendingLocation, ref float pendingYaw) =>
            OnVehicleUpdated(vehicle);

        private void OnVehicleUpdated(InteractableVehicle vehicle)
        {
            if (!LatestUpdates.ContainsKey(vehicle.instanceID))
                LatestUpdates.Add(vehicle.instanceID, GetNow());
            else
                LatestUpdates[vehicle.instanceID] = GetNow();
        }

        private static readonly DateTime BaseTime = new DateTime(1970, 1, 1);

        public static uint GetNow()
        {
            return (uint)DateTime.Now.Subtract(BaseTime).TotalSeconds;
        }

        private IEnumerator CheckLoop()
        {
            yield return new WaitForSeconds(10);

            for (var i = LatestUpdates.Count - 1; i >= 0; i--)
            {
                var instanceId = LatestUpdates.ElementAt(i).Key;

                var vehicle = VehicleManager.getVehicle(instanceId);

                if (vehicle == null || vehicle.isDead)
                    LatestUpdates.Remove(instanceId);
            }

            while (isActiveAndEnabled)
            {
                var now = GetNow();
                var respawnInterval = Configuration.Instance.UnusedDuration;

                for (var i = LatestUpdates.Count - 1; i >= 0; i--)
                {
                    var pair = LatestUpdates.ElementAt(i);

                    if (now - pair.Value <= respawnInterval) continue;

                    var vehicle = VehicleManager.getVehicle(pair.Key);

                    if (vehicle != null)
                    {
                        if (vehicle.passengers.Any(x => x?.player != null))
                        {
                            LatestUpdates[pair.Key] = GetNow();

                            continue;
                        }

                        VehicleManager.askVehicleDestroy(vehicle);
                    }

                    LatestUpdates.Remove(pair.Key);
                }

                foreach (var vehicle in VehicleManager.vehicles.Where(vehicle => vehicle != null))
                {
                    if (!LatestUpdates.ContainsKey(vehicle.instanceID))
                        LatestUpdates.Add(vehicle.instanceID, now);
                }

                yield return new WaitForSeconds(Configuration.Instance.CheckInterval);
            }
        }
    }
}
