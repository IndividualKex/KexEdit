using Unity.Burst;
using Unity.Mathematics;

namespace KexEdit.Sim {
    [BurstCompile]
    public readonly struct Frame {
        public readonly float3 Direction;
        public readonly float3 Normal;
        public readonly float3 Lateral;

        public Frame(in float3 direction, in float3 normal, in float3 lateral) {
            Direction = direction;
            Normal = normal;
            Lateral = lateral;
        }

        public float Roll => math.atan2(Lateral.y, -Normal.y);

        public float Pitch {
            get {
                float mag = math.sqrt(Direction.x * Direction.x + Direction.z * Direction.z);
                return math.atan2(Direction.y, mag);
            }
        }

        public float Yaw => math.atan2(-Direction.x, -Direction.z);

        public Frame Reorthonormalize() {
            float3 dir = math.normalize(Direction);
            float3 lat = math.normalize(Lateral - dir * math.dot(dir, Lateral));
            float3 norm = math.normalize(math.cross(dir, lat));
            return new Frame(dir, norm, lat);
        }

        public static Frame FromDirectionAndRoll(in float3 direction, float roll) {
            float3 dir = math.normalize(direction);
            float yaw = math.atan2(-dir.x, -dir.z);

            float3 lateral = math.mul(quaternion.Euler(0f, yaw, 0f), math.right());
            quaternion rollQuat = quaternion.AxisAngle(dir, -roll);
            lateral = math.normalize(math.mul(rollQuat, lateral));
            float3 normal = math.normalize(math.cross(dir, lateral));

            return new Frame(dir, normal, lateral);
        }

        public static Frame FromEuler(float pitch, float yaw, float roll) {
            float3 direction = math.normalize(math.mul(
                quaternion.Euler(pitch, yaw, 0f),
                math.back()
            ));
            return FromDirectionAndRoll(direction, roll);
        }

        public Frame WithRoll(float deltaRoll) {
            quaternion rollQuat = quaternion.AxisAngle(Direction, -deltaRoll);
            float3 newLateral = math.normalize(math.mul(rollQuat, Lateral));
            float3 newNormal = math.normalize(math.cross(Direction, newLateral));
            return new Frame(Direction, newNormal, newLateral).Reorthonormalize();
        }

        public Frame RotateAround(in float3 axis, float angle) {
            quaternion q = quaternion.AxisAngle(axis, angle);
            return new Frame(
                math.normalize(math.mul(q, Direction)),
                math.normalize(math.mul(q, Normal)),
                math.normalize(math.mul(q, Lateral))
            );
        }

        public Frame WithPitch(float deltaPitch) {
            float3 up = Normal.y >= 0f ? math.up() : -math.up();
            float3 pitchAxis = math.normalize(math.cross(up, Direction));
            quaternion pitchQuat = quaternion.AxisAngle(pitchAxis, deltaPitch);
            float3 newDirection = math.normalize(math.mul(pitchQuat, Direction));
            float3 newLateral = math.normalize(math.mul(pitchQuat, Lateral));
            float3 newNormal = math.normalize(math.cross(newDirection, newLateral));
            return new Frame(newDirection, newNormal, newLateral).Reorthonormalize();
        }

        public Frame WithYaw(float deltaYaw) {
            quaternion yawQuat = quaternion.AxisAngle(math.up(), deltaYaw);
            float3 newDirection = math.normalize(math.mul(yawQuat, Direction));
            float3 newLateral = math.normalize(math.mul(yawQuat, Lateral));
            float3 newNormal = math.normalize(math.cross(newDirection, newLateral));
            return new Frame(newDirection, newNormal, newLateral).Reorthonormalize();
        }

        public float3 SpinePosition(in float3 heartPosition, float offset) => heartPosition + Normal * offset;

        public static Frame Default => new(math.back(), math.down(), math.right());

        public bool MatchesForContinuation(in Frame other, float threshold, bool isNext) {
            float dirDot = math.dot(Direction, other.Direction);
            float normDot = math.dot(Normal, other.Normal);
            float latDot = math.dot(Lateral, other.Lateral);

            if (isNext) {
                return dirDot > threshold && normDot > threshold && latDot > threshold;
            }
            else {
                return dirDot < -threshold && normDot > threshold && latDot < -threshold;
            }
        }
    }
}
