using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Native.RustCore {
    public static class RustReversePathNode {
        private const string DLL_NAME = "kexedit_core";
        private const int INITIAL_CAPACITY = 4096;

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_reverse_path_build(
            CorePoint* source_path,
            nuint source_path_len,
            CorePoint* out_points,
            nuint* out_len,
            nuint max_len
        );

        public static unsafe int Build(
            in NativeList<CorePoint> sourcePath,
            ref NativeList<CorePoint> result
        ) {
            result.Clear();

            if (result.Capacity < INITIAL_CAPACITY) {
                result.Capacity = INITIAL_CAPACITY;
            }

            nuint outLen = 0;

            // Direct pointer to source path (zero-copy)
            CorePoint* sourcePathPtr = sourcePath.Length > 0 ? (CorePoint*)sourcePath.GetUnsafeReadOnlyPtr() : null;

            int returnCode = kexedit_reverse_path_build(
                sourcePathPtr,
                (nuint)sourcePath.Length,
                (CorePoint*)result.GetUnsafePtr(),
                &outLen,
                (nuint)result.Capacity
            );

            if (returnCode == -3) {
                int requiredCapacity = result.Capacity * 2;
                while (requiredCapacity < 1_000_000) {
                    result.Capacity = requiredCapacity;
                    returnCode = kexedit_reverse_path_build(
                        sourcePathPtr,
                        (nuint)sourcePath.Length,
                        (CorePoint*)result.GetUnsafePtr(),
                        &outLen,
                        (nuint)result.Capacity
                    );
                    if (returnCode != -3) break;
                    requiredCapacity *= 2;
                }
            }

            if (returnCode != 0) {
                return returnCode;
            }

            result.ResizeUninitialized((int)outLen);
            return 0;
        }
    }
}
