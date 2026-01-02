#!/usr/bin/env python3
# /// script
# requires-python = ">=3.10"
# dependencies = ["numpy"]
# ///
"""
OBJ Export Debug Tool

Debugging tool for investigating normals/winding issues in OBJ mesh export.
Creates simple test geometry, validates coordinate systems, and tests round-trips.
"""

import numpy as np
from pathlib import Path
from dataclasses import dataclass
from typing import List, Tuple


@dataclass
class ObjMesh:
    """Simple OBJ mesh representation."""
    vertices: np.ndarray   # Nx3
    normals: np.ndarray    # Nx3 (per-vertex or per-face-vertex)
    faces: List[List[Tuple[int, int]]]  # List of faces, each face is list of (vertex_idx, normal_idx)

    @staticmethod
    def parse(filepath: str) -> 'ObjMesh':
        """Parse an OBJ file."""
        vertices = []
        normals = []
        faces = []

        with open(filepath, 'r') as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith('#'):
                    continue

                parts = line.split()
                if parts[0] == 'v':
                    vertices.append([float(parts[1]), float(parts[2]), float(parts[3])])
                elif parts[0] == 'vn':
                    normals.append([float(parts[1]), float(parts[2]), float(parts[3])])
                elif parts[0] == 'f':
                    face = []
                    for p in parts[1:]:
                        # Handle v//vn format
                        if '//' in p:
                            v, n = p.split('//')
                            face.append((int(v) - 1, int(n) - 1))  # OBJ is 1-indexed
                        elif '/' in p:
                            # Handle v/vt/vn or v/vt format
                            sp = p.split('/')
                            v = int(sp[0]) - 1
                            n = int(sp[2]) - 1 if len(sp) > 2 and sp[2] else 0
                            face.append((int(v), int(n)))
                        else:
                            face.append((int(p) - 1, 0))
                    faces.append(face)

        return ObjMesh(
            vertices=np.array(vertices),
            normals=np.array(normals) if normals else np.zeros((len(vertices), 3)),
            faces=faces
        )

    def write(self, filepath: str, flip_winding: bool = False):
        """Write to OBJ file."""
        with open(filepath, 'w') as f:
            f.write("# OBJ Debug Export\n\n")

            # Vertices
            for v in self.vertices:
                f.write(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}\n")
            f.write("\n")

            # Normals
            for n in self.normals:
                f.write(f"vn {n[0]:.6f} {n[1]:.6f} {n[2]:.6f}\n")
            f.write("\n")

            # Faces (OBJ is 1-indexed)
            for face in self.faces:
                if flip_winding:
                    face = face[::-1]  # Reverse winding
                indices = [f"{v+1}//{n+1}" for v, n in face]
                f.write(f"f {' '.join(indices)}\n")


def compute_face_normal(v0: np.ndarray, v1: np.ndarray, v2: np.ndarray) -> np.ndarray:
    """
    Compute face normal using cross product.
    Uses CCW winding convention (standard for OBJ): cross(v1-v0, v2-v0)
    """
    edge1 = v1 - v0
    edge2 = v2 - v0
    normal = np.cross(edge1, edge2)
    length = np.linalg.norm(normal)
    if length > 1e-10:
        normal = normal / length
    return normal


def create_quad_xy_plane() -> ObjMesh:
    """
    Create a simple quad in the XY plane with +Z normal.
    CCW winding when viewed from +Z.

    Vertices:
        3---2
        |   |
        0---1
    """
    vertices = np.array([
        [0.0, 0.0, 0.0],  # 0: bottom-left
        [1.0, 0.0, 0.0],  # 1: bottom-right
        [1.0, 1.0, 0.0],  # 2: top-right
        [0.0, 1.0, 0.0],  # 3: top-left
    ])

    # All normals point +Z
    normals = np.array([
        [0.0, 0.0, 1.0],
        [0.0, 0.0, 1.0],
        [0.0, 0.0, 1.0],
        [0.0, 0.0, 1.0],
    ])

    # Two triangles, CCW winding for +Z front face
    # Triangle 1: 0-1-2 (CCW from +Z view)
    # Triangle 2: 0-2-3 (CCW from +Z view)
    faces = [
        [(0, 0), (1, 1), (2, 2)],
        [(0, 0), (2, 2), (3, 3)],
    ]

    return ObjMesh(vertices=vertices, normals=normals, faces=faces)


def create_track_segment_mesh(length: float = 10.0) -> ObjMesh:
    """
    Create a simple track segment mesh like KexEdit uses.

    Mesh convention from KexEdit:
    - Origin at segment START
    - +X lateral (left-right)
    - +Y up
    - +Z forward (0 to +length)

    Creates a simple flat rail mesh.
    """
    half_width = 0.5

    vertices = np.array([
        [-half_width, 0.0, 0.0],       # 0: start-left
        [half_width, 0.0, 0.0],        # 1: start-right
        [-half_width, 0.0, length],    # 2: end-left
        [half_width, 0.0, length],     # 3: end-right
    ])

    # Top surface normals point +Y (up)
    normals = np.array([
        [0.0, 1.0, 0.0],
        [0.0, 1.0, 0.0],
        [0.0, 1.0, 0.0],
        [0.0, 1.0, 0.0],
    ])

    # Two triangles for top surface
    # Unity uses CW winding for front faces (left-handed)
    # For +Y normal with CW winding: 0-2-1 and 1-2-3
    faces = [
        [(0, 0), (2, 2), (1, 1)],  # CW from +Y view
        [(1, 1), (2, 2), (3, 3)],  # CW from +Y view
    ]

    return ObjMesh(vertices=vertices, normals=normals, faces=faces)


def analyze_mesh(mesh: ObjMesh, name: str = "Mesh"):
    """Analyze a mesh and report on its geometry."""
    print(f"\n=== {name} Analysis ===")
    print(f"Vertices: {len(mesh.vertices)}")
    print(f"Normals: {len(mesh.normals)}")
    print(f"Faces: {len(mesh.faces)}")

    for i, face in enumerate(mesh.faces):
        v_indices = [f[0] for f in face]
        n_indices = [f[1] for f in face]

        if len(v_indices) >= 3:
            v0 = mesh.vertices[v_indices[0]]
            v1 = mesh.vertices[v_indices[1]]
            v2 = mesh.vertices[v_indices[2]]

            geom_normal = compute_face_normal(v0, v1, v2)
            vertex_normal = mesh.normals[n_indices[0]]

            dot = np.dot(geom_normal, vertex_normal)
            match = "MATCH" if dot > 0 else "INVERTED"

            print(f"\nFace {i}:")
            print(f"  Vertex indices: {v_indices}")
            print(f"  v0={v0}, v1={v1}, v2={v2}")
            print(f"  Geometric normal (CCW): {geom_normal}")
            print(f"  Vertex normal: {vertex_normal}")
            print(f"  Dot product: {dot:.4f} -> {match}")


def test_identity_roundtrip():
    """Test: Create OBJ, parse it, write it again - should be identical."""
    print("\n" + "="*60)
    print("TEST: Identity Round-trip")
    print("="*60)

    output_dir = Path(__file__).parent / "obj_test_output"
    output_dir.mkdir(exist_ok=True)

    # Create a simple quad
    mesh = create_quad_xy_plane()

    # Write it
    path1 = output_dir / "quad_original.obj"
    mesh.write(str(path1))
    print(f"Wrote: {path1}")

    # Parse it back
    mesh2 = ObjMesh.parse(str(path1))

    # Write again
    path2 = output_dir / "quad_roundtrip.obj"
    mesh2.write(str(path2))
    print(f"Wrote: {path2}")

    # Compare
    analyze_mesh(mesh, "Original")
    analyze_mesh(mesh2, "After Round-trip")

    # Check if vertices match
    if np.allclose(mesh.vertices, mesh2.vertices):
        print("\nVertices: MATCH")
    else:
        print("\nVertices: DIFFER")


def test_winding_flip():
    """Test: See what happens when we flip winding order."""
    print("\n" + "="*60)
    print("TEST: Winding Flip Effect")
    print("="*60)

    output_dir = Path(__file__).parent / "obj_test_output"
    output_dir.mkdir(exist_ok=True)

    mesh = create_quad_xy_plane()

    # Write with normal winding
    path_normal = output_dir / "quad_normal_winding.obj"
    mesh.write(str(path_normal), flip_winding=False)
    print(f"Wrote (normal winding): {path_normal}")

    # Write with flipped winding
    path_flipped = output_dir / "quad_flipped_winding.obj"
    mesh.write(str(path_flipped), flip_winding=True)
    print(f"Wrote (flipped winding): {path_flipped}")

    # Analyze both
    mesh_normal = ObjMesh.parse(str(path_normal))
    mesh_flipped = ObjMesh.parse(str(path_flipped))

    analyze_mesh(mesh_normal, "Normal Winding (CCW)")
    analyze_mesh(mesh_flipped, "Flipped Winding (CW)")


def test_unity_track_export():
    """
    Test: Simulate what TrackMeshExporter does.

    TrackMeshExporter:
    1. Gets mesh vertices/normals from Unity (left-handed, CW winding)
    2. Applies deformation (modifies positions and normals)
    3. Writes OBJ with flipped winding (i2, i1, i0)
    4. Does NOT transform coordinates
    """
    print("\n" + "="*60)
    print("TEST: Unity Track Export Simulation")
    print("="*60)

    output_dir = Path(__file__).parent / "obj_test_output"
    output_dir.mkdir(exist_ok=True)

    # Create mesh as Unity would have it (CW winding for left-handed)
    mesh = create_track_segment_mesh()
    print("Created track segment mesh (Unity convention: CW winding, left-handed)")

    analyze_mesh(mesh, "Unity Source Mesh (CW winding)")

    # Export with Unity's winding flip
    path_exported = output_dir / "track_segment_unity_export.obj"
    mesh.write(str(path_exported), flip_winding=True)  # Unity flips winding
    print(f"\nWrote with winding flip: {path_exported}")

    # Parse and analyze the exported mesh
    exported = ObjMesh.parse(str(path_exported))
    analyze_mesh(exported, "Exported Mesh (after winding flip)")

    print("\n--- Diagnosis ---")
    print("If exported mesh shows 'INVERTED', the winding flip alone")
    print("is not sufficient - may need coordinate transform too.")


def test_handedness_conversion():
    """
    Test different approaches to convert Unity (left-handed) to OBJ (right-handed).

    Options:
    1. Flip winding only
    2. Negate Z + flip winding
    3. Negate Z only
    4. Negate X only
    """
    print("\n" + "="*60)
    print("TEST: Handedness Conversion Approaches")
    print("="*60)

    output_dir = Path(__file__).parent / "obj_test_output"
    output_dir.mkdir(exist_ok=True)

    # Original Unity mesh
    mesh = create_track_segment_mesh()

    print("Testing different conversion approaches:")

    approaches = [
        ("flip_winding_only", True, False, False),
        ("negate_z_flip_winding", True, True, False),
        ("negate_z_only", False, True, False),
        ("negate_x_only", False, False, True),
    ]

    for name, flip_winding, negate_z, negate_x in approaches:
        # Copy mesh
        verts = mesh.vertices.copy()
        norms = mesh.normals.copy()

        if negate_z:
            verts[:, 2] *= -1
            norms[:, 2] *= -1
        if negate_x:
            verts[:, 0] *= -1
            norms[:, 0] *= -1

        test_mesh = ObjMesh(vertices=verts, normals=norms, faces=mesh.faces)

        path = output_dir / f"track_{name}.obj"
        test_mesh.write(str(path), flip_winding=flip_winding)

        # Analyze
        result = ObjMesh.parse(str(path))

        # Quick check first face
        face = result.faces[0]
        v_indices = [f[0] for f in face]
        v0 = result.vertices[v_indices[0]]
        v1 = result.vertices[v_indices[1]]
        v2 = result.vertices[v_indices[2]]
        geom_normal = compute_face_normal(v0, v1, v2)
        vertex_normal = result.normals[face[0][1]]
        dot = np.dot(geom_normal, vertex_normal)

        status = "OK" if dot > 0 else "INVERTED"
        print(f"  {name}: dot={dot:.4f} -> {status}")
        print(f"    Wrote: {path}")


def main():
    print("OBJ Export Debug Tool")
    print("=====================")

    # Run all tests
    test_identity_roundtrip()
    test_winding_flip()
    test_unity_track_export()
    test_handedness_conversion()

    print("\n" + "="*60)
    print("DONE - Check tools/obj_test_output/ for generated files")
    print("Import these into Blender to visually verify")
    print("="*60)


if __name__ == "__main__":
    main()
