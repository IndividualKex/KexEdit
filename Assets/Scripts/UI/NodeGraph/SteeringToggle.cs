using Unity.Properties;
using UnityEngine.UIElements;

namespace KexEdit.UI.NodeGraph {
    public class SteeringToggle : VisualElement {
        private Toggle _toggle;

        private NodeData _data;
        private DataBinding _valueBinding;

        public SteeringToggle() {
            style.position = Position.Relative;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;
            style.height = 24f;
            style.paddingLeft = 8f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;

            var label = new Label("Steering") {
                style = {
                    flexGrow = 1f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                }
            };
            Add(label);

            _toggle = new Toggle {
                style = {
                    position = Position.Relative,
                    width = 16f,
                    height = 16f,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 4f,
                    marginRight = 4f,
                    marginTop = 4f,
                    marginBottom = 4f,
                },
            };
            Add(_toggle);

            _valueBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(NodeData.Steering)),
                bindingMode = BindingMode.ToTarget
            };

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void Bind(NodeData data) {
            _data = data;

            _toggle.SetBinding("value", _valueBinding);
        }

        public void Unbind() {
            _data = null;

            ClearBindings();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt) {
            _toggle.RegisterCallback<MouseOverEvent>(evt => evt.StopPropagation());
            _toggle.RegisterCallback<ChangeEvent<bool>>(OnSteeringToggleChanged);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt) {
            _toggle.UnregisterCallback<MouseOverEvent>(evt => evt.StopPropagation());
            _toggle.UnregisterCallback<ChangeEvent<bool>>(OnSteeringToggleChanged);
        }

        public void UpdateDataSource(NodeData newData) {
            _data = newData;
        }

        private void OnSteeringToggleChanged(ChangeEvent<bool> evt) {
            if (_data.Steering == evt.newValue) return;
            Undo.Record();
            var e = this.GetPooled<SteeringToggleChangeEvent>();
            e.Node = _data.Entity;
            e.Steering = evt.newValue;
            this.Send(e);
        }
    }
}