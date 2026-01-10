using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using CoreKeyframe = KexEdit.Sim.Keyframe;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Native.RustCore {
    public static class RustForceNode {
        private const string DLL_NAME = "kexedit_core";

        // Reasonable initial size - most sections produce 100-2000 points
        // Can grow if needed, but avoids massive 92MB allocation
        private const int INITIAL_CAPACITY = 4096;

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_force_build(
            CorePoint* anchor,
            float duration,
            int duration_type,
            bool driven,
            CoreKeyframe* roll_speed,
            nuint roll_speed_len,
            CoreKeyframe* normal_force,
            nuint normal_force_len,
            CoreKeyframe* lateral_force,
            nuint lateral_force_len,
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
            float duration,
            int durationType,
            bool driven,
            in NativeArray<CoreKeyframe> rollSpeed,
            in NativeArray<CoreKeyframe> normalForce,
            in NativeArray<CoreKeyframe> lateralForce,
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

            // Ensure capacity for output - start with reasonable size
            if (result.Capacity < INITIAL_CAPACITY) {
                result.Capacity = INITIAL_CAPACITY;
            }

            fixed (CorePoint* anchorPtr = &anchor) {
                nuint outLen = 0;

                // Get direct pointers to keyframe arrays (zero-copy - identical layout)
                CoreKeyframe* rollSpeedPtr = rollSpeed.Length > 0 ? (CoreKeyframe*)rollSpeed.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* normalForcePtr = normalForce.Length > 0 ? (CoreKeyframe*)normalForce.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* lateralForcePtr = lateralForce.Length > 0 ? (CoreKeyframe*)lateralForce.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* drivenVelocityPtr = drivenVelocity.Length > 0 ? (CoreKeyframe*)drivenVelocity.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* heartOffsetPtr = heartOffset.Length > 0 ? (CoreKeyframe*)heartOffset.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* frictionPtr = friction.Length > 0 ? (CoreKeyframe*)friction.GetUnsafeReadOnlyPtr() : null;
                CoreKeyframe* resistancePtr = resistance.Length > 0 ? (CoreKeyframe*)resistance.GetUnsafeReadOnlyPtr() : null;

                // Write directly into NativeList's buffer (zero-copy output)
                int returnCode = kexedit_force_build(
                    anchorPtr,
                    duration,
                    durationType,
                    driven,
                    rollSpeedPtr,
                    (nuint)rollSpeed.Length,
                    normalForcePtr,
                    (nuint)normalForce.Length,
                    lateralForcePtr,
                    (nuint)lateralForce.Length,
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
                    // Buffer too small - grow and retry
                    int requiredCapacity = result.Capacity * 2;
                    while (requiredCapacity < 1_000_000) {
                        result.Capacity = requiredCapacity;

                        returnCode = kexedit_force_build(
                            anchorPtr,
                            duration,
                            durationType,
                            driven,
                            rollSpeedPtr,
                            (nuint)rollSpeed.Length,
                            normalForcePtr,
                            (nuint)normalForce.Length,
                            lateralForcePtr,
                            (nuint)lateralForce.Length,
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

                // Set the length directly (Rust wrote into the buffer)
                result.ResizeUninitialized((int)outLen);
            }

            return 0;
        }
    }
}
