using System.Runtime.InteropServices;

namespace KexEdit.Native.RustCore {
    [StructLayout(LayoutKind.Sequential)]
    public struct RustFloat3 {
        public float X;
        public float Y;
        public float Z;

        public RustFloat3(float x, float y, float z) {
            X = x;
            Y = y;
            Z = z;
        }

        public static RustFloat3 FromUnity(in Unity.Mathematics.float3 v) {
            return new RustFloat3(v.x, v.y, v.z);
        }

        public Unity.Mathematics.float3 ToUnity() {
            return new Unity.Mathematics.float3(X, Y, Z);
        }
    }
}
