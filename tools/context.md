# Tools - Development Utilities

## Purpose

Python scripts for validating binary formats, data assumptions, and debugging outside the Unity editor.

## Layout

```
tools/
├── context.md  # This file
├── analyze_kex.py  # .kex binary format analyzer
└── validate_coaster_state.py  # Coaster aggregate validation
```

## Scripts

### analyze_kex.py

Analyzes .kex files to understand graph structure.

**Usage**:
```bash
python tools/analyze_kex.py path/to/file.kex
```

**Output**: Node/edge topology, port connections, duplicate node IDs, Bridge node analysis

### validate_coaster_state.py

Validates Coaster aggregate state against .kex file expectations.

**Usage**:
```bash
# Export expected state from .kex
python tools/validate_coaster_state.py path/to/file.kex

# Compare expected vs actual Coaster state
python tools/validate_coaster_state.py path/to/file.kex path/to/actual.json
```

**Features**:
- Parses .kex binary format
- Extracts expected Coaster aggregate state (Graph, Scalars, Vectors, Rotations, Durations, Keyframes)
- Compares against actual Coaster state exported from Unity tests
- Handles duplicate node ID remapping, synthetic port creation, legacy format quirks

**Workflow**: Use with `Assets/Tests/CoasterValidationTests.cs` → `ExportCoasterState_ForPythonValidation` test to generate actual JSON

## Dependencies

- Python 3.12+
- Standard library only (struct, json, pathlib)
