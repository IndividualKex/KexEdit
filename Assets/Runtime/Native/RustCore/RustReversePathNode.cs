using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Native.RustCore {
    public static class RustReversePathNode {
        private const string DLL_NAME = "kexedit_core";
        private const int MAX_POINTS = 1_000_000;

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_reverse_path_build(
            RustPoint* source_path,
            nuint source_path_len,
            RustPoint* out_points,
            nuint* out_len,
            nuint max_len
        );

        public static unsafe int Build(
            in NativeList<CorePoint> sourcePath,
            ref NativeList<CorePoint> result
        ) {
            result.Clear();

            var sourcePathRust = new NativeArray<RustPoint>(sourcePath.Length, Allocator.Temp);
            for (int i = 0; i < sourcePath.Length; i++) {
                sourcePathRust[i] = RustPoint.FromCore(sourcePath[i]);
            }

            var outPoints = new NativeArray<RustPoint>(MAX_POINTS, Allocator.Temp);
            nuint outLen = 0;

            int returnCode = kexedit_reverse_path_build(
                (RustPoint*)sourcePathRust.GetUnsafePtr(),
                (nuint)sourcePathRust.Length,
                (RustPoint*)outPoints.GetUnsafePtr(),
                &outLen,
                (nuint)MAX_POINTS
            );

            sourcePathRust.Dispose();

            if (returnCode != 0) {
                outPoints.Dispose();
                return returnCode;
            }

            for (int i = 0; i < (int)outLen; i++) {
                result.Add(outPoints[i].ToCore());
            }

            outPoints.Dispose();
            return 0;
        }
    }
}
