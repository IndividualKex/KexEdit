using System.Runtime.InteropServices;
using Unity.Entities;

namespace KexEdit.Legacy {
    public struct Connection : IComponentData {
        public uint Id;
        public Entity Source;
        public Entity Target;
        [MarshalAs(UnmanagedType.U1)]
        public bool Selected;

        public static Connection Create(Entity source, Entity target, bool selected) {
            return new Connection {
                Id = Uuid.Create(),
                Source = source,
                Target = target,
                Selected = selected
            };
        }
    }
}
