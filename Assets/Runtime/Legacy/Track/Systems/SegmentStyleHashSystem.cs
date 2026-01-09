using KexEdit.Sim.Schema;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using CoreKeyframe = KexEdit.Sim.Keyframe;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateBefore(typeof(TrackSegmentInitializationSystem))]
    [BurstCompile]
    public partial struct SegmentStyleHashSystem : ISystem {
        private ComponentLookup<CoasterData> _coasterDataLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            _coasterDataLookup = state.GetComponentLookup<CoasterData>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            _coasterDataLookup.Update(ref state);

            foreach (var (hash, node, overrides, coasterRef, entity) in SystemAPI
                .Query<RefRW<TrackStyleHash>, Node, PropertyOverrides, CoasterReference>()
                .WithEntityAccess()
            ) {
                ref var hashRef = ref hash.ValueRW;

                if (overrides.TrackStyle) {
                    if (_coasterDataLookup.TryGetComponent(coasterRef, out var coasterData)) {
                        hashRef = CalculateStyleHash(in coasterData.Value.Keyframes, node.Id);
                    }
                    else {
                        hashRef = 13;
                    }
                }
                else {
                    hashRef = CalculatePointHash(SystemAPI.GetBuffer<CorePointBuffer>(entity));
                }
            }
        }

        private uint CalculateStyleHash(in KexEdit.Sim.Schema.KeyframeStore keyframes, uint nodeId) {
            if (!keyframes.TryGet(nodeId, PropertyId.TrackStyle, out var slice)) return 13;
            if (slice.Length == 0) return 13;

            uint hash = 13;
            for (int i = 0; i < slice.Length; i++) {
                var keyframe = slice[i];
                hash = math.hash(new float4(
                    keyframe.Time,
                    keyframe.Value,
                    (float)(byte)keyframe.OutInterpolation,
                    hash
                ));
            }
            return hash;
        }

        private uint CalculatePointHash(DynamicBuffer<CorePointBuffer> points) {
            if (points.Length == 0) return 17;
            if (points.Length == 1) {
                var point = points[0];
                return math.hash(new float4(point.Velocity(), point.NormalForce(), point.Friction(), 1));
            }

            var first = points[0];
            var last = points[^1];
            uint firstHash = math.hash(new float4(first.Velocity(), first.NormalForce(), first.Friction(), (uint)points.Length));
            uint lastHash = math.hash(new float4(last.Velocity(), last.NormalForce(), last.Friction(), firstHash));
            return lastHash;
        }
    }
}
