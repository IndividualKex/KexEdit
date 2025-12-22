using Unity.Properties;
using UnityEngine.UIElements;

using KexEdit.Legacy;
namespace KexEdit.UI.NodeGraph {
    public class PriorityField : VisualElement {
        private IntegerField _field;

        private NodeData _data;
        private DataBinding _priorityBinding;

        public PriorityField() {
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

            var label = new Label("Priority") {
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

            _field = new IntegerField {
                isDelayed = true,
                style = {
                    flexGrow = 0f,
                    width = 30f,
                    marginRight = 3f,
                }
            };
            Add(_field);

            _priorityBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(NodeData.Priority)),
                bindingMode = BindingMode.ToTarget
            };

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void Bind(NodeData data) {
            _data = data;

            _field.SetBinding("value", _priorityBinding);
        }

        public void Unbind() {
            _data = null;
            dataSource = null;

            ClearBindings();
        }

        private void OnAttachToPanel(AttachToPanelEvent evt) {
            _field.RegisterCallback<MouseOverEvent>(evt => evt.StopPropagation());
            _field.RegisterCallback<ChangeEvent<int>>(OnPriorityChanged);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt) {
            _field.UnregisterCallback<MouseOverEvent>(evt => evt.StopPropagation());
            _field.UnregisterCallback<ChangeEvent<int>>(OnPriorityChanged);
        }

        private void OnPriorityChanged(ChangeEvent<int> evt) {
            if (_data.Priority == evt.newValue) return;
            Undo.Record();
            var e = this.GetPooled<PriorityChangeEvent>();
            e.Node = _data.Entity;
            e.Priority = evt.newValue;
            this.Send(e);
        }
    }
}
