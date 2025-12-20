using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(TrackSegmentInitializationSystem))]
    [BurstCompile]
    public partial struct StyleHashSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            foreach (var (hash, trackStyleBuffer, entity) in SystemAPI
                .Query<RefRW<TrackStyleHash>, DynamicBuffer<TrackStyleKeyframe>>()
                .WithEntityAccess()
            ) {
                ref var hashRef = ref hash.ValueRW;

                var overrides = SystemAPI.GetComponent<PropertyOverrides>(entity);
                if (overrides.TrackStyle) {
                    hashRef = CalculateStyleHash(trackStyleBuffer);
                }
                else {
                    hashRef = CalculatePointHash(SystemAPI.GetBuffer<CorePointBuffer>(entity));
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

        private uint CalculatePointHash(DynamicBuffer<CorePointBuffer> points) {
            if (points.Length == 0) return 17;
            if (points.Length == 1) {
                var point = points[0];
                return math.hash(new float4(point.Velocity(), point.Energy(), point.Friction(), 1));
            }

            var first = points[0];
            var last = points[^1];
            uint firstHash = math.hash(new float4(first.Velocity(), first.Energy(), first.Friction(), (uint)points.Length));
            uint lastHash = math.hash(new float4(last.Velocity(), last.Energy(), last.Friction(), firstHash));
            return lastHash;
        }
    }
}
