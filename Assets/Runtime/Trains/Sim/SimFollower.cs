namespace KexEdit.Trains.Sim {
    public struct SimFollower {
        public int TraversalIndex;
        public float PointIndex;
        public int Facing;

        public static SimFollower Default => new() {
            TraversalIndex = 0,
            PointIndex = 0f,
            Facing = 1
        };
    }
}
