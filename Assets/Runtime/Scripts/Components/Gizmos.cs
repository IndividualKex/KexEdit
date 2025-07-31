using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit {
    public struct Gizmos : IComponentData {        
        public float2 VelocityRange;
        public float2 NormalForceRange;
        public float2 LateralForceRange;
        public float2 RollSpeedRange;
        public float2 PitchSpeedRange;
        public float2 YawSpeedRange;
        public float2 CurvatureRange;
        public bool DrawGizmos;
        
        public static Gizmos Default => new() {
            VelocityRange = new float2(0f, 50f),
            NormalForceRange = new float2(-2f, 5f),
            LateralForceRange = new float2(-2f, 2f),
            RollSpeedRange = new float2(-3f, 3f),
            PitchSpeedRange = new float2(-1f, 1f),
            YawSpeedRange = new float2(-1f, 1f),
            CurvatureRange = new float2(0f, 1f),
            DrawGizmos = false,
        };
    }
}
