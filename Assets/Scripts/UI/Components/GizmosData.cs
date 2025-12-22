using UnityEngine;

namespace KexEdit.UI {
    [System.Serializable]
    public class DuplicationGizmoData {
        public float StartHeart;
        public float EndHeart;
        public Color Color = Color.white;
    }

    [System.Serializable]
    public class ExtrusionGizmoData {
        public float Heart;
        public Color Color = Color.white;
    }
}
