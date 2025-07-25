using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public struct Port : IComponentData {
        public uint Id;
        public PortType Type;
        public bool IsInput;

        public static Port Create(PortType type, bool isInput) => new() {
            Id = Uuid.Create(),
            Type = type,
            IsInput = isInput
        };
    }
}
