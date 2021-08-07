using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenMod.API.Plugins;
using OpenMod.Unturned.Plugins;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

[assembly: PluginMetadata("VehicleDespawner", DisplayName = "Vehicle Despawner", Author = "SilK")]
namespace VehicleDespawner
{
    public class VehicleDespawnerPlugin : OpenModUnturnedPlugin
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<VehicleDespawnerPlugin> _logger;
        private readonly CancellationTokenSource _cancellationToken;

        public Dictionary<uint, uint> LatestUpdates { get; private set; } = new Dictionary<uint, uint>();

        public VehicleDespawnerPlugin(IConfiguration configuration,
            ILogger<VehicleDespawnerPlugin> logger,
            IServiceProvider serviceProvider) : base(serviceProvider)
        {
            _configuration = configuration;
            _logger = logger;
            _cancellationToken = new CancellationTokenSource();
        }
        
        protected override UniTask OnLoadAsync()
        {
            LoadUpdates();

            CheckLoop().Forget();

            Level.onPostLevelLoaded += OnPostLevelLoaded;
            Provider.onServerShutdown += SaveUpdates;

            if (Level.isLoaded)
                OnPostLevelLoaded(0);

            return UniTask.CompletedTask;
        }

        private void OnPostLevelLoaded(int level)
        {
            CheckLoop().Forget();
        }

        protected override UniTask OnUnloadAsync()
        {
            // ReSharper disable DelegateSubtraction
            Level.onPostLevelLoaded -= OnPostLevelLoaded;
            Provider.onServerShutdown -= SaveUpdates;
            // ReSharper restore DelegateSubtraction

            _cancellationToken.Cancel();

            SaveUpdates();

            return UniTask.CompletedTask;
        }

        private void LoadUpdates()
        {
            LatestUpdates = new Dictionary<uint, uint>();

            var path = Path.Combine(WorkingDirectory, "updates.dat");

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
            var path = Path.Combine(WorkingDirectory, "updates.dat");

            using var writer = new StreamWriter(path);

            foreach (var pair in LatestUpdates)
            {
                writer.WriteLine(pair.Key + " " + pair.Value);
            }
        }

        internal void OnVehicleUpdated(InteractableVehicle vehicle)
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

        private async UniTask CheckLoop()
        {
            await UniTask.Delay(10000, cancellationToken: _cancellationToken.Token);

            for (var i = LatestUpdates.Count - 1; i >= 0; i--)
            {
                var instanceId = LatestUpdates.ElementAt(i).Key;

                var vehicle = VehicleManager.getVehicle(instanceId);

                if (vehicle == null || vehicle.isDead)
                    LatestUpdates.Remove(instanceId);
            }

            while (!_cancellationToken.IsCancellationRequested)
            {
                if (VehicleManager.vehicles != null)
                {
                    var now = GetNow();
                    var unusedDuration = _configuration.GetValue<float>("UnusedDuration", 172800);

                    for (var i = LatestUpdates.Count - 1; i >= 0; i--)
                    {
                        var pair = LatestUpdates.ElementAt(i);

                        if (now - pair.Value <= unusedDuration) continue;

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
                }

                await UniTask.Delay((int) (_configuration.GetValue<float>("CheckInterval", 30) * 1000),
                    cancellationToken: _cancellationToken.Token);
            }
        }
    }
}
