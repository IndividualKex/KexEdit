using Unity.Entities;

namespace KexEdit.Legacy {
    public struct ReadLateralForce : IBufferElementData {
        public float Value;

        public static implicit operator float(ReadLateralForce force) => force.Value;
        public static implicit operator ReadLateralForce(float value) => new() { Value = value };
    }
}
