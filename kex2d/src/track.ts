import { type Plugin, type State, store, vec2 } from "@dylanebert/shallot";

export const Sample = {
    pos: store(vec2),
};

export const Track = {
    count: 0,
    ds: 0,
};

const SAMPLES = 1024;
const DS = 0.5;
const AMPLITUDE = 30;
const WAVELENGTH = 40;

function seed(state: State): void {
    const halfLen = (SAMPLES * DS) / 2;
    for (let i = 0; i < SAMPLES; i++) {
        const eid = state.create();
        state.add(eid, Sample);
        const s = i * DS - halfLen;
        Sample.pos.set(eid, s, AMPLITUDE * Math.sin(s / WAVELENGTH));
    }
    Track.count = SAMPLES;
    Track.ds = DS;
}

export const TrackPlugin: Plugin = {
    name: "Track",
    components: { Sample },
    traits: {
        Sample: { defaults: () => ({ pos: [0, 0] }) },
    },
    initialize(state) {
        seed(state);
    },
};
