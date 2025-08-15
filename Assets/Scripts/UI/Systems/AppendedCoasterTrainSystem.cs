using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class AppendedCoasterTrainSystem : SystemBase {
        private EntityQuery _editorCoasterQuery;
        private EntityQuery _loadTrainMeshEventQuery;

        protected override void OnCreate() {
            _editorCoasterQuery = SystemAPI.QueryBuilder()
                .WithAll<Coaster, EditorCoasterTag>()
                .Build();
            _loadTrainMeshEventQuery = SystemAPI.QueryBuilder()
                .WithAll<LoadTrainMeshEvent>()
                .Build();
            RequireForUpdate<TrainStyleSettings>();
        }

        protected override void OnUpdate() {
            if (_editorCoasterQuery.IsEmpty) return;

            Entity editorCoaster = _editorCoasterQuery.GetSingletonEntity();
            if (!SystemAPI.HasComponent<TrainReference>(editorCoaster)) return;

            Entity editorTrain = SystemAPI.GetComponent<TrainReference>(editorCoaster).Value;
            if (editorTrain == Entity.Null) return;
            if (!SystemAPI.HasComponent<TrainStyleReference>(editorTrain)) return;

            var styleSettings = SystemAPI.ManagedAPI.GetSingleton<TrainStyleSettings>();
            var editorTrainStyleRef = SystemAPI.GetComponent<TrainStyleReference>(editorTrain);

            if (editorTrainStyleRef.StyleIndex >= styleSettings.Styles.Count) return;

            var trainStyle = styleSettings.Styles[editorTrainStyleRef.StyleIndex];
            if (trainStyle.Mesh == Entity.Null) return;

            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            using var existing = new NativeHashSet<Entity>(_loadTrainMeshEventQuery.CalculateEntityCount(), Allocator.Temp);
            foreach (var evt in SystemAPI.Query<LoadTrainMeshEvent>()) {
                existing.Add(evt.Target);
            }

            foreach (var (coaster, entity) in SystemAPI
                .Query<RefRO<Coaster>>()
                .WithAll<AppendedCoasterTag>()
                .WithEntityAccess()) {

                if (!SystemAPI.HasComponent<TrainReference>(entity)) continue;

                Entity train = SystemAPI.GetComponent<TrainReference>(entity).Value;
                if (train == Entity.Null) continue;
                if (!SystemAPI.HasComponent<TrainCarMeshReference>(train)) continue;

                var trainCarMesh = SystemAPI.GetComponent<TrainCarMeshReference>(train);

                bool needsStyleReference = false;
                bool needsTrainMesh = trainCarMesh.Value == Entity.Null;

                if (SystemAPI.HasComponent<TrainStyleReference>(train)) {
                    var trainStyleRef = SystemAPI.GetComponent<TrainStyleReference>(train);
                    if (trainStyleRef.StyleIndex != editorTrainStyleRef.StyleIndex ||
                        trainStyleRef.Version != styleSettings.Version) {
                        needsStyleReference = true;
                        needsTrainMesh = true;
                    }
                }
                else {
                    needsStyleReference = true;
                    needsTrainMesh = true;
                }

                if (needsStyleReference) {
                    if (SystemAPI.HasComponent<TrainStyleReference>(train)) {
                        ecb.SetComponent(train, new TrainStyleReference {
                            StyleIndex = editorTrainStyleRef.StyleIndex,
                            Version = styleSettings.Version
                        });
                    }
                    else {
                        ecb.AddComponent(train, new TrainStyleReference {
                            StyleIndex = editorTrainStyleRef.StyleIndex,
                            Version = styleSettings.Version
                        });
                    }
                }

                if (needsTrainMesh && !existing.Contains(train)) {
                    if (trainCarMesh.Value != Entity.Null) {
                        ecb.DestroyEntity(trainCarMesh.Value);
                    }

                    var loadEventEntity = ecb.CreateEntity();
                    ecb.AddComponent(loadEventEntity, new LoadTrainMeshEvent {
                        Target = train,
                        Train = trainStyle.Mesh
                    });
                    ecb.SetName(loadEventEntity, "Load Train Mesh Event");
                }
            }

            ecb.Playback(EntityManager);
        }
    }
}
