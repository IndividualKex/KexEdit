using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace KexEdit.Spline.Rendering {
    [StructLayout(LayoutKind.Sequential)]
    public struct GPUSplinePoint {
        public float Arc;
        public float3 Position;
        public float3 Direction;
        public float3 Normal;
        public float3 Lateral;

        public static GPUSplinePoint FromSplinePoint(in SplinePoint p) {
            return new GPUSplinePoint {
                Arc = p.Arc,
                Position = p.Position,
                Direction = p.Direction,
                Normal = p.Normal,
                Lateral = p.Lateral
            };
        }

        public const int Stride = sizeof(float) + 4 * 3 * sizeof(float); // 52 bytes
    }
}
