using System.Runtime.InteropServices;
using CorePoint = KexEdit.Core.Point;

namespace KexEdit.Native.RustCore {
    [StructLayout(LayoutKind.Sequential)]
    public struct RustPoint {
        public RustFloat3 SpinePosition;
        public RustFloat3 Direction;
        public RustFloat3 Normal;
        public RustFloat3 Lateral;
        public float Velocity;
        public float Energy;
        public float NormalForce;
        public float LateralForce;
        public float HeartArc;
        public float SpineArc;
        public float SpineAdvance;
        public float FrictionOrigin;
        public float RollSpeed;
        public float HeartOffset;
        public float Friction;
        public float Resistance;

        public static RustPoint FromCore(in CorePoint point) {
            return new RustPoint {
                SpinePosition = RustFloat3.FromUnity(point.SpinePosition),
                Direction = RustFloat3.FromUnity(point.Direction),
                Normal = RustFloat3.FromUnity(point.Normal),
                Lateral = RustFloat3.FromUnity(point.Lateral),
                Velocity = point.Velocity,
                Energy = point.Energy,
                NormalForce = point.NormalForce,
                LateralForce = point.LateralForce,
                HeartArc = point.HeartArc,
                SpineArc = point.SpineArc,
                SpineAdvance = point.SpineAdvance,
                FrictionOrigin = point.FrictionOrigin,
                RollSpeed = point.RollSpeed,
                HeartOffset = point.HeartOffset,
                Friction = point.Friction,
                Resistance = point.Resistance
            };
        }

        public CorePoint ToCore() {
            return new CorePoint(
                spinePosition: SpinePosition.ToUnity(),
                direction: Direction.ToUnity(),
                normal: Normal.ToUnity(),
                lateral: Lateral.ToUnity(),
                velocity: Velocity,
                energy: Energy,
                normalForce: NormalForce,
                lateralForce: LateralForce,
                heartArc: HeartArc,
                spineArc: SpineArc,
                spineAdvance: SpineAdvance,
                frictionOrigin: FrictionOrigin,
                rollSpeed: RollSpeed,
                heartOffset: HeartOffset,
                friction: Friction,
                resistance: Resistance
            );
        }
    }
}
