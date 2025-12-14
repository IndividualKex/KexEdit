using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Core {
    [BurstCompile]
    public readonly struct Point {
        public readonly float3 SpinePosition;
        public readonly float3 Direction;
        public readonly float3 Normal;
        public readonly float3 Lateral;
        public readonly float Velocity;
        public readonly float Energy;
        public readonly float NormalForce;
        public readonly float LateralForce;
        public readonly float HeartArc;
        public readonly float SpineArc;
        public readonly float SpineAdvance;
        public readonly float FrictionOrigin;
        public readonly float RollSpeed;
        public readonly float HeartOffset;
        public readonly float Friction;
        public readonly float Resistance;

        public Point(
            in float3 spinePosition,
            in float3 direction,
            in float3 normal,
            in float3 lateral,
            float velocity,
            float energy,
            float normalForce,
            float lateralForce,
            float heartArc,
            float spineArc,
            float spineAdvance,
            float frictionOrigin,
            float rollSpeed = 0f,
            float heartOffset = 0f,
            float friction = 0f,
            float resistance = 0f
        ) {
            SpinePosition = spinePosition;
            Direction = direction;
            Normal = normal;
            Lateral = lateral;
            Velocity = velocity;
            Energy = energy;
            NormalForce = normalForce;
            LateralForce = lateralForce;
            HeartArc = heartArc;
            SpineArc = spineArc;
            SpineAdvance = spineAdvance;
            FrictionOrigin = frictionOrigin;
            RollSpeed = rollSpeed;
            HeartOffset = heartOffset;
            Friction = friction;
            Resistance = resistance;
        }

        public float Roll => math.atan2(Lateral.y, -Normal.y);
        public Frame Frame => new(Direction, Normal, Lateral);
        public float3 HeartPosition(float offset) => SpinePosition + Normal * offset;

        public static Point Create(
            in float3 spinePosition,
            in float3 direction,
            float roll,
            float velocity,
            float heartOffset,
            float friction = 0f,
            float resistance = 0f
        ) {
            Frame frame = Frame.FromDirectionAndRoll(direction, roll);
            float3 heartPos = frame.HeartPosition(spinePosition, heartOffset);
            float energy = 0.5f * velocity * velocity + Sim.G * heartPos.y;

            return new Point(
                spinePosition: spinePosition,
                direction: frame.Direction,
                normal: frame.Normal,
                lateral: frame.Lateral,
                velocity: velocity,
                energy: energy,
                normalForce: 1f,
                lateralForce: 0f,
                heartArc: 0f,
                spineArc: 0f,
                spineAdvance: 0f,
                frictionOrigin: 0f,
                rollSpeed: 0f,
                heartOffset: heartOffset,
                friction: friction,
                resistance: resistance
            );
        }

        public static Point Default => Create(
            spinePosition: new float3(0f, 3f, 0f),
            direction: math.back(),
            roll: 0f,
            velocity: 10f,
            heartOffset: 1.1f,
            friction: 0f,
            resistance: 0f
        );

        public Point WithFrictionOrigin(float newOrigin) {
            return new Point(
                SpinePosition, Direction, Normal, Lateral,
                Velocity, Energy, NormalForce, LateralForce,
                HeartArc, SpineArc, SpineAdvance,
                newOrigin, RollSpeed, HeartOffset, Friction, Resistance
            );
        }

        public Point WithVelocityAndEnergy(float newVelocity, float newEnergy, float newFrictionOrigin) {
            return new Point(
                SpinePosition, Direction, Normal, Lateral,
                newVelocity, newEnergy, NormalForce, LateralForce,
                HeartArc, SpineArc, SpineAdvance,
                newFrictionOrigin, RollSpeed, HeartOffset, Friction, Resistance
            );
        }

        public Point WithForces(float newNormalForce, float newLateralForce) {
            return new Point(
                SpinePosition, Direction, Normal, Lateral,
                Velocity, Energy, newNormalForce, newLateralForce,
                HeartArc, SpineArc, SpineAdvance,
                FrictionOrigin, RollSpeed, HeartOffset, Friction, Resistance
            );
        }

        public Point WithVelocity(float newVelocity, float heartOffset, float friction, bool resetFriction) {
            float newFrictionOrigin = resetFriction ? HeartArc : FrictionOrigin;
            float centerY = HeartPosition(heartOffset * 0.9f).y;
            float frictionDistance = HeartArc - newFrictionOrigin;
            float newEnergy = Sim.ComputeTotalEnergy(newVelocity, centerY, frictionDistance, friction);
            return new Point(
                SpinePosition, Direction, Normal, Lateral,
                newVelocity, newEnergy, NormalForce, LateralForce,
                HeartArc, SpineArc, SpineAdvance,
                newFrictionOrigin, RollSpeed, HeartOffset, Friction, Resistance
            );
        }
    }
}
