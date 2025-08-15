using System;
using KexEdit.Serialization;

namespace KexEdit.UI {
    public static class Undo {
        public static bool CanUndo => SerializationSystem.Instance.CanUndo;
        public static bool CanRedo => SerializationSystem.Instance.CanRedo;

        public static event Action Recorded {
            add => SerializationSystem.Recorded += value;
            remove => SerializationSystem.Recorded -= value;
        }

        public static void Record() => ProjectOperationsSystem.Instance.Record();
        public static void Execute() => ProjectOperationsSystem.Instance.Undo();
        public static void Redo() => ProjectOperationsSystem.Instance.Redo();
        public static void Clear() => SerializationSystem.Instance.Clear();
    }
}
