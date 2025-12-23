namespace KexEdit.Sim.Schema {
    public enum NodeType : byte {
        Scalar = 0,
        Vector = 1,
        Force = 2,
        Geometric = 3,
        Curved = 4,
        CopyPath = 5,
        Bridge = 6,
        Anchor = 7,
        Reverse = 8,
        ReversePath = 9,
    }
}
