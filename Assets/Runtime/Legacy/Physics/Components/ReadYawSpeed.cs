using Unity.Entities;

namespace KexEdit.Legacy {
    public struct ReadYawSpeed : IBufferElementData {
        public float Value;

        public static implicit operator float(ReadYawSpeed speed) => speed.Value;
        public static implicit operator ReadYawSpeed(float value) => new() { Value = value };
    }
}
