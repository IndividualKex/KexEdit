import { describe, expect, test } from "bun:test";
import { integrate, step, type State } from "../src/forward";

const G = 9.80665;

describe("step", () => {
    test("flat F_n=1 at θ=0 advances straight horizontal, v constant", () => {
        const prev: State = { x: 0, y: 0, theta: 0, v: 10 };
        const next = step(prev, 1.0, 0.5, G);

        expect(next.x).toBeCloseTo(0.5, 12);
        expect(next.y).toBeCloseTo(0, 12);
        expect(next.theta).toBeCloseTo(0, 12);
        expect(next.v).toBeCloseTo(10, 12);
    });

    test("does not mutate input state", () => {
        const prev: State = { x: 1, y: 2, theta: 0.3, v: 5 };
        const snapshot = { ...prev };
        step(prev, 1.5, 0.5, G);
        expect(prev).toEqual(snapshot);
    });

    test("dθ matches (F_n − cos θ) · g · Δs / v²", () => {
        const fN = 2.5;
        const v = 15;
        const theta = 0.4;
        const ds = 0.3;
        const expected = ((fN - Math.cos(theta)) * G * ds) / (v * v);

        const prev: State = { x: 0, y: 0, theta, v };
        const next = step(prev, fN, ds, G);

        expect(next.theta - prev.theta).toBeCloseTo(expected, 10);
    });

    test("F_n=0 at θ=0 curves downward (negative dθ)", () => {
        const prev: State = { x: 0, y: 0, theta: 0, v: 10 };
        const next = step(prev, 0, 0.1, G);
        expect(next.theta).toBeLessThan(0);
    });

    test("F_n > cos θ curves upward (positive dθ)", () => {
        const prev: State = { x: 0, y: 0, theta: 0, v: 10 };
        const next = step(prev, 2.0, 0.1, G);
        expect(next.theta).toBeGreaterThan(0);
    });

    test("position uses midpoint angle, not source angle", () => {
        // At F_n large enough to bend θ noticeably, the y advance is
        // sin(θ̄)·Δs, not sin(θ_i)·Δs. Start at θ=0 with F_n=2: θ_{i+1} > 0,
        // midpoint > 0, so y advances positive even though sin(0) = 0.
        const prev: State = { x: 0, y: 0, theta: 0, v: 10 };
        const ds = 0.5;
        const next = step(prev, 3.0, ds, G);

        const dtheta = next.theta - prev.theta;
        const midAngle = 0.5 * (prev.theta + next.theta);
        expect(next.x).toBeCloseTo(ds * Math.cos(midAngle), 10);
        expect(next.y).toBeCloseTo(ds * Math.sin(midAngle), 10);
        expect(dtheta).toBeGreaterThan(0);
    });

    test("energy ½v² + g·y is preserved per step", () => {
        const prev: State = { x: 0, y: 0, theta: 0.2, v: 12 };
        const next = step(prev, 1.5, 0.5, G);
        const ePrev = 0.5 * prev.v * prev.v + G * prev.y;
        const eNext = 0.5 * next.v * next.v + G * next.y;
        expect(eNext).toBeCloseTo(ePrev, 10);
    });

    test("v=0 below v_min does not blow up", () => {
        const prev: State = { x: 0, y: 0, theta: 0, v: 0 };
        const next = step(prev, 1.0, 0.5, G, 0.1);
        expect(Number.isFinite(next.x)).toBe(true);
        expect(Number.isFinite(next.y)).toBe(true);
        expect(Number.isFinite(next.theta)).toBe(true);
        expect(Number.isFinite(next.v)).toBe(true);
    });

    test("v clamps to zero when energy would go negative", () => {
        // Steep upward angle, low speed: would-be v² goes negative, clamps to 0.
        const prev: State = { x: 0, y: 0, theta: Math.PI / 2, v: 0.5 };
        const next = step(prev, 1.0, 1.0, G);
        expect(next.v).toBe(0);
    });
});

describe("integrate", () => {
    test("output length equals N", () => {
        const anchor: State = { x: 0, y: 0, theta: 0, v: 10 };
        const result = integrate(anchor, () => 1.0, 16, 0.5, G);
        expect(result.length).toBe(16);
    });

    test("first state is anchor (deep equal)", () => {
        const anchor: State = { x: 1, y: 2, theta: 0.3, v: 10 };
        const result = integrate(anchor, () => 1.0, 8, 0.5, G);
        expect(result[0]).toEqual(anchor);
    });

    test("flat F_n=1 produces straight horizontal line", () => {
        const anchor: State = { x: 0, y: 0, theta: 0, v: 10 };
        const ds = 0.5;
        const N = 32;
        const result = integrate(anchor, () => 1.0, N, ds, G);

        for (let i = 0; i < N; i++) {
            expect(result[i].x).toBeCloseTo(i * ds, 9);
            expect(result[i].y).toBeCloseTo(0, 10);
            expect(result[i].theta).toBeCloseTo(0, 10);
            expect(result[i].v).toBeCloseTo(10, 9);
        }
    });

    test("energy ½v² + g·y is preserved across all samples", () => {
        const anchor: State = { x: 0, y: 0, theta: 0.1, v: 15 };
        const result = integrate(anchor, () => 0.5, 64, 0.4, G);
        const E0 = 0.5 * anchor.v * anchor.v + G * anchor.y;
        for (const s of result) {
            const E = 0.5 * s.v * s.v + G * s.y;
            expect(E).toBeCloseTo(E0, 8);
        }
    });

    test("ballistic F_n=0 at θ=0: y descends, v increases", () => {
        const anchor: State = { x: 0, y: 0, theta: 0, v: 5 };
        const result = integrate(anchor, () => 0, 32, 0.5, G);
        const last = result[result.length - 1];
        expect(last.y).toBeLessThan(0);
        expect(last.v).toBeGreaterThan(anchor.v);
    });

    test("integrate matches step-by-step composition (source-σ convention)", () => {
        const anchor: State = { x: 0, y: 0, theta: 0.05, v: 12 };
        const fN = (s: number) => 1.0 + 0.5 * Math.sin(s);
        const ds = 0.4;
        const N = 10;

        const result = integrate(anchor, fN, N, ds, G);

        let manual: State = anchor;
        expect(result[0]).toEqual(anchor);
        for (let i = 0; i < N - 1; i++) {
            // Source convention: F_n sampled at σ_i drives step i → i+1
            manual = step(manual, fN(i * ds), ds, G);
            expect(result[i + 1]).toEqual(manual);
        }
    });

    test("driver curve is sampled at σ_i = i · ds for i in [0, N−1)", () => {
        const anchor: State = { x: 0, y: 0, theta: 0, v: 10 };
        const ds = 0.5;
        const N = 8;
        const queried: number[] = [];
        integrate(
            anchor,
            (s) => {
                queried.push(s);
                return 1.0;
            },
            N,
            ds,
            G,
        );
        expect(queried.length).toBe(N - 1);
        for (let i = 0; i < N - 1; i++) {
            expect(queried[i]).toBeCloseTo(i * ds, 12);
        }
    });

    test("anchor with θ=π/2 + v=10 + F_n=1: vertical launch starts curving back", () => {
        // Pointing straight up, normal force = 1g (gravity-only): the rider
        // continues along the +y direction initially. dθ at θ=π/2 is
        // (1 − cos(π/2))·g·ds/v² = g·ds/v² > 0 → curls toward +θ (away from
        // vertical, into a backflip). Smoke test that the integrator handles
        // the vertical case without singularity.
        const anchor: State = { x: 0, y: 0, theta: Math.PI / 2, v: 10 };
        const result = integrate(anchor, () => 1.0, 32, 0.5, G);
        // y should rise (we're going up, decelerating)
        expect(result[1].y).toBeGreaterThan(anchor.y);
        // No NaN/Inf
        for (const s of result) {
            expect(Number.isFinite(s.x)).toBe(true);
            expect(Number.isFinite(s.y)).toBe(true);
            expect(Number.isFinite(s.theta)).toBe(true);
            expect(Number.isFinite(s.v)).toBe(true);
        }
    });
});
