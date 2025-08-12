using Unity.Entities;
using Unity.Collections;

namespace KexEdit {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(TrackSegmentInitializationSystem))]
    public partial class TrackBuffersInitializationSystem : SystemBase {
        protected override void OnUpdate() {
            using var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI
                .Query<Segment>()
                .WithNone<TrackStyleBuffers>()
                .WithEntityAccess()
            ) {
                ecb.AddComponent(entity, new TrackStyleBuffers());
            }
            ecb.Playback(EntityManager);
        }
    }
}
