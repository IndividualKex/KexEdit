using System.Runtime.InteropServices;
using CoreFrame = KexEdit.Sim.Frame;

namespace KexEdit.Native.RustCore {
    [StructLayout(LayoutKind.Sequential)]
    public struct RustFrame {
        public RustFloat3 Direction;
        public RustFloat3 Normal;
        public RustFloat3 Lateral;

        public RustFrame(RustFloat3 direction, RustFloat3 normal, RustFloat3 lateral) {
            Direction = direction;
            Normal = normal;
            Lateral = lateral;
        }

        public static RustFrame FromCore(in CoreFrame frame) {
            return new RustFrame(
                RustFloat3.FromUnity(frame.Direction),
                RustFloat3.FromUnity(frame.Normal),
                RustFloat3.FromUnity(frame.Lateral)
            );
        }

        public CoreFrame ToCore() {
            return new CoreFrame(
                Direction.ToUnity(),
                Normal.ToUnity(),
                Lateral.ToUnity()
            );
        }
    }

    public static class RustFrameNative {
        private const string DLL_NAME = "kexedit_core";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void kexedit_frame_rotate_around(
            RustFrame* frame,
            RustFloat3* axis,
            float angle,
            RustFrame* result
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void kexedit_frame_with_roll(
            RustFrame* frame,
            float deltaRoll,
            RustFrame* result
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void kexedit_frame_with_pitch(
            RustFrame* frame,
            float deltaPitch,
            RustFrame* result
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern void kexedit_frame_with_yaw(
            RustFrame* frame,
            float deltaYaw,
            RustFrame* result
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern float kexedit_frame_roll(RustFrame* frame);

        public static unsafe RustFrame RotateAround(in RustFrame frame, in RustFloat3 axis, float angle) {
            RustFrame input = frame;
            RustFloat3 axisVal = axis;
            RustFrame result;
            kexedit_frame_rotate_around(&input, &axisVal, angle, &result);
            return result;
        }

        public static unsafe RustFrame WithRoll(in RustFrame frame, float deltaRoll) {
            RustFrame input = frame;
            RustFrame result;
            kexedit_frame_with_roll(&input, deltaRoll, &result);
            return result;
        }

        public static unsafe RustFrame WithPitch(in RustFrame frame, float deltaPitch) {
            RustFrame input = frame;
            RustFrame result;
            kexedit_frame_with_pitch(&input, deltaPitch, &result);
            return result;
        }

        public static unsafe RustFrame WithYaw(in RustFrame frame, float deltaYaw) {
            RustFrame input = frame;
            RustFrame result;
            kexedit_frame_with_yaw(&input, deltaYaw, &result);
            return result;
        }

        public static unsafe float Roll(in RustFrame frame) {
            RustFrame input = frame;
            return kexedit_frame_roll(&input);
        }
    }
}
