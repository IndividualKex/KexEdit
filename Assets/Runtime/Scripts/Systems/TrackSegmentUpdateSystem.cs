using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class TrackSegmentUpdateSystem : SystemBase {
        protected override void OnUpdate() {
            float deltaTime = SystemAPI.Time.DeltaTime;
            float t = math.saturate(deltaTime * 30f);

            foreach (var (style, segment, section, renderRW, blendRW) in SystemAPI
                .Query<TrackStyle, Segment, SectionReference, RefRW<Render>, RefRW<SelectedBlend>>()
            ) {
                if (!SystemAPI.HasComponent<Node>(section)) continue;

                var node = SystemAPI.GetComponent<Node>(section);
                var sectionRender = SystemAPI.GetComponent<Render>(section);
                renderRW.ValueRW = sectionRender;
                blendRW.ValueRW.Value = math.lerp(blendRW.ValueRW.Value, node.Selected ? 1f : 0f, t);

                if (style.CurrentBuffers != null && style.CurrentBuffers.Count > 1) {
                    ref var renderedHash = ref SystemAPI.GetComponentRW<RenderedStyleHash>(section).ValueRW;
                    if (segment.StyleHash != renderedHash) {
                        uint sectionStyleHash = SystemAPI.GetComponent<StyleHash>(section);
                        if (segment.StyleHash == sectionStyleHash) {
                            renderedHash = sectionStyleHash;
                        }
                    }
                }
            }
        }
    }
}
