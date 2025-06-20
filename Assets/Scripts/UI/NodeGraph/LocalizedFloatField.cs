using Unity.Properties;
using UnityEngine.UIElements;
using static KexEdit.UI.Units;

namespace KexEdit.UI.NodeGraph {
    public class LocalizedFloatField : FloatField {
        private UnitsData _data;
        private float _value;
        private bool _updating;

        [CreateProperty]
        public float Value {
            get => _value;
            set {
                if (_updating) return;
                _updating = true;
                _value = value;
                base.SetValueWithoutNotify(ConvertInternalToDisplay(value));
                _updating = false;
            }
        }

        public new float value {
            get => throw new System.NotImplementedException();
            set => throw new System.NotImplementedException();
        }

        public LocalizedFloatField(UnitsData data) {
            _data = data;
            this.RegisterValueChangedCallback<float>(OnBaseValueChanged);
        }

        private void OnBaseValueChanged(ChangeEvent<float> evt) {
            if (_updating) return;
            _updating = true;
            _value = ConvertDisplayToInternal(evt.newValue);
            _updating = false;
        }

        private float ConvertDisplayToInternal(float display) {
            return _data.Units switch {
                UnitsType.Meters => DisplayToMeters(display),
                _ => display,
            };
        }

        private float ConvertInternalToDisplay(float internalValue) {
            return _data.Units switch {
                UnitsType.Meters => MetersToDisplay(internalValue),
                _ => internalValue,
            };
        }

        public override void SetValueWithoutNotify(float newValue) {
            if (_updating) return;
            _updating = true;
            _value = newValue;
            base.SetValueWithoutNotify(ConvertInternalToDisplay(newValue));
            _updating = false;
        }
    }
}
