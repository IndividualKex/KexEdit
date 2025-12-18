using Unity.Properties;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI.Timeline {
    [UxmlElement]
    public partial class Timeline : VisualElement {
        private TimelineOutliner _outliner;
        private VisualElement _divider;
        private TimelineView _view;
        private VisualElement _editorDivider;
        private KeyframeEditor _keyframeEditor;

        public TimelineView View => _view;

        public Timeline() {
            style.position = Position.Absolute;
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Stretch;
            style.left = 0;
            style.bottom = 0;
            style.right = 0;
            style.top = 0;
            style.paddingLeft = 0f;
            style.paddingRight = 0f;
            style.paddingTop = 0f;
            style.paddingBottom = 0f;
            style.marginLeft = 0f;
            style.marginRight = 0f;
            style.marginTop = 0f;
            style.marginBottom = 0f;
            style.backgroundColor = s_DarkBackgroundColor;

            _outliner = new TimelineOutliner();
            Add(_outliner);

            _divider = new VisualElement() {
                style = {
                    position = Position.Relative,
                    flexGrow = 0,
                    width = 1f,
                    backgroundColor = s_DividerColor,
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
            Add(_divider);

            _view = new TimelineView();
            Add(_view);

            _editorDivider = new VisualElement() {
                style = {
                    position = Position.Relative,
                    flexGrow = 0,
                    width = 1f,
                    backgroundColor = s_DividerColor,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f,
                    marginLeft = 0f,
                    marginRight = 0f,
                    marginTop = 0f,
                    marginBottom = 0f,
                    display = DisplayStyle.None
                }
            };
            Add(_editorDivider);

            _keyframeEditor = new KeyframeEditor() {
                style = {
                    display = DisplayStyle.None
                }
            };
            Add(_keyframeEditor);

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.EnableKeyframeEditor)),
                bindingMode = BindingMode.ToTarget
            };
            displayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                (StyleEnum<DisplayStyle>)(value ? DisplayStyle.Flex : DisplayStyle.None));
            _editorDivider.SetBinding("style.display", displayBinding);
            _keyframeEditor.SetBinding("style.display", displayBinding);
        }

        public void Initialize(TimelineData data) {
            dataSource = data;

            _outliner.Initialize(data);
            _view.Initialize(data);
            _keyframeEditor.Initialize(data);
        }

        public void Draw() {
            _view.Draw();
            _keyframeEditor.Draw();
        }
    }
}
