using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using CoreKeyframe = KexEdit.Sim.Keyframe;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Native.RustCore {
    public static class RustCopyPathNode {
        private const string DLL_NAME = "kexedit_core";
        private const int INITIAL_CAPACITY = 4096;

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_copy_path_build(
            CorePoint* anchor,
            CorePoint* source_path,
            nuint source_path_len,
            float start,
            float end,
            bool driven,
            CoreKeyframe* driven_velocity,
            nuint driven_velocity_len,
            CoreKeyframe* heart_offset,
            nuint heart_offset_len,
            CoreKeyframe* friction,
            nuint friction_len,
            CoreKeyframe* resistance,
            nuint resistance_len,
            float anchor_heart,
            float anchor_friction,
            float anchor_resistance,
            CorePoint* out_points,
            nuint* out_len,
            nuint max_len
        );

        public static unsafe int Build(
            in CorePoint anchor,
            in NativeList<CorePoint> sourcePath,
            float start,
            float end,
            bool driven,
            in NativeArray<CoreKeyframe> drivenVelocity,
            in NativeArray<CoreKeyframe> heartOffset,
            in NativeArray<CoreKeyframe> friction,
            in NativeArray<CoreKeyframe> resistance,
            float anchorHeart,
            float anchorFriction,
            float anchorResistance,
            ref NativeList<CorePoint> result
        ) {
            result.Clear();

            if (result.Capacity < INITIAL_CAPACITY) {
                result.Capacity = INITIAL_CAPACITY;
            }

            fixed (CorePoint* anchorPtr = &anchor) {
                nuint outLen = 0;

                // Direct pointer to source path (zero-copy)
                CorePoint* sourcePathPtr = sourcePath.Length > 0 ? (CorePoint*)sourcePath.GetUnsafeReadOnlyPtr() : null;

                CoreKeyframe* drivenVelocityPtr = drivenVelocity.Length > 0 ? (CoreKeyframe*)drivenVelocity.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* heartOffsetPtr = heartOffset.Length > 0 ? (CoreKeyframe*)heartOffset.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* frictionPtr = friction.Length > 0 ? (CoreKeyframe*)friction.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* resistancePtr = resistance.Length > 0 ? (CoreKeyframe*)resistance.GetUnsafeReadOnlyPtr() : null;

                int returnCode = kexedit_copy_path_build(
                    anchorPtr,
                    sourcePathPtr,
                    (nuint)sourcePath.Length,
                    start,
                    end,
                    driven,
                    drivenVelocityPtr,
                    (nuint)drivenVelocity.Length,
                    heartOffsetPtr,
                    (nuint)heartOffset.Length,
                    frictionPtr,
                    (nuint)friction.Length,
                    resistancePtr,
                    (nuint)resistance.Length,
                    anchorHeart,
                    anchorFriction,
                    anchorResistance,
                    (CorePoint*)result.GetUnsafePtr(),
                    &outLen,
                    (nuint)result.Capacity
                );

                if (returnCode == -3) {
                    int requiredCapacity = result.Capacity * 2;
                    while (requiredCapacity < 1_000_000) {
                        result.Capacity = requiredCapacity;
                        returnCode = kexedit_copy_path_build(
                            anchorPtr,
                            sourcePathPtr,
                            (nuint)sourcePath.Length,
                            start,
                            end,
                            driven,
                            drivenVelocityPtr,
                            (nuint)drivenVelocity.Length,
                            heartOffsetPtr,
                            (nuint)heartOffset.Length,
                            frictionPtr,
                            (nuint)friction.Length,
                            resistancePtr,
                            (nuint)resistance.Length,
                            anchorHeart,
                            anchorFriction,
                            anchorResistance,
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
            }

            return 0;
        }
    }
}
