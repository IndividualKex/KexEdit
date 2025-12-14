using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using CorePoint = KexEdit.Core.Point;
using CoreKeyframe = KexEdit.Core.Keyframe;

namespace KexEdit.Native.RustCore {
    public static class RustForceNode {
        private const string DLL_NAME = "kexedit_core";
        private const int MAX_POINTS = 1_000_000;

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_force_build(
            RustPoint* anchor,
            float duration,
            int duration_type,
            bool driven,
            RustKeyframe* roll_speed,
            nuint roll_speed_len,
            RustKeyframe* normal_force,
            nuint normal_force_len,
            RustKeyframe* lateral_force,
            nuint lateral_force_len,
            RustKeyframe* driven_velocity,
            nuint driven_velocity_len,
            RustKeyframe* heart_offset,
            nuint heart_offset_len,
            RustKeyframe* friction,
            nuint friction_len,
            RustKeyframe* resistance,
            nuint resistance_len,
            float anchor_heart,
            float anchor_friction,
            float anchor_resistance,
            RustPoint* out_points,
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

            RustPoint rustAnchor = RustPoint.FromCore(anchor);

            var rollSpeedRust = ConvertKeyframes(rollSpeed, Allocator.Temp);
            var normalForceRust = ConvertKeyframes(normalForce, Allocator.Temp);
            var lateralForceRust = ConvertKeyframes(lateralForce, Allocator.Temp);
            var drivenVelocityRust = ConvertKeyframes(drivenVelocity, Allocator.Temp);
            var heartOffsetRust = ConvertKeyframes(heartOffset, Allocator.Temp);
            var frictionRust = ConvertKeyframes(friction, Allocator.Temp);
            var resistanceRust = ConvertKeyframes(resistance, Allocator.Temp);

            var outPoints = new NativeArray<RustPoint>(MAX_POINTS, Allocator.Temp);
            nuint outLen = 0;

            int returnCode = kexedit_force_build(
                &rustAnchor,
                duration,
                durationType,
                driven,
                (RustKeyframe*)rollSpeedRust.GetUnsafePtr(),
                (nuint)rollSpeedRust.Length,
                (RustKeyframe*)normalForceRust.GetUnsafePtr(),
                (nuint)normalForceRust.Length,
                (RustKeyframe*)lateralForceRust.GetUnsafePtr(),
                (nuint)lateralForceRust.Length,
                (RustKeyframe*)drivenVelocityRust.GetUnsafePtr(),
                (nuint)drivenVelocityRust.Length,
                (RustKeyframe*)heartOffsetRust.GetUnsafePtr(),
                (nuint)heartOffsetRust.Length,
                (RustKeyframe*)frictionRust.GetUnsafePtr(),
                (nuint)frictionRust.Length,
                (RustKeyframe*)resistanceRust.GetUnsafePtr(),
                (nuint)resistanceRust.Length,
                anchorHeart,
                anchorFriction,
                anchorResistance,
                (RustPoint*)outPoints.GetUnsafePtr(),
                &outLen,
                (nuint)MAX_POINTS
            );

            rollSpeedRust.Dispose();
            normalForceRust.Dispose();
            lateralForceRust.Dispose();
            drivenVelocityRust.Dispose();
            heartOffsetRust.Dispose();
            frictionRust.Dispose();
            resistanceRust.Dispose();

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

        private static NativeArray<RustKeyframe> ConvertKeyframes(in NativeArray<CoreKeyframe> source, Allocator allocator) {
            var result = new NativeArray<RustKeyframe>(source.Length, allocator);
            for (int i = 0; i < source.Length; i++) {
                result[i] = RustKeyframe.FromCore(source[i]);
            }
            return result;
        }
    }
}
