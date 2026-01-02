using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using DocumentAggregate = KexEdit.Document.Document;
using TrackData = KexEdit.Track.Track;

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

        public void OnDestroy(ref SystemState state) {
            if (SystemAPI.TryGetSingleton<TrackSingleton>(out var singleton) && singleton.Value.IsCreated) {
                singleton.Value.Dispose();
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            UpdateTrackSingleton(ref state);

            if (!SystemAPI.TryGetSingleton<TrackSingleton>(out var singleton) || !singleton.Value.IsCreated) {
                return;
            }

            ref readonly TrackData track = ref singleton.Value;

            var coasterEntities = _coasterQuery.ToEntityArray(Allocator.Temp);
            if (coasterEntities.Length == 0) {
                coasterEntities.Dispose();
                return;
            }

            Entity coasterEntity = coasterEntities[0];
            coasterEntities.Dispose();
            var pointBufferLookup = SystemAPI.GetBufferLookup<CorePointBuffer>(false);
            SyncPathsToEntities(ref state, coasterEntity, in track, ref pointBufferLookup);
        }

        private void UpdateTrackSingleton(ref SystemState state) {
            if (_coasterQuery.IsEmpty) return;

            var coasterEntities = _coasterQuery.ToEntityArray(Allocator.Temp);
            if (coasterEntities.Length == 0) {
                coasterEntities.Dispose();
                return;
            }

            Entity coasterEntity = coasterEntities[0];
            coasterEntities.Dispose();

            var coasterData = SystemAPI.GetComponent<CoasterData>(coasterEntity);
            ref readonly DocumentAggregate document = ref coasterData.Value;

            if (!SystemAPI.HasSingleton<TrackSingleton>()) {
                Entity singletonEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<TrackSingleton>(singletonEntity);
                state.EntityManager.SetName(singletonEntity, "TrackSingleton");
            }

            ref var singleton = ref SystemAPI.GetSingletonRW<TrackSingleton>().ValueRW;

            if (singleton.Value.IsCreated) {
                singleton.Value.Dispose();
            }

            if (!document.Graph.NodeIds.IsCreated || document.Graph.NodeCount == 0) {
                singleton.Value = default;
                return;
            }

            int defaultStyle = SystemAPI.TryGetSingleton<StyleConfigSingleton>(out var styleConfig)
                ? styleConfig.DefaultStyleIndex
                : 0;
            TrackData.Build(in document, Allocator.Persistent, 0.5f, defaultStyle, out singleton.Value);
        }

        private void SyncPathsToEntities(
            ref SystemState state,
            Entity coasterEntity,
            in KexEdit.Track.Track track,
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
    }
}
