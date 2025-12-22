using KexEdit.Trains.Sim;
using Unity.Entities;

namespace KexEdit.Legacy {
    public struct SimFollowerSingleton : IComponentData {
        public SimFollower Follower;
    }
}
