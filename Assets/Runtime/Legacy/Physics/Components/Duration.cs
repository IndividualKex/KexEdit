using System;
using Unity.Entities;

namespace KexEdit.Legacy {
    [Serializable]
    public struct Duration : IComponentData {
        public DurationType Type;
        public float Value;

        public static implicit operator float(Duration duration) => duration.Value;
        public static implicit operator Duration(float value) => new() { Value = value };
    }
}
