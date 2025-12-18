using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [UpdateInGroup(typeof(UIPresentationSystemGroup))]
    public partial class EditOperationsSystem : SystemBase {
        public static EditOperationsSystem Instance { get; private set; }

        private List<IEditableHandler> _handlers = new();
        private IEditableHandler _activeHandler;
        private IEditableHandler _lastActiveHandler;
        private bool _menuInteractionActive;

        public EditOperationsSystem() {
            Instance = this;
        }


        protected override void OnUpdate() {
            ProcessKeyboardShortcuts();
        }

        public void SetActiveHandler(IEditableHandler handler) {
            if (_menuInteractionActive) return;
            _activeHandler = handler;
            if (handler != null) {
                _lastActiveHandler = handler;
            }
        }

        private void ProcessKeyboardShortcuts() {
            var kb = Keyboard.current;

            // Do not process global edit shortcuts when typing into text inputs
            if (Extensions.IsTextInputActive()) return;

            if (kb.deleteKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) HandleDelete();
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

        public void RegisterHandler(IEditableHandler handler) => _handlers.Add(handler);

        public void UnregisterHandler(IEditableHandler handler) => _handlers.Remove(handler);

        public void HandleCopy() {
            if (CanCopy) {
                BeginMenuInteraction();
                GetEffectiveHandler().Copy();
                EndMenuInteraction();
            }
        }

        public void HandlePaste() => HandlePaste(null);

        public void HandlePaste(Vector2? worldPosition) {
            if (CanPaste) {
                BeginMenuInteraction();
                Undo.Record();
                GetEffectiveHandler().Paste(worldPosition);
                EndMenuInteraction();
            }
        }

        public void HandleDelete() {
            if (CanDelete) {
                BeginMenuInteraction();
                Undo.Record();
                GetEffectiveHandler().Delete();
                EndMenuInteraction();
            }
        }

        public void HandleCut() {
            if (CanCut) {
                BeginMenuInteraction();
                Undo.Record();
                GetEffectiveHandler().Cut();
                EndMenuInteraction();
            }
        }

        public void HandleSelectAll() {
            if (CanSelectAll) {
                BeginMenuInteraction();
                GetEffectiveHandler().SelectAll();
                EndMenuInteraction();
            }
        }

        public void HandleDeselectAll() {
            if (CanDeselectAll) {
                BeginMenuInteraction();
                GetEffectiveHandler().DeselectAll();
                EndMenuInteraction();
            }
        }

        public void HandleFocus() {
            if (CanFocus) {
                BeginMenuInteraction();
                foreach (var handler in _handlers) {
                    if (!handler.CanFocus()) continue;
                    handler.Focus();
                }
                EndMenuInteraction();
            }
        }

        private void BeginMenuInteraction() => _menuInteractionActive = true;
        private void EndMenuInteraction() => _menuInteractionActive = false;

        private IEditableHandler GetEffectiveHandler() => _activeHandler ?? _lastActiveHandler;

        public bool CanCopy => GetEffectiveHandler()?.CanCopy() == true;
        public bool CanPaste => GetEffectiveHandler()?.CanPaste() == true;
        public bool CanDelete => GetEffectiveHandler()?.CanDelete() == true;
        public bool CanCut => GetEffectiveHandler()?.CanCut() == true;
        public bool CanSelectAll => GetEffectiveHandler()?.CanSelectAll() == true;
        public bool CanDeselectAll => GetEffectiveHandler()?.CanDeselectAll() == true;
        public bool CanFocus => GetEffectiveHandler()?.CanFocus() == true;
    }
}
