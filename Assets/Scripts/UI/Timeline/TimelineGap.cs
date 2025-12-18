using Unity.Properties;
using UnityEngine.UIElements;
using static KexEdit.UI.Constants;

using KexEdit.Legacy;
namespace KexEdit.UI.Timeline {
    public class TimelineGap : VisualElement {
        private TimelineData _data;

        public TimelineGap() {
            style.height = 20f;
            style.backgroundColor = s_AltDarkBackgroundColor;

            var displayBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(TimelineData.Active)),
                bindingMode = BindingMode.ToTarget
            };
            displayBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                new StyleEnum<DisplayStyle>(value ? DisplayStyle.Flex : DisplayStyle.None));
            SetBinding("style.display", displayBinding);
        }

        public void Initialize(TimelineData data) {
            _data = data;

            generateVisualContent += OnDrawContent;
        }

        private void OnDrawContent(MeshGenerationContext ctx) {
            TimelineDrawUtils.DrawPlayhead(ctx.painter2D, _data, contentRect);
        }
    }
}
