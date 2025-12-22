using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using KexEdit.Coaster;
using CorePoint = KexEdit.Core.Point;
using CoasterAggregate = KexEdit.Coaster.Coaster;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct CoasterSyncSystem : ISystem {
        private EntityQuery _coasterQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _coasterQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Coaster, CoasterData>()
                .Build(state.EntityManager);

            state.RequireForUpdate(_coasterQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            var coasterDataLookup = SystemAPI.GetComponentLookup<CoasterData>(true);
            var anchorLookup = SystemAPI.GetComponentLookup<Anchor>(true);
#if VALIDATE_COASTER_PARITY
            var coasterPointBufferLookup = SystemAPI.GetBufferLookup<CoasterPointBuffer>(false);
#else
            var pointBufferLookup = SystemAPI.GetBufferLookup<CorePointBuffer>(false);
#endif

            var coasterEntities = _coasterQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < coasterEntities.Length; i++) {
                Entity coasterEntity = coasterEntities[i];
                CoasterData coasterData = coasterDataLookup[coasterEntity];
                ref readonly CoasterAggregate coaster = ref coasterData.Value;

                if (!coaster.Graph.NodeIds.IsCreated || coaster.Graph.NodeCount == 0) continue;

                CoasterEvaluator.Evaluate(in coaster, out var result, Allocator.Temp);

#if VALIDATE_COASTER_PARITY
                SyncPathsToCoasterBuffer(
                    ref state,
                    coasterEntity,
                    in result,
                    in anchorLookup,
                    ref coasterPointBufferLookup
                );
#else
                SyncPathsToEntities(
                    ref state,
                    coasterEntity,
                    in result,
                    in anchorLookup,
                    ref pointBufferLookup
                );
#endif

                result.Dispose();
            }

            coasterEntities.Dispose();
        }

#if VALIDATE_COASTER_PARITY
        [BurstCompile]
        private void SyncPathsToCoasterBuffer(
            ref SystemState state,
            Entity coasterEntity,
            in EvaluationResult result,
            in ComponentLookup<Anchor> anchorLookup,
            ref BufferLookup<CoasterPointBuffer> coasterPointBufferLookup
        ) {
            var nodeToEntity = new NativeHashMap<uint, Entity>(64, Allocator.Temp);

            foreach (var (node, coasterRef, entity) in
                     SystemAPI.Query<RefRO<Node>, RefRO<CoasterReference>>().WithEntityAccess()) {
                if (coasterRef.ValueRO.Value != coasterEntity) continue;
                nodeToEntity.TryAdd(node.ValueRO.Id, entity);
            }

            var pathKeys = result.Paths.GetKeyArray(Allocator.Temp);

            for (int k = 0; k < pathKeys.Length; k++) {
                uint nodeId = pathKeys[k];
                if (!nodeToEntity.TryGetValue(nodeId, out Entity nodeEntity)) continue;
                if (!coasterPointBufferLookup.TryGetBuffer(nodeEntity, out var coasterPoints)) continue;
                if (!result.Paths.TryGetValue(nodeId, out var path) || !path.IsCreated) continue;

                int facing = 1;
                if (anchorLookup.TryGetComponent(nodeEntity, out var anchor)) {
                    facing = anchor.Value.Facing;
                }

                WriteToCoasterBuffer(in path, facing, ref coasterPoints);
            }

            pathKeys.Dispose();
            nodeToEntity.Dispose();
        }

        [BurstCompile]
        private static void WriteToCoasterBuffer(
            in NativeList<CorePoint> path,
            int facing,
            ref DynamicBuffer<CoasterPointBuffer> coasterPoints
        ) {
            coasterPoints.Clear();
            for (int i = 0; i < path.Length; i++) {
                coasterPoints.Add(new CoasterPointBuffer {
                    Point = path[i],
                    Facing = facing
                });
            }
        }
#else
        [BurstCompile]
        private void SyncPathsToEntities(
            ref SystemState state,
            Entity coasterEntity,
            in EvaluationResult result,
            in ComponentLookup<Anchor> anchorLookup,
            ref BufferLookup<CorePointBuffer> pointBufferLookup
        ) {
            var nodeToEntity = new NativeHashMap<uint, Entity>(64, Allocator.Temp);

            foreach (var (node, coasterRef, entity) in
                     SystemAPI.Query<RefRO<Node>, RefRO<CoasterReference>>().WithEntityAccess()) {
                if (coasterRef.ValueRO.Value != coasterEntity) continue;
                nodeToEntity.TryAdd(node.ValueRO.Id, entity);
            }

            var pathKeys = result.Paths.GetKeyArray(Allocator.Temp);

            for (int k = 0; k < pathKeys.Length; k++) {
                uint nodeId = pathKeys[k];
                if (!nodeToEntity.TryGetValue(nodeId, out Entity nodeEntity)) continue;
                if (!pointBufferLookup.TryGetBuffer(nodeEntity, out var corePoints)) continue;
                if (!result.Paths.TryGetValue(nodeId, out var path) || !path.IsCreated) continue;

                int facing = 1;
                if (anchorLookup.TryGetComponent(nodeEntity, out var anchor)) {
                    facing = anchor.Value.Facing;
                }

                WritePath(in path, facing, ref corePoints);
            }

            pathKeys.Dispose();
            nodeToEntity.Dispose();
        }

        [BurstCompile]
        private static void WritePath(
            in NativeList<CorePoint> path,
            int facing,
            ref DynamicBuffer<CorePointBuffer> corePoints
        ) {
            corePoints.Clear();
            if (path.Length == 0) return;

            CorePointBuffer.CreateFirst(in path.ElementAt(0), facing, out CorePointBuffer first);
            corePoints.Add(first);

            for (int i = 1; i < path.Length; i++) {
                CorePointBuffer.Create(in path.ElementAt(i), in path.ElementAt(i - 1), facing, out CorePointBuffer curr);
                corePoints.Add(curr);
            }
        }
#endif
    }
}
