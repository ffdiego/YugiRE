'use strict';

// RVAs for the fingerprint checked by the companion Python runner.
const ROTATED = 0x22840;
const INTENSITY = 0x22c00;
const SELECTION_BACKGROUND_CALL = 0x3b47e;
const SURFACE_VTABLE = 0x1dc6ec;
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
let limit = 8192;
let cadence = null;

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

function imageRva(address) {
    const rva = address.sub(image.base).toUInt32();
    return rva < image.size ? rva : null;
}

function watchSelectionBackground() {
    const call = image.base.add(SELECTION_BACKGROUND_CALL);
    if (call.readU8() !== 0xff || call.add(1).readU8() !== 0x50 || call.add(2).readU8() !== 0x4c)
        throw new Error('Unexpected stable selection blit instruction');
    hook(SELECTION_BACKGROUND_CALL, {
        onEnter() {
            const frame = scenes.get(this.threadId);
            if (frame === undefined) return;
            const { esp, ecx: destination, edx } = this.context;
            const source = esp.readPointer();
            const sourceVtable = source.readPointer();
            const destinationVtable = destination.readPointer();
            const sourceLayoutKnown = imageRva(sourceVtable) === SURFACE_VTABLE;
            const keyFromPixel = sourceLayoutKnown ? source.add(0x55).readU8() !== 0 : null;
            record({
                kind: 'selection-background',
                frame,
                drawRva: 0x3b280,
                callRva: SELECTION_BACKGROUND_CALL,
                manifestKey: edx.toUInt32(),
                x: esp.add(4).readS32(),
                y: esp.add(8).readS32(),
                destinationVtableRva: imageRva(destinationVtable),
                destinationMethodRva: imageRva(destinationVtable.add(0x4c).readPointer()),
                sourceVtableRva: imageRva(sourceVtable),
                sourceFormat: sourceLayoutKnown ? source.add(0x74).readU32() : null,
                sourceFlag78: sourceLayoutKnown ? source.add(0x78).readU8() : null,
                keyFromPixel,
                keyX: keyFromPixel === true ? source.add(0x5c).readS32() : null,
                keyY: keyFromPixel === true ? source.add(0x60).readS32() : null,
                keyColorRef: keyFromPixel === false ? source.add(0x58).readU32() : null,
                packedKey: sourceLayoutKnown ? source.add(0x64).readU32() : null,
            });
        },
    });
}

function startCadenceClock() {
    const kernel = Process.getModuleByName('kernel32.dll');
    const buffer = Memory.alloc(8);
    const frequency = new NativeFunction(kernel.getExportByName('QueryPerformanceFrequency'),
        'int', ['pointer'], 'stdcall');
    const counter = new NativeFunction(kernel.getExportByName('QueryPerformanceCounter'),
        'int', ['pointer'], 'stdcall');
    if (frequency(buffer) === 0) throw new Error('QueryPerformanceFrequency failed');
    const frequencyHz = buffer.readS64().toNumber();
    if (frequencyHz <= 0) throw new Error('Invalid performance counter frequency');
    function ticks() {
        if (counter(buffer) === 0) throw new Error('QueryPerformanceCounter failed');
        return buffer.readS64();
    }
    const origin = ticks();
    return {
        frequencyHz,
        nominalHz: image.base.add(0x1eb618).readU32(),
        ticks: () => ticks().sub(origin).toNumber(),
    };
}

function watchCadenceDispatcher(rva, kind, maskIndex, sceneReturns) {
    hook(rva, {
        onEnter(args) {
            const frame = scenes.get(this.threadId);
            if (frame === undefined) return;
            const returnRva = imageRva(this.returnAddress);
            if (!sceneReturns.includes(returnRva)) return;
            record({
                kind, frame, threadId: this.threadId, returnRva,
                ticks: cadence.ticks(), mask: args[maskIndex].toUInt32(),
            });
        },
    });
}

function stop() {
    for (const listener of hooks.splice(0)) listener.detach();
    const result = { frames, dropped, events };
    if (cadence !== null) {
        result.frequencyHz = cadence.frequencyHz;
        result.nominalHz = cadence.nominalHz;
    }
    cadence = null;
    return result;
}

rpc.exports = {
    imagePath() { return image.path; },
    start(mode = 'attack') {
        if (Process.arch !== 'ia32' || image.size < 0x4dd01)
            throw new Error('Unexpected original process architecture or image size');
        if (mode !== 'attack' && mode !== 'selection' && mode !== 'cadence')
            throw new Error('Unexpected capture mode');
        if (hooks.length !== 0) throw new Error('Capture is already active');
        events.length = 0;
        frames = dropped = 0;
        limit = mode === 'cadence' ? 16384 : 8192;
        scenes.clear();
        draws.clear();
        try {
            if (mode === 'cadence') cadence = startCadenceClock();
            hook(0x4dd00, {
                onEnter() {
                    this.previous = scenes.get(this.threadId);
                    scenes.set(this.threadId, ++frames);
                    if (cadence !== null)
                        record({ kind: 'scene', frame: frames, threadId: this.threadId, ticks: cadence.ticks() });
                },
                onLeave() {
                    if (this.previous === undefined) scenes.delete(this.threadId);
                    else scenes.set(this.threadId, this.previous);
                },
            });
            if (mode === 'cadence') {
                watchCadenceDispatcher(0x5d040, 'dispatch-update', 0, [0x4dd47, 0x4dd8a]);
                watchCadenceDispatcher(0x5d170, 'dispatch-draw', 1, [0x4dd62, 0x4dd92]);
                return;
            }
            if (mode === 'selection') {
                watchSelectionBackground();
                return;
            }
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
