using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class TrainStyleLoadingSystem : SystemBase {
        protected override void OnCreate() {
            RequireForUpdate<Preferences>();
            RequireForUpdate<LoadTrainStyleEvent>();
        }

        protected override void OnUpdate() {
            var preferences = SystemAPI.GetSingleton<Preferences>();
            int layer = preferences.TrainLayer;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (evt, entity) in SystemAPI.Query<LoadTrainStyleEvent>().WithEntityAccess()) {
                ecb.AddComponent(entity, new TrainStyle {
                    Version = evt.Data.Version,
                });
                ecb.AddComponent(entity, new TrainStyleManaged(evt.Data, layer));
                ecb.SetName(entity, "Train Style");
                ecb.AddComponent<TrainStyleReference>(entity, entity);
                ecb.AddBuffer<TrainCarMeshReference>(entity);
                ecb.AddBuffer<WheelAssemblyMeshReference>(entity);
                ecb.RemoveComponent<LoadTrainStyleEvent>(entity);
            }
            ecb.Playback(EntityManager);
        }
    }
}
