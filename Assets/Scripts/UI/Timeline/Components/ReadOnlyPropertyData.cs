using System;
using Unity.Collections;
using KexEdit.Legacy;

namespace KexEdit.UI.Timeline {
    [Serializable]
    public class ReadOnlyPropertyData : IDisposable {
        public PropertyType Type;
        public TimelineViewMode ViewMode = TimelineViewMode.Curve;
        public bool Visible = false;
        public NativeList<float> Values;
        public NativeList<float> Times;
        public string Units = "";

        public void Dispose() {
            if (Values.IsCreated) Values.Dispose();
            if (Times.IsCreated) Times.Dispose();
        }
    }
}
