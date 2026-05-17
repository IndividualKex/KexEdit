import { defineConfig } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";

export default defineConfig({
    base: "./",
    plugins: [svelte()],
    server: {
        port: 3000,
        fs: {
            // allow the bun-linked shallot dev copy two levels up.
            allow: [".", "../../shallot"],
        },
    },
    build: {
        target: "esnext",
        outDir: "dist",
        emptyOutDir: true,
    },
});
