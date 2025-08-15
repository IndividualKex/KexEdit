using Unity.Entities;

namespace KexEdit {
    public struct ReadPitchSpeed : IBufferElementData {
        public float Value;

        public static implicit operator float(ReadPitchSpeed speed) => speed.Value;
        public static implicit operator ReadPitchSpeed(float value) => new() { Value = value };
    }
}
