'use strict';

// RVAs for the fingerprint checked by the companion Python runner.
const ROTATED = 0x22840;
const INTENSITY = 0x22c00;
const SELECTION_BACKGROUND_CALL = 0x3b47e;
const SURFACE_VTABLE = 0x1dc6ec;
const REACTION_VTABLE = 0x1da840;
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

function watchReaction() {
    const reactionIds = new Map();
    let nextId = 0;
    let nextDraw = 0;

    function state(pointer) {
        const key = pointer.toString();
        if (!reactionIds.has(key)) reactionIds.set(key, ++nextId);
        return {
            reactionId: reactionIds.get(key),
            active: pointer.add(0x04).readU8() !== 0,
            counter: pointer.add(0x10).readS32(),
            primarySound: pointer.add(0x14).readU32(),
            secondarySound: pointer.add(0x18).readU32(),
            scratchEnabled: pointer.add(0x1c).readU8() !== 0,
        };
    }

    hook(0x56730, {
        onEnter() {
            const frame = scenes.get(this.threadId);
            const object = this.context.ecx;
            if (frame === undefined || imageRva(object.readPointer()) !== REACTION_VTABLE) return;
            if (object.add(0x10).readS32() === 0) reactionIds.delete(object.toString());
            record({ kind: 'reaction-update', frame, threadId: this.threadId, ...state(object) });
        },
    });
    hook(0x16bc0, {
        onEnter() {
            const frame = scenes.get(this.threadId);
            if (frame === undefined) return;
            this.object = this.context.ecx;
            this.previous = draws.get(this.threadId);
            this.draw = { frame, threadId: this.threadId, drawId: ++nextDraw };
            draws.set(this.threadId, this.draw);
            record({ kind: 'reaction-draw-enter', ...this.draw, ...state(this.object) });
        },
        onLeave() {
            if (this.draw === undefined) return;
            record({ kind: 'reaction-draw-exit', ...this.draw, ...state(this.object) });
            if (this.previous === undefined) draws.delete(this.threadId);
            else draws.set(this.threadId, this.previous);
        },
    });
    hook(0x1203c0, {
        onEnter(args) {
            const draw = draws.get(this.threadId);
            if (draw === undefined || imageRva(this.returnAddress) !== 0x16d4c || args[5].toInt32() !== 4) return;
            const source = args[0];
            const destination = this.context.ecx;
            function quad(pointer) {
                return Array.from({ length: 4 }, (_, i) => ({
                    x: pointer.add(i * 8).readS32(), y: pointer.add(i * 8 + 4).readS32(),
                }));
            }
            function format(surface) {
                return imageRva(surface.readPointer()) === SURFACE_VTABLE ? surface.add(0x74).readU32() : null;
            }
            record({
                kind: 'reaction-base-quad', ...draw,
                sourceQuad: quad(args[1]), destinationQuad: quad(args[2]),
                sourceFormat: format(source), destinationFormat: format(destination),
            });
        },
    });
}

function watchResultBanner() {
    const parents = new Map();
    const bannerDraws = new Map();
    const runIds = new Map();
    let nextRun = 0;

    hook(0x47000, {
        onEnter() {
            const parent = this.context.ecx;
            const banner = parent.add(0x2c);
            if (imageRva(banner.readPointer()) !== 0x1dbc48) return;
            this.previous = parents.get(this.threadId);
            this.tracked = true;
            parents.set(this.threadId, {
                banner, frame: ++frames,
                phase: parent.add(0x10).readS32(),
                subphase: parent.add(0x14).readS32(),
            });
        },
        onLeave() {
            if (!this.tracked) return;
            if (this.previous === undefined) parents.delete(this.threadId);
            else parents.set(this.threadId, this.previous);
        },
    });
    hook(0x44490, {
        onEnter() {
            const parent = parents.get(this.threadId);
            const banner = this.context.ecx;
            if (parent === undefined || !parent.banner.equals(banner) || !banner.add(0x14).readU8()) return;
            const before = banner.add(0x10).readS32();
            const key = banner.toString();
            if (before === 0 || !runIds.has(key)) runIds.set(key, ++nextRun);
            this.banner = banner;
            this.previous = bannerDraws.get(this.threadId);
            this.event = {
                kind: 'result-banner-draw', frame: parent.frame, runId: runIds.get(key),
                threadId: this.threadId, phase: parent.phase, subphase: parent.subphase,
                before, blit: null,
            };
            bannerDraws.set(this.threadId, this.event);
        },
        onLeave() {
            if (this.event === undefined) return;
            this.event.after = this.banner.add(0x10).readS32();
            this.event.activeAfter = this.banner.add(0x04).readU8() !== 0;
            record(this.event);
            if (this.previous === undefined) bannerDraws.delete(this.threadId);
            else bannerDraws.set(this.threadId, this.previous);
        },
    });
    hook(0x4451f, {
        onEnter() {
            const draw = bannerDraws.get(this.threadId);
            if (draw === undefined) return;
            const stack = this.context.esp;
            draw.blit = { x: stack.add(4).readS32(), y: stack.add(8).readS32(),
                intensity: stack.add(16).readS32() };
        },
    });
}

function watchDisabledChain() {
    const handlers = new Map();
    const producers = new Map([
        [0x1835d8, 0x183510],
        [0x188e94, 0x188d90],
        [0x188fdd, 0x188d90],
        [0x19cd51, 0x19ccf0],
        [0x1bb8b3, 0x1bb720],
        [0x1bb937, 0x1bb720],
    ]);
    hook(0x1b9170, {
        onEnter(args) {
            const command = args[0].toUInt32();
            if ((command & 0xfff) !== 0xb1) return;
            const returnRva = imageRva(this.returnAddress);
            record({
                kind: 'disabled-chain-request', threadId: this.threadId,
                frame: scenes.get(this.threadId) ?? null, returnRva,
                producerRva: producers.get(returnRva) ?? null,
                side: (command >>> 15) & 1, zone: args[1].toUInt32() & 0xffff,
                low: args[2].toUInt32() & 0xffff, high: args[3].toUInt32() & 0xffff,
            });
        },
    });
    hook(0x8730, {
        onEnter() {
            const argumentsBase = image.base.add(0x6550a0);
            this.previous = handlers.get(this.threadId);
            this.event = {
                kind: 'disabled-chain-handler', threadId: this.threadId,
                frame: scenes.get(this.threadId) ?? null,
                side: argumentsBase.readU16() >>> 15, zone: argumentsBase.add(2).readU16(),
                low: argumentsBase.add(4).readU16(), high: argumentsBase.add(6).readU16(),
                substate: image.base.add(0x6558ac).readU8() & 0x7f,
                bit5Write: null, descriptor: null,
            };
            handlers.set(this.threadId, this.event);
        },
        onLeave() {
            record(this.event);
            if (this.previous === undefined) handlers.delete(this.threadId);
            else handlers.set(this.threadId, this.previous);
        },
    });
    hook(0x170710, {
        onEnter(args) {
            const event = handlers.get(this.threadId);
            if (event === undefined || imageRva(this.returnAddress) !== 0x87e5 || args[2].toUInt32() !== 5)
                return;
            event.bit5Write = args[3].toUInt32() !== 0;
        },
    });
    hook(0x52f20, {
        onEnter(args) {
            const event = handlers.get(this.threadId);
            if (event === undefined || imageRva(this.returnAddress) !== 0x88de) return;
            event.descriptor = args[0].readU16();
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
        if (!['attack', 'selection', 'cadence', 'reaction', 'result', 'disabled-chain'].includes(mode))
            throw new Error('Unexpected capture mode');
        if (hooks.length !== 0) throw new Error('Capture is already active');
        events.length = 0;
        frames = dropped = 0;
        limit = mode === 'cadence' ? 16384 : 8192;
        scenes.clear();
        draws.clear();
        try {
            if (mode === 'result') {
                watchResultBanner();
                return;
            }
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
            if (mode === 'reaction') {
                watchReaction();
                return;
            }
            if (mode === 'disabled-chain') {
                watchDisabledChain();
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
