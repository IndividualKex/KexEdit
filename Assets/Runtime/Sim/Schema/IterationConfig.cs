using Unity.Burst;

namespace KexEdit.Sim.Schema {
    public enum DurationType : byte { Time, Distance }

    [BurstCompile]
    public readonly struct IterationConfig {
        public readonly float Duration;
        public readonly DurationType DurationType;

        public IterationConfig(float duration, DurationType durationType) {
            Duration = duration;
            DurationType = durationType;
        }
    }
}
