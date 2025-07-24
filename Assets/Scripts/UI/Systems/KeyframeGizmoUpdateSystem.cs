using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using static KexEdit.Constants;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct KeyframeGizmoUpdateSystem : ISystem {
        [BurstCompile]
        public void OnCreate(ref SystemState state) {
            state.RequireForUpdate<PreferencesSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            if (!SystemAPI.GetSingleton<PreferencesSingleton>().ShowGizmos) return;

            float deltaTime = SystemAPI.Time.DeltaTime;
            float t = math.saturate(deltaTime * 30f);

            foreach (var (gizmo, transform, blendRW, entity) in SystemAPI
                .Query<KeyframeGizmo, RefRW<LocalTransform>, RefRW<KeyframeSelectedBlend>>()
                .WithAll<KeyframeGizmoTag>()
                .WithEntityAccess()
            ) {
                if (!SystemAPI.HasComponent<Node>(gizmo.Section)) continue;

                var targetKeyframe = state.EntityManager.GetKeyframe(gizmo.Section, gizmo.PropertyType, gizmo.KeyframeId);
                if (!targetKeyframe.HasValue) continue;

                float3 worldPosition = CalculateWorldPosition(ref state, gizmo.Section, targetKeyframe.Value.Time);
                transform.ValueRW.Position = worldPosition;

                var node = SystemAPI.GetComponent<Node>(gizmo.Section);
                float targetBlend = (targetKeyframe.Value.Selected && node.Selected) ? 1f : 0f;
                blendRW.ValueRW.Value = math.lerp(blendRW.ValueRW.Value, targetBlend, t);
            }
        }

        private float3 CalculateWorldPosition(ref SystemState state, Entity section, float time) {
            if (!SystemAPI.HasBuffer<Point>(section)) return float3.zero;

            var pointBuffer = SystemAPI.GetBuffer<Point>(section);
            if (pointBuffer.Length < 2) return float3.zero;

            float position = TimeToPosition(ref state, section, time);
            position = math.clamp(position, 0, pointBuffer.Length - 1);

            int index = (int)math.floor(position);
            int nextIndex = math.min(index + 1, pointBuffer.Length - 1);

            if (index == nextIndex) {
                return pointBuffer[index].Value.Position;
            }

            float t = position - index;
            PointData p0 = pointBuffer[index].Value;
            PointData p1 = pointBuffer[nextIndex].Value;

            return math.lerp(p0.Position, p1.Position, t);
        }

        private float TimeToPosition(ref SystemState state, Entity section, float time) {
            if (!SystemAPI.HasComponent<Duration>(section)) {
                return time * HZ;
            }

            var duration = SystemAPI.GetComponent<Duration>(section);
            if (duration.Type == DurationType.Time) {
                return time * HZ;
            }

            if (!SystemAPI.HasComponent<Anchor>(section)) {
                return time * HZ;
            }

            var pointBuffer = SystemAPI.GetBuffer<Point>(section);
            if (pointBuffer.Length < 2) return 0f;

            float targetDistance = SystemAPI.GetComponent<Anchor>(section).Value.TotalLength + time;

            for (int i = 0; i < pointBuffer.Length - 1; i++) {
                float currentDistance = pointBuffer[i].Value.TotalLength;
                float nextDistance = pointBuffer[i + 1].Value.TotalLength;
                if (targetDistance >= currentDistance && targetDistance <= nextDistance) {
                    float t = (nextDistance - currentDistance) > 0 ?
                        (targetDistance - currentDistance) / (nextDistance - currentDistance) : 0f;
                    return i + t;
                }
            }

            return pointBuffer.Length - 1;
        }
    }
}
