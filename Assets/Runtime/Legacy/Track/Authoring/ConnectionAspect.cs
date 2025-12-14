using Unity.Entities;

namespace KexEdit {
    public readonly partial struct ConnectionAspect : IAspect {
        public readonly Entity Self;

        private readonly RefRO<Connection> connection;
        private readonly RefRO<CoasterReference> coasterReference;

        public uint Id => connection.ValueRO.Id;
        public Entity Source => connection.ValueRO.Source;
        public Entity Target => connection.ValueRO.Target;
        public bool Selected => connection.ValueRO.Selected;

        public Entity Coaster => coasterReference.ValueRO.Value;
    }
}
