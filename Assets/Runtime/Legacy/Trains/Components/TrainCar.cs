using Unity.Entities;

namespace KexEdit.Legacy {
    public struct TrainCar : IComponentData {
        public Entity Train;
        public int TrainStyleVersion;
    }
}
