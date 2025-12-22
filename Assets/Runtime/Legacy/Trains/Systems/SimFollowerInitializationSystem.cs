using KexEdit.Trains.Sim;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class SimFollowerInitializationSystem : SystemBase {
        protected override void OnUpdate() {
            if (SystemAPI.HasSingleton<SimFollowerSingleton>()) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            Entity entity = ecb.CreateEntity();
            ecb.AddComponent(entity, new SimFollowerSingleton {
                Follower = SimFollower.Default
            });
            ecb.SetName(entity, "SimFollowerSingleton");
            ecb.Playback(EntityManager);
        }
    }
}
