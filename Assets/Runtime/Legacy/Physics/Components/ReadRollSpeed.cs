using Unity.Entities;

namespace KexEdit.Legacy {
    public struct ReadRollSpeed : IBufferElementData {
        public float Value;

        public static implicit operator float(ReadRollSpeed speed) => speed.Value;
        public static implicit operator ReadRollSpeed(float value) => new() { Value = value };
    }
}
