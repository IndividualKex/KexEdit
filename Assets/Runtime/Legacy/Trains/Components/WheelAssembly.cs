using Unity.Entities;

namespace KexEdit.Legacy {
    public struct WheelAssembly : IComponentData {
        public Entity TrainCar;
        public float Offset;
    }
}
