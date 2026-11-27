'use strict';

// RVAs for the fingerprint checked by the companion Python runner.
const ROTATED = 0x22840;
const INTENSITY = 0x22c00;
const sites = new Map([
    [0x146ea, { primitive: ROTATED, key: 0, pass: 'flight' }],
    [0x14777, { primitive: INTENSITY, key: 2, pass: 'impact' }],
    [0x14805, { primitive: INTENSITY, key: 1, pass: 'partial-sword' }],
    [0x148fa, { primitive: ROTATED, key: 0, pass: 'aim' }],
]);
const assets = ['atk_icon.bmp', 'atk_icon2.bmp', 'atk_eff.yga'];
const image = Process.mainModule;
const scenes = new Map();
const draws = new Map();
const events = [];
const hooks = [];
let frames = 0;
let dropped = 0;
const limit = 8192;

function hook(rva, callbacks) {
    hooks.push(Interceptor.attach(image.base.add(rva), callbacks));
}

function record(event) {
    if (events.length < limit) events.push(event);
    else dropped++;
}

function attackState(context) {
    const attack = context.ecx;
    return {
        state: attack.add(0x10).readS32(),
        counter: attack.add(0x14).readS32(),
        attackerSide: attack.add(0x18).readS32(),
        attackerSlot: attack.add(0x1c).readS32(),
        targetSide: attack.add(0x20).readS32(),
        targetSlot: attack.add(0x24).readS32(),
    };
}

function watchPrimitive(primitive) {
    hook(primitive, {
        onEnter(args) {
            const draw = draws.get(this.threadId);
            if (draw === undefined) return;
            const returnRva = this.returnAddress.sub(image.base).toUInt32();
            const site = sites.get(returnRva);
            if (site === undefined || site.primitive !== primitive) return;
            record({
                kind: 'blit-request',
                ...draw,
                primitiveRva: primitive,
                returnRva,
                pass: site.pass,
                manifestKey: site.key,
                asset: assets[site.key],
                x: args[2].toInt32(),
                y: args[3].toInt32(),
                angleIndex: args[4].toInt32(),
                scaleX16: args[5].toInt32(),
                scaleY16: args[6].toInt32(),
                intensity: primitive === INTENSITY ? args[7].toInt32() : null,
                mode: primitive === ROTATED ? args[7].toInt32() : null,
            });
        },
    });
}

function stop() {
    for (const listener of hooks.splice(0)) listener.detach();
    return { frames, dropped, events };
}

rpc.exports = {
    imagePath() { return image.path; },
    start() {
        if (Process.arch !== 'ia32' || image.size < 0x4dd01)
            throw new Error('Unexpected original process architecture or image size');
        if (hooks.length !== 0) throw new Error('Capture is already active');
        try {
            hook(0x4dd00, {
                onEnter() {
                    this.previous = scenes.get(this.threadId);
                    scenes.set(this.threadId, ++frames);
                },
                onLeave() {
                    if (this.previous === undefined) scenes.delete(this.threadId);
                    else scenes.set(this.threadId, this.previous);
                },
            });
            hook(0x14110, {
                onEnter() {
                    const frame = scenes.get(this.threadId);
                    if (frame === undefined) return;
                    record({ kind: 'update-enter', frame, ...attackState(this.context) });
                },
            });
            hook(0x142c0, {
                onEnter() {
                    const frame = scenes.get(this.threadId);
                    if (frame === undefined) return;
                    this.previous = draws.get(this.threadId);
                    this.tracked = true;
                    const draw = { frame, drawRva: 0x142c0, ...attackState(this.context) };
                    draws.set(this.threadId, draw);
                    record({ kind: 'draw-enter', ...draw });
                },
                onLeave() {
                    if (!this.tracked) return;
                    if (this.previous === undefined) draws.delete(this.threadId);
                    else draws.set(this.threadId, this.previous);
                },
            });
            watchPrimitive(ROTATED);
            watchPrimitive(INTENSITY);
        } catch (error) {
            stop();
            throw error;
        }
    },
    stop,
};
