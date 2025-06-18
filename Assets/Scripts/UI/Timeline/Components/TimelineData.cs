using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace KexEdit.UI.Timeline {
    public class TimelineData : IDisposable {
        public NativeList<float> Times = new(Allocator.Persistent);
        public List<PropertyType> OrderedProperties = new();
        public Dictionary<PropertyType, PropertyData> Properties = new();
        public ValueBounds ValueBounds = ValueBounds.Default;
        public string DisplayName;
        public float Time = 0f;
        public float Duration = 0f;
        public float Zoom = 1f;
        public float Offset = 0f;
        public float ViewWidth = 0f;
        public DurationType DurationType = DurationType.Time;
        public TimelineViewMode ViewMode = TimelineViewMode.DopeSheet;
        public PropertyType? LatestSelectedProperty = null;
        public Entity Entity;
        public int SelectedKeyframeCount = 0;
        public bool AddPropertyButtonVisible = false;
        public bool Active = false;
        public bool HasEditableDuration = false;
        public bool DrawAnyReadOnly = false;

        public void Dispose() {
            Entity = Entity.Null;
            Active = false;
            Times.Dispose();
            foreach (var propertyData in Properties.Values) {
                propertyData.Dispose();
            }
        }
    }
}
