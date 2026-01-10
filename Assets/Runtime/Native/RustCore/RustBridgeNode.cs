using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using CoreKeyframe = KexEdit.Sim.Keyframe;
using CorePoint = KexEdit.Sim.Point;

namespace KexEdit.Native.RustCore {
    public static class RustBridgeNode {
        private const string DLL_NAME = "kexedit_core";
        private const int MAX_POINTS = 1_000_000;

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int kexedit_bridge_build(
            RustPoint* anchor,
            RustPoint* target_anchor,
            float in_weight,
            float out_weight,
            bool driven,
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
            in CorePoint targetAnchor,
            float inWeight,
            float outWeight,
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

            RustPoint rustAnchor = RustPoint.FromCore(anchor);
            RustPoint rustTarget = RustPoint.FromCore(targetAnchor);

            var drivenVelocityRust = ConvertKeyframes(drivenVelocity, Allocator.Temp);
            var heartOffsetRust = ConvertKeyframes(heartOffset, Allocator.Temp);
            var frictionRust = ConvertKeyframes(friction, Allocator.Temp);
            var resistanceRust = ConvertKeyframes(resistance, Allocator.Temp);

            var outPoints = new NativeArray<RustPoint>(MAX_POINTS, Allocator.Temp);
            nuint outLen = 0;

            int returnCode = kexedit_bridge_build(
                &rustAnchor,
                &rustTarget,
                inWeight,
                outWeight,
                driven,
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
