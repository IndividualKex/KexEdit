using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace KexEdit.UI {
    // Optional provider that calls into a native macOS plugin (Objective-C) built as a Unity bundle.
    // The plugin is expected to export two C functions:
    //   bool MacGestures_TryGetMagnifyDelta(float* outDelta);
    //   bool MacGestures_TryGetPanDelta(float* outDeltaX, float* outDeltaY);
    // When unavailable, this provider silently fails and disables itself.
    public sealed class MacTrackpadGestureProvider : ITrackpadGestureProvider {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [DllImport("MacGestures")]
        private static extern bool MacGestures_TryGetMagnifyDelta(out float delta);

        [DllImport("MacGestures")]
        private static extern bool MacGestures_TryGetPanDelta(out float deltaX, out float deltaY);
#endif

        private bool _available = true;

        public bool TryGetMagnifyDelta(out float delta) {
            delta = 0f;
            if (!_available) return false;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try {
                return MacGestures_TryGetMagnifyDelta(out delta);
            }
            catch (DllNotFoundException) { _available = false; }
            catch (EntryPointNotFoundException) { _available = false; }
#endif
            return false;
        }

        public bool TryGetTwoFingerPanDelta(out float2 delta) {
            delta = float2.zero;
            if (!_available) return false;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            try {
                if (MacGestures_TryGetPanDelta(out float x, out float y)) {
                    delta = new float2(x, y);
                    return true;
                }
            }
            catch (DllNotFoundException) { _available = false; }
            catch (EntryPointNotFoundException) { _available = false; }
#endif
            return false;
        }
    }
}


