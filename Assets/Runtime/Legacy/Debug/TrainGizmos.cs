using KexEdit.Trains;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace KexEdit.Legacy.Debug {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TrainGizmos : SystemBase {
        private const float AxisLength = 1f;
        private const int CarCount = 5;
        private const float CarSpacing = 3f;

        protected override void OnCreate() {
            RequireForUpdate<SimFollowerSingleton>();
            RequireForUpdate<TrackSingleton>();
        }

        protected override void OnUpdate() {
            var track = SystemAPI.GetSingleton<TrackSingleton>().Value;
            var follower = SystemAPI.GetSingleton<SimFollowerSingleton>().Follower;

            float halfSpan = (CarCount - 1) * CarSpacing * 0.5f;

            for (int i = 0; i < CarCount; i++) {
                float offset = i * CarSpacing - halfSpan;
                if (!TrainCarLogic.TryGetSplinePoint(in follower, in track, offset, out var sp)) return;
                DrawFrame(sp.Position, sp.Direction, sp.Normal, sp.Lateral, AxisLength);
            }
        }

        private static void DrawFrame(float3 position, float3 forward, float3 up, float3 right, float size) {
            UnityEngine.Debug.DrawRay(position, right * size, Color.red, 0f);
            UnityEngine.Debug.DrawRay(position, up * size, Color.green, 0f);
            UnityEngine.Debug.DrawRay(position, forward * size, Color.blue, 0f);
        }
    }
}
