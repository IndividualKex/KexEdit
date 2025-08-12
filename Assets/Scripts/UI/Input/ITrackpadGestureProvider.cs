using Unity.Mathematics;

namespace KexEdit.UI {
    public interface ITrackpadGestureProvider {
        // Returns true if a magnify (pinch) gesture delta is available this frame.
        // Positive values mean zoom in, negative zoom out. Units are arbitrary, caller applies sensitivity.
        bool TryGetMagnifyDelta(out float delta);

        // Returns true if a two-finger swipe/scroll is available in trackpad gesture space (not wheel).
        // If not supported, return false and caller should fall back to Mouse.scroll.
        bool TryGetTwoFingerPanDelta(out float2 delta);
    }
}


