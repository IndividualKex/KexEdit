using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class CartCreationSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (coaster, entity) in SystemAPI
                .Query<Coaster>()
                .WithNone<CartReference, TrainReference>()
                .WithEntityAccess()
            ) {
                var trainEntity = ecb.CreateEntity();
                ecb.AddComponent(trainEntity, LocalTransform.Identity);
                ecb.AddComponent<TrainReference>(entity, trainEntity);
                ecb.AddComponent<CoasterReference>(trainEntity, entity);
                ecb.AddComponent(trainEntity, new Train {
                    Position = 1f,
                    TotalLength = 5f,
                    Enabled = true,
                    Kinematic = false,
                    CarCount = 1
                });
                ecb.SetName(trainEntity, "Train");

                var carEntity = ecb.CreateEntity();
                ecb.AddComponent(carEntity, LocalTransform.Identity);
                ecb.AddComponent(carEntity, new TrainCar {
                    Train = trainEntity,
                    CarIndex = 0,
                    Length = 5f,
                    CouplerOffset = 0f,
                    Position = 1f
                });
                ecb.AddBuffer<WheelAssembly>(carEntity);
                ecb.AppendToBuffer(carEntity, new WheelAssembly {
                    Offset = 1.9f,
                    TrackGauge = 1.0f
                });
                ecb.AppendToBuffer(carEntity, new WheelAssembly {
                    Offset = -1.9f,
                    TrackGauge = 1.0f
                });
                ecb.AddComponent(carEntity, new TrainCarMeshReference());
                ecb.SetName(carEntity, "TrainCar_0");

                var cartEntity = ecb.CreateEntity();
                ecb.AddComponent(cartEntity, LocalTransform.Identity);
                ecb.AddComponent<CartReference>(entity, cartEntity);
                ecb.AddComponent<CoasterReference>(cartEntity, entity);
                ecb.AddComponent(cartEntity, new Cart {
                    Position = 1f,
                    Enabled = true,
                    Kinematic = false
                });
                ecb.AddComponent(cartEntity, new CartMeshReference());
                ecb.SetName(cartEntity, "Cart_Legacy");
            }
            ecb.Playback(EntityManager);
        }
    }
}
