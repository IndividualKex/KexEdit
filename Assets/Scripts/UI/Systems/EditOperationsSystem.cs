using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace KexEdit.UI {
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EditOperationsSystem : SystemBase {
        private static List<IEditableHandler> _handlers = new();
        private static IEditableHandler _activeHandler;
        private static bool _menuInteractionActive;

        private VisualElement _root;

        protected override void OnStartRunning() {
            _root = UIService.Instance.UIDocument.rootVisualElement;
        }

        protected override void OnUpdate() {
            UpdateActiveHandler();
            ProcessKeyboardShortcuts();
        }

        private void UpdateActiveHandler() {
            if (_menuInteractionActive) return;
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            float uiScale = UIScaleSystem.Instance.CurrentScale;
            mousePosition /= uiScale;

            mousePosition.y = _root.worldBound.height - mousePosition.y;
            foreach (var handler in _handlers) {
                if (!handler.IsInBounds(mousePosition)) continue;
                _activeHandler = handler;
                return;
            }
            _activeHandler = null;
        }

        private void ProcessKeyboardShortcuts() {
            var kb = Keyboard.current;

            if (kb.deleteKey.wasPressedThisFrame) HandleDelete();
            else if (kb.fKey.wasPressedThisFrame) HandleFocus();

            if (kb.ctrlKey.isPressed || kb.leftCommandKey.isPressed) {
                if (kb.xKey.wasPressedThisFrame) HandleCut();
                else if (kb.cKey.wasPressedThisFrame) HandleCopy();
                else if (kb.vKey.wasPressedThisFrame) HandlePaste();
                else if (kb.aKey.wasPressedThisFrame) HandleSelectAll();
            }
            else if (kb.altKey.isPressed || kb.leftCommandKey.isPressed || kb.rightCommandKey.isPressed) {
                if (kb.aKey.wasPressedThisFrame) HandleDeselectAll();
            }
        }

        public static void RegisterHandler(IEditableHandler handler) => _handlers.Add(handler);

        public static void UnregisterHandler(IEditableHandler handler) => _handlers.Remove(handler);

        public static void HandleCopy() {
            if (CanCopy) {
                BeginMenuInteraction();
                _activeHandler.Copy();
                EndMenuInteraction();
            }
        }

        public static void HandlePaste() => HandlePaste(null);

        public static void HandlePaste(Vector2? worldPosition) {
            if (CanPaste) {
                BeginMenuInteraction();
                Undo.Record();
                _activeHandler.Paste(worldPosition);
                EndMenuInteraction();
            }
        }

        public static void HandleDelete() {
            if (CanDelete) {
                BeginMenuInteraction();
                Undo.Record();
                _activeHandler.Delete();
                EndMenuInteraction();
            }
        }

        public static void HandleCut() {
            if (CanCut) {
                BeginMenuInteraction();
                Undo.Record();
                _activeHandler.Cut();
                EndMenuInteraction();
            }
        }

        public static void HandleSelectAll() {
            if (CanSelectAll) {
                BeginMenuInteraction();
                _activeHandler.SelectAll();
                EndMenuInteraction();
            }
        }

        public static void HandleDeselectAll() {
            if (CanDeselectAll) {
                BeginMenuInteraction();
                _activeHandler.DeselectAll();
                EndMenuInteraction();
            }
        }

        public static void HandleFocus() {
            if (CanFocus) {
                BeginMenuInteraction();
                _activeHandler.Focus();
                EndMenuInteraction();
            }
        }

        private static void BeginMenuInteraction() => _menuInteractionActive = true;
        private static void EndMenuInteraction() => _menuInteractionActive = false;

        public static bool CanCopy => _activeHandler?.CanCopy() == true;
        public static bool CanPaste => _activeHandler?.CanPaste() == true;
        public static bool CanDelete => _activeHandler?.CanDelete() == true;
        public static bool CanCut => _activeHandler?.CanCut() == true;
        public static bool CanSelectAll => _activeHandler?.CanSelectAll() == true;
        public static bool CanDeselectAll => _activeHandler?.CanDeselectAll() == true;
        public static bool CanFocus => _activeHandler?.CanFocus() == true;
    }
}
