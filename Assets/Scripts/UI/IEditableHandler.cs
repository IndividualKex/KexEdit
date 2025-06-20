using Unity.Mathematics;
using UnityEngine;

namespace KexEdit.UI {
    public interface IEditableHandler {
        bool IsInBounds(Vector2 mousePosition);
        bool CanCopy();
        bool CanPaste();
        bool CanDelete();
        bool CanCut();
        bool CanSelectAll();
        bool CanDeselectAll();
        bool CanFocus();
        void Copy();
        void Paste(float2? worldPosition = null);
        void Delete();
        void Cut();
        void SelectAll();
        void DeselectAll();
        void Focus();
    }
}
