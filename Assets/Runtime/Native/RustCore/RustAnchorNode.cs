using System.Runtime.InteropServices;
using Unity.Mathematics;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Native.RustCore {
    public static class RustAnchorNode {
        private const string DLL_NAME = "kexedit_core";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_anchor_build(
            RustFloat3* position,
            float pitch,
            float yaw,
            float roll,
            float velocity,
            float heart_offset,
            float friction,
            float resistance,
            RustPoint* out_point
        );

        public static unsafe int Build(
            in float3 position,
            float pitch,
            float yaw,
            float roll,
            float velocity,
            float heartOffset,
            float friction,
            float resistance,
            out CorePoint result
        ) {
            var rustPos = RustFloat3.FromUnity(position);
            RustPoint rustOut;

            int returnCode = kexedit_anchor_build(
                &rustPos,
                pitch,
                yaw,
                roll,
                velocity,
                heartOffset,
                friction,
                resistance,
                &rustOut
            );

            result = rustOut.ToCore();
            return returnCode;
        }
    }
}
