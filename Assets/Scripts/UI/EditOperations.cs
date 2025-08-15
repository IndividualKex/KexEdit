using UnityEngine;

namespace KexEdit.UI {
    public static class EditOperations {
        public static void RegisterHandler(IEditableHandler handler) => EditOperationsSystem.Instance?.RegisterHandler(handler);
        public static void UnregisterHandler(IEditableHandler handler) => EditOperationsSystem.Instance?.UnregisterHandler(handler);
        public static void SetActiveHandler(IEditableHandler handler) => EditOperationsSystem.Instance?.SetActiveHandler(handler);

        public static void HandleCopy() => EditOperationsSystem.Instance?.HandleCopy();
        public static void HandlePaste() => EditOperationsSystem.Instance?.HandlePaste();
        public static void HandlePaste(Vector2? worldPosition) => EditOperationsSystem.Instance?.HandlePaste(worldPosition);
        public static void HandleDelete() => EditOperationsSystem.Instance?.HandleDelete();
        public static void HandleCut() => EditOperationsSystem.Instance?.HandleCut();
        public static void HandleSelectAll() => EditOperationsSystem.Instance?.HandleSelectAll();
        public static void HandleDeselectAll() => EditOperationsSystem.Instance?.HandleDeselectAll();
        public static void HandleFocus() => EditOperationsSystem.Instance?.HandleFocus();

        public static bool CanCopy => EditOperationsSystem.Instance?.CanCopy == true;
        public static bool CanPaste => EditOperationsSystem.Instance?.CanPaste == true;
        public static bool CanDelete => EditOperationsSystem.Instance?.CanDelete == true;
        public static bool CanCut => EditOperationsSystem.Instance?.CanCut == true;
        public static bool CanSelectAll => EditOperationsSystem.Instance?.CanSelectAll == true;
        public static bool CanDeselectAll => EditOperationsSystem.Instance?.CanDeselectAll == true;
        public static bool CanFocus => EditOperationsSystem.Instance?.CanFocus == true;
    }
}
