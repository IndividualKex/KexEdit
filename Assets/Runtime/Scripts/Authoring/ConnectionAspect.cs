using Unity.Entities;

namespace KexEdit {
    public readonly partial struct ConnectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Connection> ConnectionRO;
        private readonly RefRW<Dirty> DirtyRW;

        public uint Id => ConnectionRO.ValueRO.Id;
        public Entity Source => ConnectionRO.ValueRO.Source;
        public Entity Target => ConnectionRO.ValueRO.Target;
        public bool Selected => ConnectionRO.ValueRO.Selected;

        public bool Dirty {
            get => DirtyRW.ValueRO.Value;
            set => DirtyRW.ValueRW.Value = value;
        }
    }
}
