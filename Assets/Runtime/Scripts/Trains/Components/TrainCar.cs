using Unity.Entities;

namespace KexEdit {
    public struct TrainCar : IComponentData {
        public Entity Train;
        public int TrainStyleVersion;
    }
}
