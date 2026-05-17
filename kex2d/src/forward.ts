const G_DEFAULT = 9.80665;
const V_MIN_DEFAULT = 0.1;

/** 2D sample: heart position (m), tangent angle from horizontal (rad), speed (m/s). */
export interface State {
    x: number;
    y: number;
    theta: number;
    v: number;
}

/**
 * Advance one sample by Δs along arclength, given a normal-force target in g.
 * Semi-implicit: angle updates first, position uses midpoint angle, velocity
 * from energy delta (preserves ½v² + gy to f64 round-off).
 */
export function step(
    prev: State,
    fN: number,
    ds: number,
    g: number = G_DEFAULT,
    vMin: number = V_MIN_DEFAULT,
): State {
    const vSafe = Math.max(Math.abs(prev.v), vMin);
    const dtheta = ((fN - Math.cos(prev.theta)) * g * ds) / (vSafe * vSafe);
    const theta = prev.theta + dtheta;
    const midTheta = 0.5 * (prev.theta + theta);
    const x = prev.x + ds * Math.cos(midTheta);
    const y = prev.y + ds * Math.sin(midTheta);
    const dy = y - prev.y;
    const vSq = prev.v * prev.v - 2 * g * dy;
    const v = Math.sqrt(Math.max(vSq, 0));
    return { x, y, theta, v };
}

/**
 * Walk N samples from the anchor using the F_n driver curve sampled at
 * σ_i = i · ds (source convention). `result[0]` is a copy of the anchor.
 */
export function integrate(
    anchor: State,
    fNCurve: (sigma: number) => number,
    N: number,
    ds: number,
    g: number = G_DEFAULT,
    vMin: number = V_MIN_DEFAULT,
): State[] {
    const result: State[] = new Array(N);
    result[0] = { ...anchor };
    for (let i = 0; i < N - 1; i++) {
        result[i + 1] = step(result[i], fNCurve(i * ds), ds, g, vMin);
    }
    return result;
}
