#!/usr/bin/env python3
"""
Debug script to investigate force section drift between Rust and Burst implementations.
Implements the physics simulation in Python to understand step-by-step behavior.
"""

import json
import math
from dataclasses import dataclass
from typing import List, Tuple

# Constants matching both implementations
G = 9.80665
HZ = 100.0
DT = 1.0 / HZ
EPSILON = 1.192093e-7
MIN_VELOCITY = 1e-3


@dataclass
class Float3:
    x: float
    y: float
    z: float

    def __add__(self, other):
        return Float3(self.x + other.x, self.y + other.y, self.z + other.z)

    def __sub__(self, other):
        return Float3(self.x - other.x, self.y - other.y, self.z - other.z)

    def __mul__(self, scalar):
        return Float3(self.x * scalar, self.y * scalar, self.z * scalar)

    def __neg__(self):
        return Float3(-self.x, -self.y, -self.z)

    def dot(self, other):
        return self.x * other.x + self.y * other.y + self.z * other.z

    def cross(self, other):
        return Float3(
            self.y * other.z - self.z * other.y,
            self.z * other.x - self.x * other.z,
            self.x * other.y - self.y * other.x
        )

    def magnitude(self):
        return math.sqrt(self.x * self.x + self.y * self.y + self.z * self.z)

    def normalize(self):
        mag = self.magnitude()
        if mag < 1e-10:
            return Float3(0, 0, 0)
        return self * (1.0 / mag)

    @staticmethod
    def from_dict(d):
        return Float3(d['x'], d['y'], d['z'])

    @staticmethod
    def UP():
        return Float3(0, 1, 0)

    @staticmethod
    def DOWN():
        return Float3(0, -1, 0)


@dataclass
class Quaternion:
    x: float
    y: float
    z: float
    w: float

    @staticmethod
    def from_axis_angle(axis: Float3, angle: float):
        half = angle * 0.5
        s = math.sin(half)
        c = math.cos(half)
        n = axis.normalize()
        return Quaternion(n.x * s, n.y * s, n.z * s, c)

    def __mul__(self, other):
        return Quaternion(
            self.w * other.x + self.x * other.w + self.y * other.z - self.z * other.y,
            self.w * other.y - self.x * other.z + self.y * other.w + self.z * other.x,
            self.w * other.z + self.x * other.y - self.y * other.x + self.z * other.w,
            self.w * other.w - self.x * other.x - self.y * other.y - self.z * other.z
        )

    def mul_vec(self, v: Float3) -> Float3:
        qv = Float3(self.x, self.y, self.z)
        uv = qv.cross(v)
        uuv = qv.cross(uv)
        return v + (uv * (2.0 * self.w)) + (uuv * 2.0)


@dataclass
class Frame:
    direction: Float3
    normal: Float3
    lateral: Float3

    def roll(self) -> float:
        return math.atan2(self.lateral.y, -self.normal.y)

    def pitch(self) -> float:
        mag = math.sqrt(self.direction.x ** 2 + self.direction.z ** 2)
        return math.atan2(self.direction.y, mag)

    def with_roll(self, delta_roll: float):
        q = Quaternion.from_axis_angle(self.direction, -delta_roll)
        new_lateral = q.mul_vec(self.lateral).normalize()
        new_normal = self.direction.cross(new_lateral).normalize()
        return Frame(self.direction, new_normal, new_lateral)

    @staticmethod
    def from_dict(d):
        return Frame(
            Float3.from_dict(d['direction']),
            Float3.from_dict(d['normal']),
            Float3.from_dict(d['lateral'])
        )


def step_by_forces(prev: Frame, normal_force: float, lateral_force: float,
                   velocity: float, heart_advance: float) -> Frame:
    """Python implementation matching both C# and Rust step_by_forces."""
    force_vec = (prev.normal * (-normal_force)
                 + prev.lateral * (-lateral_force)
                 + Float3.DOWN())
    normal_accel = -force_vec.dot(prev.normal) * G
    lateral_accel = -force_vec.dot(prev.lateral) * G

    # estimated_velocity from heart_advance
    if abs(heart_advance) < EPSILON:
        estimated_velocity = velocity
    else:
        estimated_velocity = heart_advance * HZ
    if abs(estimated_velocity) < EPSILON:
        estimated_velocity = EPSILON

    # safe_velocity for lateral rotation
    safe_velocity = velocity if abs(velocity) >= EPSILON else EPSILON

    # Quaternion rotations
    q_normal = Quaternion.from_axis_angle(prev.lateral, normal_accel / estimated_velocity / HZ)
    q_lateral = Quaternion.from_axis_angle(prev.normal, -lateral_accel / safe_velocity / HZ)
    combined = q_normal * q_lateral

    new_direction = combined.mul_vec(prev.direction).normalize()
    new_lateral = q_lateral.mul_vec(prev.lateral).normalize()
    new_normal = new_direction.cross(new_lateral).normalize()

    return Frame(new_direction, new_normal, new_lateral)


def analyze_first_point_step(section):
    """Analyze the first physics step in detail."""
    inputs = section['inputs']
    outputs = section['outputs']
    gold_points = outputs['points']

    if len(gold_points) < 2:
        print("Not enough points for analysis")
        return

    anchor = gold_points[0]
    second = gold_points[1]

    # Extract frame from anchor
    prev_frame = Frame.from_dict(anchor)
    velocity = anchor['velocity']
    heart_advance = anchor['heartAdvance']
    normal_force = anchor['normalForce']
    lateral_force = anchor['lateralForce']

    print("\n=== First Step Analysis ===")
    print(f"Anchor normal_force: {normal_force:.6f}")
    print(f"Anchor lateral_force: {lateral_force:.6f}")
    print(f"Anchor velocity: {velocity:.6f}")
    print(f"Anchor heart_advance: {heart_advance:.6f}")

    # Compute force vector
    force_vec = (prev_frame.normal * (-normal_force)
                 + prev_frame.lateral * (-lateral_force)
                 + Float3.DOWN())
    print(f"\nForce vector: ({force_vec.x:.8f}, {force_vec.y:.8f}, {force_vec.z:.8f})")

    normal_accel = -force_vec.dot(prev_frame.normal) * G
    lateral_accel = -force_vec.dot(prev_frame.lateral) * G
    print(f"normal_accel: {normal_accel:.8f}")
    print(f"lateral_accel: {lateral_accel:.8f}")

    # estimated velocity
    if abs(heart_advance) < EPSILON:
        estimated_velocity = velocity
    else:
        estimated_velocity = heart_advance * HZ
    if abs(estimated_velocity) < EPSILON:
        estimated_velocity = EPSILON
    safe_velocity = velocity if abs(velocity) >= EPSILON else EPSILON
    print(f"estimated_velocity: {estimated_velocity:.8f}")
    print(f"safe_velocity: {safe_velocity:.8f}")

    # Rotation angles
    normal_angle = normal_accel / estimated_velocity / HZ
    lateral_angle = -lateral_accel / safe_velocity / HZ
    print(f"normal rotation angle: {normal_angle:.10f} rad")
    print(f"lateral rotation angle: {lateral_angle:.10f} rad")

    # Execute step
    result_frame = step_by_forces(prev_frame, normal_force, lateral_force, velocity, heart_advance)

    print(f"\nResult direction: ({result_frame.direction.x:.8f}, {result_frame.direction.y:.8f}, {result_frame.direction.z:.8f})")
    print(f"Gold direction:   ({second['direction']['x']:.8f}, {second['direction']['y']:.8f}, {second['direction']['z']:.8f})")

    diff_x = result_frame.direction.x - second['direction']['x']
    diff_y = result_frame.direction.y - second['direction']['y']
    diff_z = result_frame.direction.z - second['direction']['z']
    print(f"Direction diff: ({diff_x:.2e}, {diff_y:.2e}, {diff_z:.2e})")


def compare_velocity_evolution(section, max_points=200):
    """Compare velocity and position evolution."""
    gold_points = section['outputs']['points']

    print("\n=== Velocity/Energy Comparison ===")
    print(f"{'Idx':>5} {'Gold Vel':>12} {'Gold Energy':>14} {'Gold HeartArc':>14} {'dVel':>12}")

    prev_vel = None
    for idx in range(0, min(max_points, len(gold_points)), 10):
        pt = gold_points[idx]
        d_vel = (pt['velocity'] - prev_vel) if prev_vel is not None else 0.0
        print(f"{idx:>5} {pt['velocity']:>12.6f} {pt['energy']:>14.6f} {pt['heartArc']:>14.6f} {d_vel:>+12.6f}")
        prev_vel = pt['velocity']


def main():
    with open('rust-backend/test-data/veloci.json', 'r', encoding='utf-8-sig') as f:
        data = json.load(f)

    force_sections = [s for s in data['sections'] if s['nodeType'] == 'ForceSection']

    if not force_sections:
        print("No force sections found!")
        return

    print(f"Found {len(force_sections)} force sections")

    section = force_sections[0]
    outputs = section['outputs']
    print(f"\n{'='*60}")
    print(f"Force Section 0: {outputs['pointCount']} points")
    print(f"{'='*60}")

    analyze_first_point_step(section)
    compare_velocity_evolution(section)


if __name__ == "__main__":
    import os
    os.chdir(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    main()
