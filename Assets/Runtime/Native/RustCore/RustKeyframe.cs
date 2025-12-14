using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using CoreKeyframe = KexEdit.Core.Keyframe;
using CoreInterpolationType = KexEdit.Core.InterpolationType;

namespace KexEdit.Native.RustCore {
    [StructLayout(LayoutKind.Sequential)]
    public struct RustKeyframe {
        private const string DLL_NAME = "kexedit_core";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern float kexedit_keyframe_evaluate(
            RustKeyframe* keyframes,
            nuint keyframes_len,
            float t,
            float default_value
        );

        public static unsafe float Evaluate(in NativeArray<RustKeyframe> keyframes, float t, float defaultValue) {
            if (!keyframes.IsCreated || keyframes.Length == 0) {
                return kexedit_keyframe_evaluate(null, 0, t, defaultValue);
            }
            return kexedit_keyframe_evaluate(
                (RustKeyframe*)keyframes.GetUnsafeReadOnlyPtr(),
                (nuint)keyframes.Length,
                t,
                defaultValue
            );
        }

        public float Time;
        public float Value;
        public int InInterpolation;
        public int OutInterpolation;
        public float InTangent;
        public float OutTangent;
        public float InWeight;
        public float OutWeight;

        public static RustKeyframe FromCore(in CoreKeyframe keyframe) {
            return new RustKeyframe {
                Time = keyframe.Time,
                Value = keyframe.Value,
                InInterpolation = (int)keyframe.InInterpolation,
                OutInterpolation = (int)keyframe.OutInterpolation,
                InTangent = keyframe.InTangent,
                OutTangent = keyframe.OutTangent,
                InWeight = keyframe.InWeight,
                OutWeight = keyframe.OutWeight
            };
        }

        public CoreKeyframe ToCore() {
            return new CoreKeyframe(
                Time,
                Value,
                (CoreInterpolationType)InInterpolation,
                (CoreInterpolationType)OutInterpolation,
                InTangent,
                OutTangent,
                InWeight,
                OutWeight
            );
        }
    }
}
