using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [BurstCompile]
    public partial struct TrackSegmentUpdateSystem : ISystem {
        [BurstCompile]
        public void OnUpdate(ref SystemState state) {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float t = math.saturate(deltaTime * 30f);

            foreach (var (segment, section, renderRW, blendRW) in SystemAPI
                .Query<Segment, SectionReference, RefRW<Render>, RefRW<SelectedBlend>>()
            ) {
                if (!SystemAPI.HasComponent<Node>(section)) continue;

                var node = SystemAPI.GetComponent<Node>(section);
                var sectionRender = SystemAPI.GetComponent<Render>(section);
                renderRW.ValueRW = sectionRender;
                blendRW.ValueRW.Value = math.lerp(blendRW.ValueRW.Value, node.Selected ? 1f : 0f, t);
            }
        }
    }
}
