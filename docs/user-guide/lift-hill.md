# Lift Hill

Create classic chain lift hills with curved transitions and straight inclined sections.

![Lift Hill Node Setup](../images/lift-hill.png)
_Complete lift hill setup with bottom curve, straight lift section, and top curve_

## About Lift Hills

**Lift hills** are essential coaster elements that slowly carry trains up steep inclines using a chain mechanism. A proper lift consists of three parts: a curved transition at the bottom, a straight inclined section, and a curved transition at the top.

**Speed**: Typically 3-5 m/s for realistic chain lift operation.

## Building the Lift Hill

### Bottom Curve Transition

Create the entry transition that curves upward into the lift.

1. **Add Curved Section** - Right-click → **Curved Section**
2. **Set Upward Direction** - Change **Axis** to `90` (curves up instead of right)
3. **Set Incline Angle** - Lower **Arc** to `30` for a 30-degree incline
4. **Add Velocity Control** - Timeline → **Add Property** → **Fixed Velocity**
5. **Set Lift Speed** - Add keyframe with desired velocity (e.g. `3 m/s`)

### Straight Lift Section

Build the main inclined section with consistent speed and pitch.

1. **Connect Geometric Section** - Drag from curved section output to new **Geometric Section**
2. **Maintain Speed** - Add **Fixed Velocity** property (inherits previous `3 m/s` value)
3. **Straighten Section** - Add **Pitch Speed** keyframe set to `0 rad/s`
4. **Extend Duration** - Increase section length to desired lift hill height

### Top Curve Transition

Complete the lift with a downward transition curve.

1. **Connect Curved Section** - Drag from geometric section output to new **Curved Section**
2. **Set Downward Direction** - Change **Axis** to `-90` (curves down)
3. **Match Angle** - Set **Arc** to `30` degrees
4. **Continue Speed Control** - Add **Fixed Velocity** property

## Common Issues

**Problem**: Lift hill has unwanted banking or rotation

-   **Solution**: Add **Roll Speed** and **Yaw Speed** keyframes set to `0 rad/s` on the geometric section

**Problem**: Speed changes unexpectedly between sections

-   **Solution**: Ensure each section has a **Fixed Velocity** property to maintain chain lift speed

---

**Next**: [Advanced Techniques - Complete Circuits](complete-circuits.md)

---

[← Back to Documentation](../)
