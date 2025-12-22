namespace KexEdit.Spline.Rendering {
    public struct StyleBreakpoint {
        public int SectionIndex;
        public float StartArc;
        public float EndArc;
        public int StyleIndex;

        public StyleBreakpoint(int sectionIndex, float startArc, float endArc, int styleIndex) {
            SectionIndex = sectionIndex;
            StartArc = startArc;
            EndArc = endArc;
            StyleIndex = styleIndex;
        }

        public float Length => EndArc - StartArc;
    }
}
