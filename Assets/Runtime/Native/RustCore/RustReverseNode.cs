using System.Runtime.InteropServices;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Native.RustCore {
    public static class RustReverseNode {
        private const string DLL_NAME = "kexedit_core";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_reverse_build(
            RustPoint* anchor,
            RustPoint* out_point
        );

        public static unsafe int Build(
            in CorePoint anchor,
            out CorePoint result
        ) {
            var rustAnchor = RustPoint.FromCore(anchor);
            RustPoint rustOut;

            int returnCode = kexedit_reverse_build(&rustAnchor, &rustOut);

            result = rustOut.ToCore();
            return returnCode;
        }
    }
}
