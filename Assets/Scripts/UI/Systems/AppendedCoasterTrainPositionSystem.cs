using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

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

            var editorTrain = SystemAPI.GetComponent<TrainReference>(editorCoaster).Value;
            if (editorTrain == Entity.Null) return;

            if (!SystemAPI.HasComponent<Train>(editorTrain)) return;
            var editorTrainComponent = SystemAPI.GetComponent<Train>(editorTrain);

            if (!editorTrainComponent.Enabled || editorTrainComponent.Kinematic) return;

            bool positionChanged = editorTrainComponent.Position != _lastTrainPosition ||
                editorTrainComponent.Section != _lastTrainSection;

            if (!positionChanged) return;

            _lastTrainSection = editorTrainComponent.Section;
            _lastTrainPosition = editorTrainComponent.Position;

            var editorRootNode = SystemAPI.GetComponent<Coaster>(editorCoaster).RootNode;
            if (editorRootNode == Entity.Null) return;

            float totalDistance = CalculateDistanceToSection(ref state, editorRootNode, editorTrainComponent.Section) + editorTrainComponent.Position;

            foreach (var (coaster, entity) in SystemAPI
                .Query<RefRO<Coaster>>()
                .WithAll<AppendedCoasterTag>()
                .WithEntityAccess()) {

                if (!SystemAPI.HasComponent<TrainReference>(entity)) continue;

                var appendedTrain = SystemAPI.GetComponent<TrainReference>(entity).Value;
                if (appendedTrain == Entity.Null) continue;

                if (!SystemAPI.HasComponent<Train>(appendedTrain)) continue;

                ref var appendedTrainComponent = ref SystemAPI.GetComponentRW<Train>(appendedTrain).ValueRW;
                if (!appendedTrainComponent.Enabled || appendedTrainComponent.Kinematic) continue;

                SetTrainPosition(ref state, ref appendedTrainComponent, coaster.ValueRO.RootNode, totalDistance);
            }
        }

        private float CalculateDistanceToSection(ref SystemState state, Entity startEntity, Entity targetSection) {
            float distance = 0f;
            var currentEntity = startEntity;
            using var processedEntities = new NativeHashSet<Entity>(16, Allocator.Temp);

            while (currentEntity != Entity.Null && !processedEntities.Contains(currentEntity)) {
                processedEntities.Add(currentEntity);

                if (currentEntity == targetSection) return distance;

                if (SystemAPI.HasBuffer<Point>(currentEntity)) {
                    distance += SystemAPI.GetBuffer<Point>(currentEntity).Length;
                }

                currentEntity = SystemAPI.HasComponent<Node>(currentEntity)
                    ? SystemAPI.GetComponent<Node>(currentEntity).Next
                    : Entity.Null;
            }

            return distance;
        }

        private void SetTrainPosition(ref SystemState state, ref Train train, Entity start, float targetDistance) {
            float currentDistance = 0f;
            var current = start;
            using var processedEntities = new NativeHashSet<Entity>(16, Allocator.Temp);

            while (current != Entity.Null && !processedEntities.Contains(current)) {
                processedEntities.Add(current);

                if (SystemAPI.HasBuffer<Point>(current)) {
                    var points = SystemAPI.GetBuffer<Point>(current);
                    float sectionLength = points.Length;

                    if (currentDistance + sectionLength >= targetDistance) {
                        train.Section = current;
                        train.Position = math.clamp(targetDistance - currentDistance, 0f, points.Length - 1f);
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

            if (train.Section != Entity.Null && SystemAPI.HasBuffer<Point>(train.Section)) {
                var points = SystemAPI.GetBuffer<Point>(train.Section);
                train.Position = points.Length - 1f;
            }
        }
    }
}
