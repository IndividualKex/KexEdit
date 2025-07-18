# Controlling Properties

Use timeline properties to create dynamic track elements like launches and hills.

![Launch and Hill](../images/controlling-properties.png)
_Launch and hill created with timeline properties_

## Creating a Launch (Velocity Control)

Add smooth acceleration to your track with velocity overrides.

### Setup Launch Section

1. **Select** the Geometric Section node
2. **Timeline Outliner** shows available properties on the left
3. **Add Property** → **Fixed Velocity**
4. **Increase Duration** - Drag ruler end or adjust node input

### Add Velocity Keyframes

1. **Set Starting Speed** - Click keyframe button, enter `0.1` m/s at time 0
2. **Set Launch Speed** - Add keyframe near section end with `30` m/s
3. **View Curve** - Toggle Curve View button to see acceleration curve
4. **Edit Keyframes** - Double-click keyframes to open edit panel for precise time, value, and easing control

## Creating a Hill (Normal Force Control)

Shape track elevation using normal force values.

### Setup Hill Section

1. **Select** the Force Section node
2. **Increase Duration** as needed

### Add Force Keyframes

| Time | Normal Force | Effect                           |
| ---- | ------------ | -------------------------------- |
| 0s   | Default      | Transition from previous section |
| 1s   | 3.5G         | Pull up (creates hill)           |
| 2s   | 0G           | Weightless (airtime)             |

### Result

Creates a hill with airtime - riders experience strong downward force, then weightlessness.

---

**Next**: [Getting Started - Lift Hill](lift-hill.md)

---

[← Back to Documentation](../)
