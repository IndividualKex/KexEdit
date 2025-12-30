using KexEdit.Document;
using KexEdit.Sim;
using KexEdit.Spline;
using KexEdit.Trains;
using KexEdit.Trains.Sim;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DocumentAggregate = KexEdit.Document.Document;
using static KexEdit.Sim.Sim;

namespace KexEdit.Legacy.Debug {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrainGizmos : SystemBase {
        private const float AxisLength = 1f;
        private const int CarCount = 5;
        private const float CarSpacing = 3f;

        private SimFollower _follower;

        protected override void OnUpdate() {
            if (!SystemAPI.TryGetSingletonEntity<Coaster>(out Entity coasterEntity)) return;
            var coasterDataLookup = SystemAPI.GetComponentLookup<CoasterData>(true);
            if (!coasterDataLookup.TryGetComponent(coasterEntity, out var coasterData)) return;
            ref readonly DocumentAggregate document = ref coasterData.Value;

            if (!document.Graph.NodeIds.IsCreated || document.Graph.NodeCount == 0) return;

            KexEdit.Track.Track.Build(in document, Allocator.Temp, out var track);

            if (track.SectionCount == 0) {
                track.Dispose();
                return;
            }

            float dt = SystemAPI.Time.DeltaTime;
            SimFollowerLogic.Advance(ref _follower, in track, dt, HZ, wrapAtEnd: true, out Point point);

            float baseArc = point.SpineArc;
            float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;
            int facing = _follower.Facing;

            int sectionIndex = track.TraversalOrder[_follower.TraversalIndex];
            for (int i = 0; i < CarCount; i++) {
                float offset = i * CarSpacing - halfSpan;
                TrainCarLogic.PositionCarWithOverhang(in track, sectionIndex, baseArc, offset, facing, out SplinePoint carPoint);
                float3 dir = carPoint.Direction * facing;
                float3 lat = carPoint.Lateral * facing;
                DrawFrame(carPoint.Position, dir, carPoint.Normal, lat, AxisLength);
            }

            track.Dispose();
        }

        private static void DrawFrame(float3 position, float3 forward, float3 up, float3 right, float size) {
            UnityEngine.Debug.DrawRay(position, right * size, Color.red, 0f);
            UnityEngine.Debug.DrawRay(position, up * size, Color.green, 0f);
            UnityEngine.Debug.DrawRay(position, forward * size, Color.blue, 0f);
        }
    }
}
