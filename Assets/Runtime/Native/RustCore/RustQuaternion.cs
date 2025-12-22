using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace KexEdit.Native.RustCore {
    [StructLayout(LayoutKind.Sequential)]
    public struct RustQuaternion {
        public float x;
        public float y;
        public float z;
        public float w;

        [DllImport("kexedit_core", CallingConvention = CallingConvention.Cdecl)]
        private static extern RustQuaternion kexedit_quat_mul(RustQuaternion a, RustQuaternion b);

        [DllImport("kexedit_core", CallingConvention = CallingConvention.Cdecl)]
        private static extern float3 kexedit_quat_mul_vec(RustQuaternion q, float3 v);

        public static RustQuaternion Mul(RustQuaternion a, RustQuaternion b) => kexedit_quat_mul(a, b);
        public static float3 MulVec(RustQuaternion q, float3 v) => kexedit_quat_mul_vec(q, v);
    }
}
