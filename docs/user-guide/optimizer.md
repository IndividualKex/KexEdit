# Optimizer

Use gradient descent optimization to achieve precise roll, pitch, or yaw values.

<video width="100%" controls>
  <source src="../images/roll-to-zero.mp4" type="video/mp4">
  Your browser does not support the video tag.
</video>

_Using the optimizer to automatically adjust banking for a perfect roll-to-zero turn_

## How It Works

The optimizer adjusts selected keyframe values to reach target orientations at specific track positions.

**Process**: Gradient descent algorithm iteratively modifies the keyframe value until the roll, pitch, or yaw at the playhead position matches your target.

## Roll-to-Zero Example

Create a banked turn that returns to level track.

### Setup the Turn

1. **Create** a Geometric Section with 4 roll speed keyframes
2. **Lower** the 2nd keyframe - banks track left
3. **Raise** the 3rd keyframe - banks track back right
4. **Problem**: Final roll rarely returns to exactly 0°

### Optimize the Exit

1. **Position Playhead** at the 4th keyframe (or beyond)
2. **Right-click** the 3rd keyframe
3. **Optimize** → **Roll**
4. **Target**: Leave at 0° (default)
5. **Start** - Optimizer adjusts the keyframe value

### Result

The 3rd keyframe value automatically adjusts until the track roll at the playhead position reaches exactly 0°.

## Optimization Targets

| Target    | Use Case          | Example                                   |
| --------- | ----------------- | ----------------------------------------- |
| **Roll**  | Banking control   | Level track exits, precise banking angles |
| **Pitch** | Elevation control | Specific hill heights, level sections     |
| **Yaw**   | Direction control | Exact turn angles, parallel sections      |

## Usage Tips

Typically, optimization works best by creating 3 keyframes, placing the playhead at the final keyframe, and optimizing the middle keyframe.

---

**Next**: [Advanced Techniques - Complete Circuits](complete-circuits.md)

---

[← Back to Documentation](../)
