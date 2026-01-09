using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Sim {
    [BurstCompile]
    public readonly struct Point {
        public readonly float3 HeartPosition;
        public readonly float3 Direction;
        public readonly float3 Normal;
        public readonly float3 Lateral;
        public readonly float Velocity;
        public readonly float NormalForce;
        public readonly float LateralForce;
        public readonly float HeartArc;
        public readonly float SpineArc;
        public readonly float HeartAdvance;
        public readonly float FrictionOrigin;
        public readonly float RollSpeed;
        public readonly float HeartOffset;
        public readonly float Friction;
        public readonly float Resistance;

        public Point(
            in float3 heartPosition,
            in float3 direction,
            in float3 normal,
            in float3 lateral,
            float velocity,
            float normalForce,
            float lateralForce,
            float heartArc,
            float spineArc,
            float heartAdvance,
            float frictionOrigin,
            float rollSpeed = 0f,
            float heartOffset = 0f,
            float friction = 0f,
            float resistance = 0f
        ) {
            HeartPosition = heartPosition;
            Direction = direction;
            Normal = normal;
            Lateral = lateral;
            Velocity = velocity;
            NormalForce = normalForce;
            LateralForce = lateralForce;
            HeartArc = heartArc;
            SpineArc = spineArc;
            HeartAdvance = heartAdvance;
            FrictionOrigin = frictionOrigin;
            RollSpeed = rollSpeed;
            HeartOffset = heartOffset;
            Friction = friction;
            Resistance = resistance;
        }

        public float Roll => math.atan2(Lateral.y, -Normal.y);
        public Frame Frame => new(Direction, Normal, Lateral);
        public float3 SpinePosition(float offset) => HeartPosition + Normal * offset;

        public static Point Create(
            in float3 heartPosition,
            in float3 direction,
            float roll,
            float velocity,
            float heartOffset,
            float friction = 0f,
            float resistance = 0f
        ) {
            Frame frame = Frame.FromDirectionAndRoll(direction, roll);

            return new Point(
                heartPosition: heartPosition,
                direction: frame.Direction,
                normal: frame.Normal,
                lateral: frame.Lateral,
                velocity: velocity,
                normalForce: 1f,
                lateralForce: 0f,
                heartArc: 0f,
                spineArc: 0f,
                heartAdvance: 0f,
                frictionOrigin: 0f,
                rollSpeed: 0f,
                heartOffset: heartOffset,
                friction: friction,
                resistance: resistance
            );
        }

        public static Point Default => Create(
            heartPosition: new float3(0f, 3f, 0f),
            direction: math.back(),
            roll: 0f,
            velocity: 10f,
            heartOffset: 1.1f,
            friction: 0f,
            resistance: 0f
        );

        public Point WithFrictionOrigin(float newOrigin) {
            return new Point(
                HeartPosition, Direction, Normal, Lateral,
                Velocity, NormalForce, LateralForce,
                HeartArc, SpineArc, HeartAdvance,
                newOrigin, RollSpeed, HeartOffset, Friction, Resistance
            );
        }

        public Point WithForces(float newNormalForce, float newLateralForce) {
            return new Point(
                HeartPosition, Direction, Normal, Lateral,
                Velocity, newNormalForce, newLateralForce,
                HeartArc, SpineArc, HeartAdvance,
                FrictionOrigin, RollSpeed, HeartOffset, Friction, Resistance
            );
        }

        public Point WithVelocity(float newVelocity, float heartOffset, float friction, bool resetFriction) {
            float newFrictionOrigin = resetFriction ? SpineArc : FrictionOrigin;
            return new Point(
                HeartPosition, Direction, Normal, Lateral,
                newVelocity, NormalForce, LateralForce,
                HeartArc, SpineArc, HeartAdvance,
                newFrictionOrigin, RollSpeed, HeartOffset, Friction, Resistance
            );
        }
    }
}
