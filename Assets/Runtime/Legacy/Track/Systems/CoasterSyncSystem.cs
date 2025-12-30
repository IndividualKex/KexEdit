using KexEdit.Spline;
using KexEdit.Spline.Resampling;
using KexEdit.Document;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using DocumentAggregate = KexEdit.Document.Document;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [BurstCompile]
    public partial struct CoasterSyncSystem : ISystem {
        private const float SplineResolution = 0.1f;

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
            var splineBufferLookup = SystemAPI.GetBufferLookup<SplineBuffer>(false);
#if VALIDATE_COASTER_PARITY
            var coasterPointBufferLookup = SystemAPI.GetBufferLookup<CoasterPointBuffer>(false);
#else
            var pointBufferLookup = SystemAPI.GetBufferLookup<CorePointBuffer>(false);
#endif

            var coasterEntities = _coasterQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < coasterEntities.Length; i++) {
                Entity coasterEntity = coasterEntities[i];
                CoasterData coasterData = coasterDataLookup[coasterEntity];
                ref readonly DocumentAggregate document = ref coasterData.Value;

                if (!document.Graph.NodeIds.IsCreated || document.Graph.NodeCount == 0) continue;

                KexEdit.Track.Track.Build(in document, Allocator.Temp, out var track);

#if VALIDATE_COASTER_PARITY
                SyncPathsToCoasterBuffer(
                    ref state,
                    coasterEntity,
                    in track,
                    in anchorLookup,
                    ref coasterPointBufferLookup
                );
#else
                SyncPathsToEntities(
                    ref state,
                    coasterEntity,
                    in track,
                    in anchorLookup,
                    ref pointBufferLookup
                );
#endif

                SyncPathsToSplineBuffers(ref state, coasterEntity, in track, ref splineBufferLookup);

                track.Dispose();
            }

            coasterEntities.Dispose();
        }

#if VALIDATE_COASTER_PARITY
        [BurstCompile]
        private void SyncPathsToCoasterBuffer(
            ref SystemState state,
            Entity coasterEntity,
            in KexEdit.Track.Track track,
            in ComponentLookup<Anchor> anchorLookup,
            ref BufferLookup<CoasterPointBuffer> coasterPointBufferLookup
        ) {
            var nodeToEntity = new NativeHashMap<uint, Entity>(64, Allocator.Temp);

            foreach (var (node, coasterRef, entity) in
                     SystemAPI.Query<RefRO<Node>, RefRO<CoasterReference>>().WithEntityAccess()) {
                if (coasterRef.ValueRO.Value != coasterEntity) continue;
                nodeToEntity.TryAdd(node.ValueRO.Id, entity);
            }

            var nodeKeys = track.NodeToSection.GetKeyArray(Allocator.Temp);

            for (int k = 0; k < nodeKeys.Length; k++) {
                uint nodeId = nodeKeys[k];
                if (!nodeToEntity.TryGetValue(nodeId, out Entity nodeEntity)) continue;
                if (!coasterPointBufferLookup.TryGetBuffer(nodeEntity, out var coasterPoints)) continue;
                if (!track.NodeToSection.TryGetValue(nodeId, out int sectionIndex)) continue;

                var section = track.Sections[sectionIndex];
                if (!section.IsValid) continue;

                int facing = section.Facing;

                WriteToCoasterBuffer(in track, in section, facing, ref coasterPoints);
            }

            nodeKeys.Dispose();
            nodeToEntity.Dispose();
        }

        [BurstCompile]
        private static void WriteToCoasterBuffer(
            in KexEdit.Track.Track track,
            in KexEdit.Track.Section section,
            int facing,
            ref DynamicBuffer<CoasterPointBuffer> coasterPoints
        ) {
            coasterPoints.Clear();
            for (int i = section.StartIndex; i <= section.EndIndex; i++) {
                coasterPoints.Add(new CoasterPointBuffer {
                    Point = track.Points[i],
                    Facing = facing
                });
            }
        }
#else
        [BurstCompile]
        private void SyncPathsToEntities(
            ref SystemState state,
            Entity coasterEntity,
            in KexEdit.Track.Track track,
            in ComponentLookup<Anchor> anchorLookup,
            ref BufferLookup<CorePointBuffer> pointBufferLookup
        ) {
            var nodeToEntity = new NativeHashMap<uint, Entity>(64, Allocator.Temp);

            foreach (var (node, coasterRef, entity) in
                     SystemAPI.Query<RefRO<Node>, RefRO<CoasterReference>>().WithEntityAccess()) {
                if (coasterRef.ValueRO.Value != coasterEntity) continue;
                nodeToEntity.TryAdd(node.ValueRO.Id, entity);
            }

            var nodeKeys = track.NodeToSection.GetKeyArray(Allocator.Temp);

            for (int k = 0; k < nodeKeys.Length; k++) {
                uint nodeId = nodeKeys[k];
                if (!nodeToEntity.TryGetValue(nodeId, out Entity nodeEntity)) continue;
                if (!pointBufferLookup.TryGetBuffer(nodeEntity, out var corePoints)) continue;
                if (!track.NodeToSection.TryGetValue(nodeId, out int sectionIndex)) continue;

                var section = track.Sections[sectionIndex];
                if (!section.IsValid) continue;

                int facing = section.Facing;

                WritePath(in track, in section, facing, ref corePoints);
            }

            nodeKeys.Dispose();
            nodeToEntity.Dispose();
        }

        [BurstCompile]
        private static void WritePath(
            in KexEdit.Track.Track track,
            in KexEdit.Track.Section section,
            int facing,
            ref DynamicBuffer<CorePointBuffer> corePoints
        ) {
            corePoints.Clear();
            if (!section.IsValid || section.Length == 0) return;

            var firstPoint = track.Points[section.StartIndex];
            CorePointBuffer.CreateFirst(in firstPoint, facing, out CorePointBuffer first);
            corePoints.Add(first);

            for (int i = section.StartIndex + 1; i <= section.EndIndex; i++) {
                var currPoint = track.Points[i];
                var prevPoint = track.Points[i - 1];
                CorePointBuffer.Create(in currPoint, in prevPoint, facing, out CorePointBuffer curr);
                corePoints.Add(curr);
            }
        }
#endif

        [BurstCompile]
        private void SyncPathsToSplineBuffers(
            ref SystemState state,
            Entity coasterEntity,
            in KexEdit.Track.Track track,
            ref BufferLookup<SplineBuffer> splineBufferLookup
        ) {
            var nodeToEntity = new NativeHashMap<uint, Entity>(64, Allocator.Temp);

            foreach (var (node, coasterRef, entity) in
                     SystemAPI.Query<RefRO<Node>, RefRO<CoasterReference>>().WithEntityAccess()) {
                if (coasterRef.ValueRO.Value != coasterEntity) continue;
                nodeToEntity.TryAdd(node.ValueRO.Id, entity);
            }

            var nodeKeys = track.NodeToSection.GetKeyArray(Allocator.Temp);
            var tempSpline = new NativeList<SplinePoint>(256, Allocator.Temp);
            var tempPath = new NativeArray<CorePoint>(256, Allocator.Temp);

            for (int k = 0; k < nodeKeys.Length; k++) {
                uint nodeId = nodeKeys[k];
                if (!nodeToEntity.TryGetValue(nodeId, out Entity nodeEntity)) continue;
                if (!splineBufferLookup.TryGetBuffer(nodeEntity, out var splineBuffer)) continue;
                if (!track.NodeToSection.TryGetValue(nodeId, out int sectionIndex)) continue;

                var section = track.Sections[sectionIndex];
                if (!section.IsValid) continue;

                int pathLength = section.Length;
                if (tempPath.Length < pathLength) {
                    tempPath.Dispose();
                    tempPath = new NativeArray<CorePoint>(pathLength, Allocator.Temp);
                }

                for (int i = 0; i < pathLength; i++) {
                    tempPath[i] = track.Points[section.StartIndex + i];
                }

                var pathSlice = tempPath.GetSubArray(0, pathLength);
                SplineResampler.Resample(pathSlice, SplineResolution, ref tempSpline);

                splineBuffer.Clear();
                for (int i = 0; i < tempSpline.Length; i++) {
                    splineBuffer.Add(new SplineBuffer { Point = tempSpline[i] });
                }
            }

            tempPath.Dispose();
            tempSpline.Dispose();
            nodeKeys.Dispose();
            nodeToEntity.Dispose();
        }
    }
}
