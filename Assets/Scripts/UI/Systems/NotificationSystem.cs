using Unity.Entities;
using UnityEngine.UIElements;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [UpdateInGroup(typeof(UISimulationSystemGroup))]
    public partial class NotificationSystem : SystemBase {
        public static NotificationSystem Instance { get; private set; }

        private const float DefaultDisplayTime = 2f;

        private NotificationData _data;
        private NotificationOverlay _overlay;

        public NotificationSystem() {
            Instance = this;
        }

        protected override void OnStartRunning() {
            _data = new NotificationData {
                DisplayText = "",
                Timer = 0f,
                IsVisible = false
            };

            var root = UIService.Instance.UIDocument.rootVisualElement;
            var gameView = root.Q<GameView>();

            _overlay = new NotificationOverlay(_data);
            gameView.Add(_overlay);
        }

        protected override void OnUpdate() {
            if (_data.Timer <= 0f) return;

            _data.Timer -= UnityEngine.Time.unscaledDeltaTime;

            if (_data.Timer <= 0f) {
                _data.IsVisible = false;
            }
        }

        public static void ShowNotification(string text) {
            Instance._data.DisplayText = text;
            Instance._data.Timer = DefaultDisplayTime;
            Instance._data.IsVisible = true;
        }
    }
}
