using Rocket.API;

namespace SilK.VehicleDespawner.Configuration
{
    public class VehicleDespawnerConfiguration : IRocketPluginConfiguration
    {
        public float CheckInterval { get; set; }

        public float UnusedDuration { get; set; }

        public void LoadDefaults()
        {
            CheckInterval = 30;

            // 2 days
            UnusedDuration = 2 * 24 * 60 * 60;
        }
    }
}