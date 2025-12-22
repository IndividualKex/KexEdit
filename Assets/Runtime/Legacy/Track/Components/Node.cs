using System;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;

namespace KexEdit.Legacy {
    [Serializable]
    public struct Node : IComponentData {
        public uint Id;
        public float2 Position;
        public NodeType Type;
        public int Priority;
        [MarshalAs(UnmanagedType.U1)]
        public bool Selected;

        public Entity Next;
        public Entity Previous;

        public static Node Create(float2 position, NodeType type) => new() {
            Id = Uuid.Create(),
            Position = position,
            Type = type,
            Priority = 0,
            Selected = false,
            Next = Entity.Null,
            Previous = Entity.Null,
        };
    }
}
