using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(TrackSegmentInitializationSystem))]
    [BurstCompile]
    public partial struct StyleHashSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            foreach (var (hashRW, trackStyleBuffer, entity) in SystemAPI
                .Query<RefRW<TrackStyleHash>, DynamicBuffer<TrackStyleKeyframe>>()
                .WithEntityAccess()
            ) {
                ref var hash = ref hashRW.ValueRW;

                var overrides = SystemAPI.GetComponent<PropertyOverrides>(entity);
                if (overrides.TrackStyle) {
                    hash = CalculateStyleHash(trackStyleBuffer);
                }
                else {
                    hash = CalculatePointHash(SystemAPI.GetBuffer<Point>(entity));
                }
            }
        }

        private uint CalculateStyleHash(DynamicBuffer<TrackStyleKeyframe> keyframes) {
            if (keyframes.Length == 0) return 13;

            uint hash = 13;
            for (int i = 0; i < keyframes.Length; i++) {
                var keyframe = keyframes[i].Value;
                hash = math.hash(new float4(
                    keyframe.Time,
                    keyframe.Value,
                    (float)keyframe.Id,
                    hash
                ));
            }
            return hash;
        }

        private uint CalculatePointHash(DynamicBuffer<Point> points) {
            if (points.Length == 0) return 17;
            if (points.Length == 1) {
                PointData point = points[0];
                return math.hash(new float4(point.Velocity, point.Energy, point.Friction, 1));
            }

            PointData first = points[0];
            PointData last = points[^1];
            uint firstHash = math.hash(new float4(first.Velocity, first.Energy, first.Friction, (uint)points.Length));
            uint lastHash = math.hash(new float4(last.Velocity, last.Energy, last.Friction, firstHash));
            return lastHash;
        }
    }
}
