using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    [BurstCompile]
    public partial struct AppendedCoasterCartPositionSystem : ISystem {
        private Entity _lastCartSection;
        private float _lastCartPosition;

        private EntityQuery _editorCoasterQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _editorCoasterQuery = SystemAPI.QueryBuilder()
                .WithAll<Coaster, EditorCoasterTag>()
                .Build();

            _lastCartSection = Entity.Null;
            _lastCartPosition = -1f;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            if (_editorCoasterQuery.IsEmpty) return;

            var editorCoaster = _editorCoasterQuery.GetSingletonEntity();
            if (!SystemAPI.HasComponent<CartReference>(editorCoaster)) return;

            var editorCart = SystemAPI.GetComponent<CartReference>(editorCoaster).Value;
            if (editorCart == Entity.Null) return;

            if (!SystemAPI.HasComponent<Cart>(editorCart)) return;
            var editorCartComponent = SystemAPI.GetComponent<Cart>(editorCart);

            if (!editorCartComponent.Enabled || editorCartComponent.Kinematic) return;

            bool positionChanged = editorCartComponent.Position != _lastCartPosition ||
                editorCartComponent.Section != _lastCartSection;

            if (!positionChanged) return;

            _lastCartSection = editorCartComponent.Section;
            _lastCartPosition = editorCartComponent.Position;

            var editorRootNode = SystemAPI.GetComponent<Coaster>(editorCoaster).RootNode;
            if (editorRootNode == Entity.Null) return;

            float totalDistance = CalculateDistanceToSection(ref state, editorRootNode, editorCartComponent.Section) + editorCartComponent.Position;

            foreach (var (coaster, entity) in SystemAPI
                .Query<RefRO<Coaster>>()
                .WithAll<AppendedCoasterTag>()
                .WithEntityAccess()) {

                if (!SystemAPI.HasComponent<CartReference>(entity)) continue;

                var appendedCart = SystemAPI.GetComponent<CartReference>(entity).Value;
                if (appendedCart == Entity.Null) continue;

                if (!SystemAPI.HasComponent<Cart>(appendedCart)) continue;

                ref var appendedCartComponent = ref SystemAPI.GetComponentRW<Cart>(appendedCart).ValueRW;
                if (!appendedCartComponent.Enabled || appendedCartComponent.Kinematic) continue;

                SetCartPosition(ref state, ref appendedCartComponent, coaster.ValueRO.RootNode, totalDistance);
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

        private void SetCartPosition(ref SystemState state, ref Cart cart, Entity start, float targetDistance) {
            float currentDistance = 0f;
            var current = start;
            using var processedEntities = new NativeHashSet<Entity>(16, Allocator.Temp);

            while (current != Entity.Null && !processedEntities.Contains(current)) {
                processedEntities.Add(current);

                if (SystemAPI.HasBuffer<Point>(current)) {
                    var points = SystemAPI.GetBuffer<Point>(current);
                    float sectionLength = points.Length;

                    if (currentDistance + sectionLength >= targetDistance) {
                        cart.Section = current;
                        cart.Position = math.clamp(targetDistance - currentDistance, 0f, points.Length - 1f);
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

            if (cart.Section != Entity.Null && SystemAPI.HasBuffer<Point>(cart.Section)) {
                var points = SystemAPI.GetBuffer<Point>(cart.Section);
                cart.Position = points.Length - 1f;
            }
        }
    }
}
