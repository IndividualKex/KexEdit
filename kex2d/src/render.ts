import type { Plugin, System } from "@dylanebert/shallot";

export interface Canvas2D {
    element: HTMLCanvasElement;
    ctx: CanvasRenderingContext2D;
}

export const Canvas2D: Canvas2D = {} as Canvas2D;

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

export const RenderPlugin: Plugin = {
    name: "Render",
    systems: [GridSystem],
};
