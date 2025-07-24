using Unity.Entities;
using System.Collections.Generic;

namespace KexEdit {
    public class CartStyleSettings : IComponentData {
        public List<CartStyle> Styles = new();
        public int Version;
    }
}
