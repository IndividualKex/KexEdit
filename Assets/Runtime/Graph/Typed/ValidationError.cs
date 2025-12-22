namespace KexEdit.Graph.Typed {
    public enum ValidationError : byte {
        None = 0,
        SourcePortNotFound = 1,
        TargetPortNotFound = 2,
        SourceMustBeOutput = 3,
        TargetMustBeInput = 4,
        IncompatiblePortTypes = 5,
        SelfConnection = 6,
    }
}
