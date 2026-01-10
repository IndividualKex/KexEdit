using System.Runtime.InteropServices;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Native.RustCore {
    public static class RustReverseNode {
        private const string DLL_NAME = "kexedit_core";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_reverse_build(
            CorePoint* anchor,
            CorePoint* out_point
        );

        public static unsafe int Build(
            in CorePoint anchor,
            out CorePoint result
        ) {
            CorePoint outPoint;

            fixed (CorePoint* anchorPtr = &anchor) {
                int returnCode = kexedit_reverse_build(anchorPtr, &outPoint);
                result = outPoint;
                return returnCode;
            }
        }
    }
}
