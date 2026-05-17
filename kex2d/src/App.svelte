<script lang="ts">
import { onMount } from "svelte";
import { attachCanvas2D } from "./render";

let canvas: HTMLCanvasElement;

onMount(() => {
    attachCanvas2D(canvas);
});
</script>

<canvas bind:this={canvas}></canvas>

<style>
    canvas {
        display: block;
        width: 100%;
        height: 100%;
    }
</style>
