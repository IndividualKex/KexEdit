import type { Plugin, State, System } from "@dylanebert/shallot";
import { Sample } from "./track";

export interface Canvas2D {
    element: HTMLCanvasElement;
    ctx: CanvasRenderingContext2D;
}

export const Canvas2D: Canvas2D = {} as Canvas2D;

const VIEW_HALF_X = 280;

function viewTransform(canvas: HTMLCanvasElement): {
    sx: number;
    sy: number;
    ox: number;
    oy: number;
} {
    const w = canvas.clientWidth;
    const h = canvas.clientHeight;
    const sx = w / (2 * VIEW_HALF_X);
    return { sx, sy: -sx, ox: w / 2, oy: h / 2 };
}

export function attachCanvas2D(element: HTMLCanvasElement): void {
    const ctx = element.getContext("2d");
    if (!ctx) throw new Error("2d context unavailable");
    Object.assign(Canvas2D, { element, ctx });
}

function resize(canvas: HTMLCanvasElement, ctx: CanvasRenderingContext2D): void {
    const dpr = window.devicePixelRatio || 1;
    const w = canvas.clientWidth;
    const h = canvas.clientHeight;
    if (canvas.width !== w * dpr || canvas.height !== h * dpr) {
        canvas.width = w * dpr;
        canvas.height = h * dpr;
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }
}

const GridSystem: System = {
    group: "draw",
    update(): void {
        const { element: canvas, ctx } = Canvas2D;
        if (!ctx) return;
        resize(canvas, ctx);

        const w = canvas.clientWidth;
        const h = canvas.clientHeight;

        ctx.fillStyle = "#0e0d0c";
        ctx.fillRect(0, 0, w, h);

        const spacing = 40;
        ctx.strokeStyle = "#1f1e1d";
        ctx.lineWidth = 1;
        ctx.beginPath();
        for (let x = (w / 2) % spacing; x < w; x += spacing) {
            ctx.moveTo(x, 0);
            ctx.lineTo(x, h);
        }
        for (let y = (h / 2) % spacing; y < h; y += spacing) {
            ctx.moveTo(0, y);
            ctx.lineTo(w, y);
        }
        ctx.stroke();

        ctx.strokeStyle = "#363534";
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(0, h / 2);
        ctx.lineTo(w, h / 2);
        ctx.moveTo(w / 2, 0);
        ctx.lineTo(w / 2, h);
        ctx.stroke();
    },
};

const TrackDrawSystem: System = {
    group: "draw",
    update(state: State): void {
        const { element: canvas, ctx } = Canvas2D;
        if (!ctx) return;
        const { sx, sy, ox, oy } = viewTransform(canvas);

        ctx.strokeStyle = "#cce5ff";
        ctx.lineWidth = 2;
        ctx.beginPath();
        let first = true;
        for (const eid of state.query([Sample])) {
            const x = Sample.pos.x.get(eid);
            const y = Sample.pos.y.get(eid);
            const cx = ox + x * sx;
            const cy = oy + y * sy;
            if (first) {
                ctx.moveTo(cx, cy);
                first = false;
            } else {
                ctx.lineTo(cx, cy);
            }
        }
        ctx.stroke();
    },
};

export const RenderPlugin: Plugin = {
    name: "Render",
    systems: [GridSystem, TrackDrawSystem],
};
