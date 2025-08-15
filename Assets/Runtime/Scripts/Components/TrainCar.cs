using Unity.Entities;

namespace KexEdit {
    public struct TrainCar : IComponentData {
        public Entity Train;
        public Entity Section;
        public float Length;
        public float CouplerOffset;
        public float Position;
        public int CarIndex;
    }
}
