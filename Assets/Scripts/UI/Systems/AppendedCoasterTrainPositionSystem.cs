using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    [BurstCompile]
    public partial struct AppendedCoasterTrainPositionSystem : ISystem {
        private Entity _lastTrainSection;
        private float _lastTrainPosition;

        private EntityQuery _editorCoasterQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _editorCoasterQuery = SystemAPI.QueryBuilder()
                .WithAll<Coaster, EditorCoasterTag>()
                .Build();

            _lastTrainSection = Entity.Null;
            _lastTrainPosition = -1f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            if (_editorCoasterQuery.IsEmpty) return;

            var editorCoaster = _editorCoasterQuery.GetSingletonEntity();
            if (!SystemAPI.HasComponent<TrainReference>(editorCoaster)) return;

            var editorTrainEntity = SystemAPI.GetComponent<TrainReference>(editorCoaster).Value;
            if (editorTrainEntity == Entity.Null) return;

            if (!SystemAPI.HasComponent<Train>(editorTrainEntity)) return;
            var editorTrain = SystemAPI.GetComponent<Train>(editorTrainEntity);
            var editorFollower = SystemAPI.GetComponent<TrackFollower>(editorTrainEntity);

            if (!editorTrain.Enabled || editorTrain.Kinematic) return;

            bool positionChanged = editorFollower.Index != _lastTrainPosition ||
                editorFollower.Section != _lastTrainSection;

            if (!positionChanged) return;

            _lastTrainSection = editorFollower.Section;
            _lastTrainPosition = editorFollower.Index;

            var editorRootNode = SystemAPI.GetComponent<Coaster>(editorCoaster).RootNode;
            if (editorRootNode == Entity.Null) return;

            float totalDistance = CalculateDistanceToSection(ref state, editorRootNode, editorFollower.Section) + editorFollower.Index;

            foreach (var (coaster, entity) in SystemAPI
                .Query<RefRO<Coaster>>()
                .WithAll<AppendedCoasterTag>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasComponent<TrainReference>(entity)) continue;

                var appendedTrainEntity = SystemAPI.GetComponent<TrainReference>(entity).Value;
                if (appendedTrainEntity == Entity.Null) continue;

                if (!SystemAPI.HasComponent<Train>(appendedTrainEntity)) continue;

                ref var appendedTrain = ref SystemAPI.GetComponentRW<Train>(appendedTrainEntity).ValueRW;
                if (appendedTrain.Kinematic) continue;

                ref var appendedFollower = ref SystemAPI.GetComponentRW<TrackFollower>(appendedTrainEntity).ValueRW;
                SetTrainPosition(ref state, ref appendedTrain, ref appendedFollower, coaster.ValueRO.RootNode, totalDistance);
            }
        }

        private float CalculateDistanceToSection(ref SystemState state, Entity startEntity, Entity targetSection) {
            float distance = 0f;
            var currentEntity = startEntity;
            using var processedEntities = new NativeHashSet<Entity>(16, Allocator.Temp);

            while (currentEntity != Entity.Null && !processedEntities.Contains(currentEntity)) {
                processedEntities.Add(currentEntity);

                if (currentEntity == targetSection) return distance;

                if (SystemAPI.HasBuffer<CorePointBuffer>(currentEntity)) {
                    distance += SystemAPI.GetBuffer<CorePointBuffer>(currentEntity).Length;
                }

                currentEntity = SystemAPI.HasComponent<Node>(currentEntity)
                    ? SystemAPI.GetComponent<Node>(currentEntity).Next
                    : Entity.Null;
            }

            return distance;
        }

        private void SetTrainPosition(ref SystemState state, ref Train train, ref TrackFollower follower, Entity start, float targetDistance) {
            float currentDistance = 0f;
            var current = start;
            using var processedEntities = new NativeHashSet<Entity>(16, Allocator.Temp);
            Entity lastSection = Entity.Null;
            int lastSectionPointsLength = 0;

            while (current != Entity.Null && !processedEntities.Contains(current)) {
                processedEntities.Add(current);

                if (SystemAPI.HasBuffer<CorePointBuffer>(current)) {
                    var points = SystemAPI.GetBuffer<CorePointBuffer>(current);
                    float sectionLength = points.Length;

                    lastSection = current;
                    lastSectionPointsLength = points.Length;

                    if (currentDistance + sectionLength >= targetDistance) {
                        follower.Section = current;
                        follower.Index = math.clamp(targetDistance - currentDistance, 0f, points.Length - 1f);
                        train.Enabled = true;
                        return;
                    }

                    currentDistance += sectionLength;
                }

                if (SystemAPI.HasComponent<Node>(current)) {
                    var node = SystemAPI.GetComponent<Node>(current);
                    current = node.Next;
                }
                else {
                    break;
                }
            }

            if (lastSection != Entity.Null) {
                follower.Section = lastSection;
                follower.Index = math.max(0f, lastSectionPointsLength - 1f);
                train.Enabled = false;
            }
        }
    }
}
