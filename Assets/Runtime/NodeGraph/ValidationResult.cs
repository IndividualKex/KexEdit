using System.Runtime.InteropServices;

namespace KexEdit.NodeGraph {
    public enum ValidationError : byte {
        None = 0,
        SourcePortNotFound = 1,
        TargetPortNotFound = 2,
        SourceMustBeOutput = 3,
        TargetMustBeInput = 4,
        IncompatiblePortTypes = 5,
        SelfConnection = 6,
    }

    public readonly struct ValidationResult {
        [MarshalAs(UnmanagedType.U1)]
        public readonly bool IsValid;
        public readonly ValidationError Error;

        ValidationResult(bool isValid, ValidationError error) {
            IsValid = isValid;
            Error = error;
        }

        public static ValidationResult Success() => new(true, ValidationError.None);
        public static ValidationResult Failure(ValidationError error) => new(false, error);
    }
}
