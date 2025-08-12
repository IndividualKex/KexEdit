using Unity.Mathematics;
using UnityEngine;

namespace KexEdit.UI {
    // Default no-op implementation. A native macOS plugin can replace this at runtime.
    public class TrackpadGestureProvider : ITrackpadGestureProvider {
        public static ITrackpadGestureProvider Instance { get; private set; } = new TrackpadGestureProvider();

        public static void SetProvider(ITrackpadGestureProvider provider) {
            Instance = provider ?? Instance;
        }

        public bool TryGetMagnifyDelta(out float delta) {
            delta = 0f;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // Placeholder for future native binding.
#endif
            return false;
        }

        public bool TryGetTwoFingerPanDelta(out float2 delta) {
            delta = float2.zero;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // Placeholder for future native binding.
#endif
            return false;
        }
    }
}


