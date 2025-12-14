using Unity.Entities;

namespace KexEdit {
    public struct WheelAssembly : IComponentData {
        public Entity TrainCar;
        public float Offset;
    }
}
