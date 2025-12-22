using System;
using System.Text;
using Unity.Mathematics;
using static KexEdit.Sim.Sim;

namespace KexEdit.Legacy {
    [Serializable]
    public struct PointData {
        public float3 HeartPosition;
        public float3 Direction;
        public float3 Lateral;
        public float3 Normal;
        public float Roll;
        public float Velocity;
        public float Energy;
        public float NormalForce;
        public float LateralForce;
        public float SpineAdvance;
        public float HeartAdvance;
        public float AngleFromLast;
        public float PitchFromLast;
        public float YawFromLast;
        public float RollSpeed;
        public float SpineArc;
        public float HeartArc;
        public float FrictionOrigin;
        public float HeartOffset;
        public float Friction;
        public float Resistance;
        public int Facing;

        public static PointData Create(float velocity = 10f) {
            return Create(new float3(0f, 3f, 0f), velocity);
        }

        public static PointData Create(float3 position, float velocity = 10f) {
            PointData point = new() {
                HeartPosition = position,
                Direction = math.back(),
                Lateral = math.right(),
                Normal = math.down(),
                Roll = 0f,
                Velocity = velocity,
                Energy = 0f,
                NormalForce = 1f,
                LateralForce = 0f,
                SpineAdvance = 0f,
                HeartAdvance = 0f,
                AngleFromLast = 0f,
                PitchFromLast = 0f,
                YawFromLast = 0f,
                RollSpeed = 0f,
                HeartArc = 0f,
                SpineArc = 0f,
                FrictionOrigin = 0f,
                HeartOffset = HEART_BASE,
                Friction = MAX_FORCE,
                Resistance = RESISTANCE_BASE,
                Facing = 1,
            };
            point.Energy = point.ComputeEnergy();
            return point;
        }

        public void SetPosition(float3 position) {
            HeartPosition = position;
            Energy = this.ComputeEnergy();
        }

        public void SetRoll(float degrees) {
            Roll = degrees;
            float currentPitch = this.GetPitch();
            float currentYaw = this.GetYaw();

            Direction = math.normalize(math.mul(
                quaternion.Euler(math.radians(currentPitch), math.radians(currentYaw), 0f),
                math.back()
            ));
            Lateral = math.mul(quaternion.Euler(0f, math.radians(currentYaw), 0f), math.right());

            quaternion rollQuat = quaternion.AxisAngle(Direction, math.radians(-degrees));
            Lateral = math.normalize(math.mul(rollQuat, Lateral));
            Normal = math.normalize(math.cross(Direction, Lateral));

            Energy = this.ComputeEnergy();
        }

        public void SetPitch(float degrees) {
            float currentYaw = this.GetYaw();

            Direction = math.normalize(math.mul(
                quaternion.Euler(math.radians(degrees), math.radians(currentYaw), 0f),
                math.back()
            ));

            Lateral = math.mul(quaternion.Euler(0f, math.radians(currentYaw), 0f), math.right());
            Normal = math.normalize(math.cross(Direction, Lateral));

            SetRoll(Roll);
        }

        public void SetYaw(float degrees) {
            float currentPitch = this.GetPitch();

            Direction = math.normalize(math.mul(
                quaternion.Euler(math.radians(currentPitch), math.radians(degrees), 0f),
                math.back()
            ));

            Lateral = math.mul(quaternion.Euler(0f, math.radians(degrees), 0f), math.right());
            Normal = math.normalize(math.cross(Direction, Lateral));

            SetRoll(Roll);
        }

        public void Reverse() {
            Direction = -Direction;
            Facing = -Facing;
            SetRoll(-Roll);
        }

        public void SetVelocity(float velocity, bool resetFriction = false) {
            Velocity = velocity;
            if (resetFriction) {
                FrictionOrigin = HeartArc;
            }
            Energy = this.ComputeEnergy();
        }

        public void SetHeart(float heart) {
            HeartOffset = heart;
            Energy = this.ComputeEnergy();
        }

        public void SetFriction(float friction) {
            Friction = friction;
            Energy = this.ComputeEnergy();
        }

        public void SetResistance(float resistance) {
            Resistance = resistance;
            Energy = this.ComputeEnergy();
        }

        public static PointData Lerp(PointData p0, PointData p1, float t) {
            PointData result = new() {
                HeartPosition = math.lerp(p0.HeartPosition, p1.HeartPosition, t),
                Direction = math.lerp(p0.Direction, p1.Direction, t),
                Lateral = math.lerp(p0.Lateral, p1.Lateral, t),
                Normal = math.lerp(p0.Normal, p1.Normal, t),
                Roll = math.lerp(p0.Roll, p1.Roll, t),
                Velocity = math.lerp(p0.Velocity, p1.Velocity, t),
                Energy = math.lerp(p0.Energy, p1.Energy, t),
                NormalForce = math.lerp(p0.NormalForce, p1.NormalForce, t),
                LateralForce = math.lerp(p0.LateralForce, p1.LateralForce, t),
                SpineAdvance = math.lerp(p0.SpineAdvance, p1.SpineAdvance, t),
                HeartAdvance = math.lerp(p0.HeartAdvance, p1.HeartAdvance, t),
                AngleFromLast = math.lerp(p0.AngleFromLast, p1.AngleFromLast, t),
                PitchFromLast = math.lerp(p0.PitchFromLast, p1.PitchFromLast, t),
                YawFromLast = math.lerp(p0.YawFromLast, p1.YawFromLast, t),
                RollSpeed = math.lerp(p0.RollSpeed, p1.RollSpeed, t),
                HeartArc = math.lerp(p0.HeartArc, p1.HeartArc, t),
                SpineArc = math.lerp(p0.SpineArc, p1.SpineArc, t),
                FrictionOrigin = math.lerp(p0.FrictionOrigin, p1.FrictionOrigin, t),
                HeartOffset = math.lerp(p0.HeartOffset, p1.HeartOffset, t),
                Friction = math.lerp(p0.Friction, p1.Friction, t),
                Resistance = math.lerp(p0.Resistance, p1.Resistance, t),
                Facing = p0.Facing,
            };
            return result;
        }

        public override string ToString() {
            StringBuilder sb = new();
            sb.AppendLine($"HeartPosition: {HeartPosition}");
            sb.AppendLine($"Direction: {Direction}");
            sb.AppendLine($"Lateral: {Lateral}");
            sb.AppendLine($"Normal: {Normal}");
            sb.AppendLine($"Roll: {Roll}");
            sb.AppendLine($"Velocity: {Velocity}");
            sb.AppendLine($"Energy: {Energy}");
            sb.AppendLine($"NormalForce: {NormalForce}");
            sb.AppendLine($"LateralForce: {LateralForce}");
            sb.AppendLine($"SpineAdvance: {SpineAdvance}");
            sb.AppendLine($"HeartAdvance: {HeartAdvance}");
            sb.AppendLine($"AngleFromLast: {AngleFromLast}");
            sb.AppendLine($"PitchFromLast: {PitchFromLast}");
            sb.AppendLine($"YawFromLast: {YawFromLast}");
            sb.AppendLine($"RollSpeed: {RollSpeed}");
            sb.AppendLine($"HeartArc: {HeartArc}");
            sb.AppendLine($"SpineArc: {SpineArc}");
            sb.AppendLine($"FrictionOrigin: {FrictionOrigin}");
            sb.AppendLine($"HeartOffset: {HeartOffset}");
            sb.AppendLine($"Friction: {Friction}");
            sb.AppendLine($"Resistance: {Resistance}");
            sb.AppendLine($"Facing: {Facing}");
            return sb.ToString();
        }
    }
}
