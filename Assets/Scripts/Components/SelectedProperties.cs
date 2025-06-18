using System;
using Unity.Entities;

namespace KexEdit {
    [Serializable]
    public struct SelectedProperties : IComponentData {
        public int Value;

        public bool IsEmpty => Value == 0;

        public bool IsSelected(PropertyType propertyType) {
            int bit = 1 << (int)propertyType;
            return (Value & bit) != 0;
        }

        public void Select(PropertyType propertyType) {
            int bit = 1 << (int)propertyType;
            Value |= bit;
        }

        public void Deselect(PropertyType propertyType) {
            int bit = 1 << (int)propertyType;
            Value &= ~bit;
        }

        public void Clear() {
            Value = 0;
        }
    }
}
