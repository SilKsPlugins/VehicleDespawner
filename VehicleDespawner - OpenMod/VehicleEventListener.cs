using OpenMod.API.Eventing;
using OpenMod.Unturned.Vehicles.Events;
using System.Threading.Tasks;

namespace VehicleDespawner
{
    public class EventsListener :
        IEventListener<UnturnedPlayerEnteredVehicleEvent>,
        IEventListener<UnturnedPlayerExitedVehicleEvent>
    {
        private readonly VehicleDespawnerPlugin _plugin;

        public EventsListener(VehicleDespawnerPlugin plugin)
        {
            _plugin = plugin;
        }

        public Task HandleEventAsync(object? sender, UnturnedPlayerEnteredVehicleEvent @event)
        {
            _plugin.OnVehicleUpdated(@event.Vehicle.Vehicle);

            return Task.CompletedTask;
        }

        public Task HandleEventAsync(object? sender, UnturnedPlayerExitedVehicleEvent @event)
        {
            _plugin.OnVehicleUpdated(@event.Vehicle.Vehicle);

            return Task.CompletedTask;
        }
    }
}
