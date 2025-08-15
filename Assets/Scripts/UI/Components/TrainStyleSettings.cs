using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit.UI {
    public class TrainStyleSettings : IComponentData {
        public List<TrainStyle> Styles = new();
        public int Version;
    }
}
