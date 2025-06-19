using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace KexEdit.UI.Timeline {
    public static class TimelineEvents {
        public static void Send<T>(this VisualElement element) where T : TimelineEvent<T>, new() {
            using var e = EventBase<T>.GetPooled() as T;
            e.target = element;
            element.panel.visualTree.SendEvent(e);
        }

        public static T GetPooled<T>(this VisualElement element) where T : TimelineEvent<T>, new() {
            var e = EventBase<T>.GetPooled() as T;
            e.target = element;
            return e;
        }

        public static void Send<T>(this VisualElement element, T e) where T : TimelineEvent<T>, new() {
            using (e) {
                element.panel.visualTree.SendEvent(e);
            }
        }
    }

    public class TimelineEvent<T> : EventBase<T> where T : TimelineEvent<T>, new() {
        public TimelineEvent() {
            LocalInit();
        }

        protected override void Init() {
            base.Init();
            LocalInit();
        }

        protected virtual void LocalInit() {
            bubbles = true;
            tricklesDown = true;
        }
    }

    public class CurveButtonClickEvent : TimelineEvent<CurveButtonClickEvent> {
        public Vector2 MousePosition;
        public bool IsRightClick;
    }

    public class OutlineMouseDownEvent : TimelineEvent<OutlineMouseDownEvent> { }

    public class AddPropertyClickEvent : TimelineEvent<AddPropertyClickEvent> {
        public Vector2 MousePosition;
    }

    public class TimeChangeEvent : TimelineEvent<TimeChangeEvent> {
        public float Time;
        public bool Snap;
    }

    public class DurationChangeEvent : TimelineEvent<DurationChangeEvent> {
        public float Duration;
        public bool Snap;
    }

    public class PropertyClickEvent : TimelineEvent<PropertyClickEvent> {
        public PropertyType Type;
        public bool ShiftKey;
    }

    public class RemovePropertyClickEvent : TimelineEvent<RemovePropertyClickEvent> {
        public PropertyType Type;
    }

    public class PropertyRightClickEvent : TimelineEvent<PropertyRightClickEvent> {
        public PropertyType Type;
        public Vector2 MousePosition;
    }

    public class KeyframeClickEvent : TimelineEvent<KeyframeClickEvent> {
        public KeyframeData Keyframe;
        public bool ShiftKey;
    }

    public class KeyframeDoubleClickEvent : TimelineEvent<KeyframeDoubleClickEvent> {
        public KeyframeData Keyframe;
        public Vector2 MousePosition;
    }

    public class ViewClickEvent : TimelineEvent<ViewClickEvent> {
        public Vector2 MousePosition;
        public bool ShiftKey;
    }

    public class ViewRightClickEvent : TimelineEvent<ViewRightClickEvent> {
        public Vector2 MousePosition;
    }

    public class SetKeyframeEvent : TimelineEvent<SetKeyframeEvent> {
        public PropertyType Type;
        public float Value;
    }

    public class KeyframeButtonClickEvent : TimelineEvent<KeyframeButtonClickEvent> {
        public PropertyType Type;
    }

    public class AddKeyframeEvent : TimelineEvent<AddKeyframeEvent> { }

    public class JumpToKeyframeEvent : TimelineEvent<JumpToKeyframeEvent> {
        public PropertyType Type;
        public NavigationDirection Direction;
    }

    public class DragKeyframesEvent : TimelineEvent<DragKeyframesEvent> {
        public Dictionary<uint, float> StartTimes;
        public Dictionary<uint, float> StartValues;
        public float TimeDelta;
        public float ValueDelta;
        public ValueBounds Bounds;
        public float ContentHeight;
        public bool ShiftKey;
    }

    public class DragBezierHandleEvent : TimelineEvent<DragBezierHandleEvent> {
        public KeyframeData Keyframe;
        public bool IsOutHandle;
        public float StartTime;
        public float StartValue;
        public float TimeDelta;
        public float ValueDelta;
        public ValueBounds Bounds;
        public float ContentHeight;
    }

    public class SelectKeyframesEvent : TimelineEvent<SelectKeyframesEvent> {
        public List<KeyframeData> Keyframes;
    }
}
