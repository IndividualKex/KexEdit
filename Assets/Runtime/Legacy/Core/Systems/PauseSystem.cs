using Unity.Collections;
using Unity.Entities;

namespace KexEdit.Legacy {
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class PauseSystem : SystemBase {
        public static PauseSystem Instance { get; private set; }

        private int _lock;

        public bool IsPaused => _lock > 0;

        public PauseSystem() {
            Instance = this;
        }

        protected override void OnUpdate() {
            if (!SystemAPI.HasSingleton<PauseSingleton>()) {
                using var ecb = new EntityCommandBuffer(Allocator.Temp);
                Entity entity = ecb.CreateEntity();
                ecb.AddComponent<PauseSingleton>(entity);
                ecb.SetName(entity, "PauseSingleton");
                ecb.Playback(EntityManager);
            }

            SystemAPI.GetSingletonRW<PauseSingleton>().ValueRW.IsPaused = _lock > 0;
        }

        public void Lock() {
            _lock++;
        }

        public void Unlock() {
            UnityEngine.Debug.Assert(_lock > 0, "Lock must be greater than 0");
            _lock--;
        }
    }

    public static class KexTime {
        public static bool IsPaused => PauseSystem.Instance?.IsPaused ?? true;
        public static void Pause() => PauseSystem.Instance?.Lock();
        public static void Unpause() => PauseSystem.Instance?.Unlock();
    }
}
