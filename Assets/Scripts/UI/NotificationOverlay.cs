using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

namespace KexEdit.UI {
    public class NotificationOverlay : Label {
        private NotificationData _data;

        public NotificationOverlay(NotificationData data) {
            _data = data;
            dataSource = _data;
            pickingMode = PickingMode.Ignore;

            style.position = Position.Absolute;
            style.top = 20f;
            style.left = Length.Percent(50f);
            style.translate = new Translate(Length.Percent(-50f), 0f);
            style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            style.fontSize = 12;
            style.paddingTop = 8f;
            style.paddingRight = 12f;
            style.paddingBottom = 8f;
            style.paddingLeft = 12f;
            style.borderTopLeftRadius = 4f;
            style.borderTopRightRadius = 4f;
            style.borderBottomLeftRadius = 4f;
            style.borderBottomRightRadius = 4f;
            style.opacity = 0f;
            style.unityTextAlign = TextAnchor.MiddleCenter;
            style.transitionProperty = new List<StylePropertyName> { "opacity" };
            style.transitionDuration = new List<TimeValue> { new(300, TimeUnit.Millisecond) };
            style.transitionTimingFunction = new List<EasingFunction> { EasingMode.EaseOutCubic };

            var textBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.DisplayText)),
                bindingMode = BindingMode.ToTarget,
            };
            SetBinding("text", textBinding);

            var visibilityBinding = new DataBinding {
                dataSourcePath = new PropertyPath(nameof(_data.IsVisible)),
                bindingMode = BindingMode.ToTarget,
            };
            visibilityBinding.sourceToUiConverters.AddConverter((ref bool value) =>
                (StyleFloat)(value ? 1f : 0f));
            SetBinding("style.opacity", visibilityBinding);
        }
    }
}
