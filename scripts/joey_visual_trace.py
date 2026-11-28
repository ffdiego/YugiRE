#!/usr/bin/env python3
"""Read-only Windows collector for known Joey presentation contracts."""
import argparse
import ctypes
from ctypes import wintypes
from datetime import datetime
import hashlib
import json
from pathlib import Path
import sys
import time

PROFILES = ("placement", "activation", "reactions", "cursor", "battle", "all")
EXPECTED = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
AGENT = r'''
'use strict';
const image = Process.mainModule, base = image.base;
const hooks = [], events = [], frames = new Map();
const cursorPasses = new Map();
const limit = 100000;
let frame = 0, cursorPass = 0, dropped = 0, origin = Date.now();
function at(va) { return base.add(va - 0x400000); }
function va(p) { const n = p.sub(base).toUInt32(); return n < image.size ? n + 0x400000 : null; }
function read(p, offset, kind) { try { return p.add(offset)[kind](); } catch (_) { return null; } }
function push(event) { if (events.length >= limit) { dropped++; return; } events.push(event); }
function record(kind, ctx, fields) {
    push(Object.assign({kind, ms:Date.now()-origin, thread:ctx.threadId,
        frame:frames.get(ctx.threadId)||null, cursorPass:cursorPasses.get(ctx.threadId)||null,
        caller:va(ctx.returnAddress),
        appTick:read(at(0x005f2fb4),0,'readU32'),
        opcode:currentOpcode()},fields));
}
function commandWords() {
    return [0,2,4,6].map(off=>read(at(0x00a550a0),off,'readU16'));
}
function currentOpcode() {
    const word=read(at(0x00a550a0),0,'readU16');
    return word===null?null:word&0xfff;
}
function hook(address, callbacks) { hooks.push(Interceptor.attach(at(address),callbacks)); }
function snapshot(p, offsets) {
    const result = {object:p.toString()};
    for (const [key,off,type] of offsets) result[key]=read(p,off,type);
    return result;
}
const effectFields = [['counter',0x10,'readS32'],['active',4,'readU8'],
    ['x',0x14,'readS32'],['y',0x18,'readS32']];
const reactionFields = [['counter',0x10,'readS32'],['active',4,'readU8'],
    ['primary',0x14,'readU32'],['secondary',0x18,'readU32'],['gate',0x1c,'readU8']];
const travelFields = [['counter',0x14,'readS32'],['kindRaw',0x18,'readS32'],
    ['tail',0x1c,'readS32'],['sourceDescriptor',0x26,'readU16'],
    ['destinationDescriptor',0x24,'readU16'],['x',0x30,'readS32'],
    ['y',0x34,'readS32'],['angle',0x40,'readS32'],['size',0x44,'readS32'],['face',0x48,'readS32']];
function watch(address, name, fields) {
    hook(address,{onEnter(){this.obj=this.context.ecx;record(name+'-enter',this,snapshot(this.obj,fields));},
        onLeave(){record(name+'-exit',this,snapshot(this.obj,fields));}});
}
// 0054B030 stores the Windows left/right messages in +05/+04.
const mouseFields = [['rawLeft',5,'readU8'],['rawRight',4,'readU8'],
    ['previousLeft',7,'readU8'],['previousRight',6,'readU8'],
    ['left',0x0d,'readU8'],['right',0x0c,'readU8'],
    ['x',0x14,'readS32'],['y',0x18,'readS32'],['inhibit',0x1c,'readU32']];
const cursorFields = [['enabled',0x10,'readU8'],['request',0x108,'readU32'],
    ['waveEnabled',0x10d,'readU8']];
function watchMouseButtons() {
    const mouseMessages = new Set([0x201,0x202,0x204,0x205]);
    hook(0x0054b030,{onEnter(args){
        const message=args[1].toUInt32();
        if(!mouseMessages.has(message))return;
        const position=args[3].toUInt32();
        record('mouse-button',this,{message,x:(position<<16)>>16,y:position>>16});
    }});
}
function watchCursor() {
    hook(0x0047fe90,{
        onEnter(){
            this.previous=cursorPasses.get(this.threadId);
            cursorPasses.set(this.threadId,++cursorPass);
            const mouse=read(at(0x005f33e0),0,'readPointer');
            record('cursor-pass',this,{...snapshot(this.context.ecx,cursorFields),
                input:mouse===null||mouse.isNull()?null:snapshot(mouse,mouseFields)});
        },
        onLeave(){
            if(this.previous===undefined)cursorPasses.delete(this.threadId);
            else cursorPasses.set(this.threadId,this.previous);
        }
    });
    hook(0x00451f70,{onLeave(result){
        record('cursor-wave-created',this,snapshot(result,effectFields));
    }});
    hook(0x00450fb0,{
        onEnter(){this.obj=this.context.ecx;},
        onLeave(){record('cursor-wave-update',this,snapshot(this.obj,effectFields));}
    });
    hook(0x00451fb0,{onEnter(){
        record('cursor-wave-draw',this,snapshot(this.context.ecx,effectFields));
    }});
}

const attackFields = [['active',4,'readU8'],['state',0x10,'readS32'],['counter',0x14,'readS32'],
    ['attackerSide',0x18,'readS32'],['attackerSlot',0x1c,'readS32'],
    ['targetSide',0x20,'readS32'],['targetSlot',0x24,'readS32']];
function watchBattle() {
    hook(0x005aa480,{onLeave(result){
        const bits=result.toUInt32();
        if(bits!==0)record('duel-confirm-input',this,{bits,
            side:read(at(0x00a55044),0,'readS32'),zone:read(at(0x00a55048),0,'readS32'),
            slot:read(at(0x00a5504c),0,'readS32')});
    }});
    hook(0x00414110,{
        onEnter(){this.obj=this.context.ecx;this.before=snapshot(this.obj,attackFields);},
        onLeave(){
            const after=snapshot(this.obj,attackFields);
            if(this.before.active===1||after.active===1)
                record('battle-attack-update',this,{before:this.before,after});
        }
    });
    hook(0x004142c0,{onEnter(){
        record('battle-attack-draw',this,snapshot(this.context.ecx,attackFields));
    }});
    hook(0x0047c4f0,{onEnter(args){
        record('battle-lp-request',this,{delta:args[0].toInt32(),slot:args[1].toInt32(),flags:args[2].toUInt32()});
    }});
    hook(0x0047c660,{onEnter(){
        record('battle-lp-draw',this,snapshot(this.context.ecx,[['slot',0x2c,'readS32'],['delta',0x28,'readS32']]));
    }});
    hook(0x004514b0,{onEnter(){
        record('battle-explosion-draw',this,snapshot(this.context.ecx,[['active',4,'readU8'],['counter',0x10,'readS32']]));
    }});
    hook(0x005b9470,{
        onEnter(){this.words=commandWords();},
        onLeave(){
            const state=read(at(0x00a558ac),0,'readU16');
            record('battle-command-step',this,{words:this.words,
                substate:state===null?null:state&0x7f, busy:state===null?null:(state&0x4000)!==0});
        }
    });
}

function watchTravelRequests() {
    // The low 12 bits of word 0 carry the command, including when its side bit is set.
    const transferCommands = new Set([0x79,0x7a,0x7c,0x83,0x8d,0x8e,0x8f,0x91,0x95]);
    hook(0x005b9170,{onEnter(args){
        const word=args[0].toUInt32()&0xffff;
        if(transferCommands.has(word&0xfff))
            record('transfer-command-produced',this,{words:[word,...[1,2,3].map(i=>args[i].toUInt32()&0xffff)]});
    }});
    // 83 and 8F pass the source and destination descriptor addresses here.
    hook(0x0040cdb0,{onEnter(args){
        record('travel-request',this,{cardIndex:args[0].toUInt32()&0xffff,
            sourceDescriptor:read(args[1],0,'readU16'),destinationDescriptor:read(args[2],0,'readU16')});
    }});
}
function soundName(id) {
    const bank=id>>>28, index=id&0x0fffffff;
    if ((bank!==1 && bank!==2) || index>=(bank===1?110:14)) return null;
    try {
        const p=at((bank===1?0x005efd48:0x005f0a30)+16*index).readPointer();
        if(va(p)===null) return null;
        return p.readCString(96);
    } catch (_) { return null; }
}
rpc.exports = {
    imagePath(){return image.path;},
    start(profile){
        if(Process.arch!=='ia32') throw new Error('Expected x86 original');
        origin=Date.now();
        hook(0x0044dd00,{onEnter(){this.previous=frames.get(this.threadId);frames.set(this.threadId,++frame);},
            onLeave(){if(this.previous===undefined)frames.delete(this.threadId);else frames.set(this.threadId,this.previous);}});
        hook(0x005c03d0,{onEnter(args){const id=args[0].toUInt32();
            record('sound-request',this,{id,parameter:args[1].toInt32(),resource:soundName(id)});}});
        hook(0x0044ef40,{onEnter(args){record('raw-presentation-event',this,{code:args[0].toUInt32()});}});
        if(profile==='all'||profile==='placement'||profile==='activation'||profile==='battle'){
            watchTravelRequests();
            hook(0x00418b60,{onEnter(){this.obj=this.context.ecx;this.before=snapshot(this.obj,travelFields);},
                onLeave(){const after=snapshot(this.obj,travelFields),b=this.before;
                    if(b.counter!==after.counter||b.tail!==after.tail||b.sourceDescriptor!==after.sourceDescriptor||
                       b.destinationDescriptor!==after.destinationDescriptor||b.face!==after.face)
                        record('travel-update',this,{before:b,after});}});
        }
        if(profile==='all'||profile==='cursor'||profile==='battle')watchMouseButtons();
        if(profile==='all'||profile==='cursor')watchCursor();
        if(profile==='all'||profile==='battle')watchBattle();
        if(profile==='all'||profile==='placement'){
            watch(0x0044f370,'set-update',effectFields);watch(0x0044f390,'set-draw',effectFields);
            watch(0x0044f9f0,'summon-update',effectFields);watch(0x0044fa10,'summon-draw',effectFields);
        }
        if(profile==='all'||profile==='activation'){
            watch(0x00450590,'activation-update',effectFields);watch(0x004505d0,'activation-draw',effectFields);
        }
        if(profile==='all'||profile==='activation'||profile==='reactions'){
            for (const address of [0x00402be0,0x004030f0]){
                hook(address,{onEnter(args){record('reaction-route',this,{selector:address,
                    side:args[0].toUInt32(),resourceId:args[1].toUInt32(),
                    orientation:read(at(0x00a54e5c),0,'readU8')});}});
            }
            hook(0x00402e00,{onEnter(args){const id=args[1].toUInt32();
                record('reaction-request',this,{manifestIndex:args[0].toInt32(),id,resource:soundName(id)});}});
            watch(0x00416bc0,'reaction-draw',reactionFields);
            hook(0x005c0490,{onEnter(args){const caller=va(this.returnAddress);
                this.reaction=caller!==null&&caller>=0x00417970&&caller<=0x00417a0b;
                if(this.reaction)this.id=args[0].toUInt32();
            },onLeave(ret){if(this.reaction)record('reaction-audio-poll',this,{id:this.id,complete:(ret.toUInt32()&255)!==0});}});
        }
    },
    mark(){push({kind:'marker',ms:Date.now()-origin,frame,cursorPass,appTick:read(at(0x005f2fb4),0,'readU32'),
        opcode:currentOpcode(),label:'F9'});},
    stop(){hooks.forEach(h=>h.detach());return {frames:frame,cursorPasses:cursorPass,dropped,events};}
};
'''

def process_image(pid):
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    kernel.QueryFullProcessImageNameW.restype = wintypes.BOOL
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.CloseHandle.restype = wintypes.BOOL
    handle = kernel.OpenProcess(0x1000, False, pid)
    if not handle:
        raise ctypes.WinError(ctypes.get_last_error())
    try:
        buffer = ctypes.create_unicode_buffer(32768)
        size = wintypes.DWORD(len(buffer))
        if not kernel.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(size)):
            raise ctypes.WinError(ctypes.get_last_error())
        return Path(buffer.value), kernel, handle
    except BaseException:
        kernel.CloseHandle(handle)
        raise

def collect_for(seconds, pid, mark):
    # Observe F9 only when the target game is foreground. No key injection
    # or interception. Markers are optional, not game-thread samples.
    user = ctypes.WinDLL("user32", use_last_error=True)
    user.GetForegroundWindow.restype = wintypes.HWND
    user.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    user.GetWindowThreadProcessId.restype = wintypes.DWORD
    user.GetAsyncKeyState.argtypes = [ctypes.c_int]
    user.GetAsyncKeyState.restype = ctypes.c_short
    until = time.monotonic() + seconds
    was_down = False
    while time.monotonic() < until:
        owner = wintypes.DWORD()
        user.GetWindowThreadProcessId(user.GetForegroundWindow(), ctypes.byref(owner))
        down = bool(user.GetAsyncKeyState(0x78) & 0x8000)
        if down and not was_down and owner.value == pid:
            mark()
        was_down = down
        time.sleep(0.04)

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--pid", type=int)
    p.add_argument("--seconds", type=int, default=90)
    p.add_argument("--label", choices=["normal", "speedy"], required=True, help="Label only; does not change speed")
    p.add_argument("--scenario", default="", help="Free-form scenario label")
    p.add_argument("--profile", choices=PROFILES, default="all",
                   help="Limit instrumentation to the selected family (all enables every profile)")
    p.add_argument("--output", type=Path)
    a = p.parse_args()
    if sys.platform != "win32":
        p.error("Run on Windows.")
    if not 5 <= a.seconds <= 180:
        p.error("Use 5–180 seconds.")
    try:
        import frida
    except ImportError:
        p.error("Install Frida: python -m pip install frida")
    device = frida.get_local_device()
    candidates = [q.pid for q in device.enumerate_processes() if q.name.lower() == "joey_pc.exe"]
    if a.pid is None:
        if len(candidates) != 1:
            p.error(f"Expected one joey_pc.exe, found {candidates}; specify --pid.")
        a.pid = candidates[0]
    if a.pid not in candidates:
        p.error("PID is not joey_pc.exe.")
    output = a.output or Path(f"scratch/joey-visual-{a.profile}-{a.label}-{datetime.now():%Y%m%d-%H%M%S}.json")
    if output.exists():
        p.error("Output already exists.")
    exe, kernel, handle = process_image(a.pid)
    session = None
    try:
        digest = hashlib.sha256(exe.read_bytes()).hexdigest()
        if digest != EXPECTED:
            p.error(f"Unexpected fingerprint {digest}; no hooks installed.")
        session = device.attach(a.pid)
        script = session.create_script(AGENT)
        errors = []
        script.on("message", lambda msg, data: errors.append(msg) if msg.get("type") == "error" else None)
        script.load()
        if not Path(script.exports_sync.image_path()).samefile(exe):
            raise RuntimeError("Attached image changed.")
        script.exports_sync.start(a.profile)
        print(f"Gravando {a.seconds}s ({a.profile}). F9 marca o duelo; Ctrl+C encerra.", flush=True)
        try:
            collect_for(a.seconds, a.pid, script.exports_sync.mark)
        except KeyboardInterrupt:
            pass
        result = script.exports_sync.stop()
        output.parent.mkdir(parents=True, exist_ok=True)
        with output.open("x", encoding="utf-8") as out:
            json.dump(dict(schemaVersion=3, exeSha256=digest, modeLabel=a.label,
                profile=a.profile, scenario=a.scenario, pid=a.pid, errors=errors, **result), out, indent=2)
        counts = {}
        for event in result["events"]:
            counts[event["kind"]] = counts.get(event["kind"], 0) + 1
        print(f"Arquivo: {output.resolve()}\nEventos: {counts}\nDescartados: {result['dropped']}; erros: {len(errors)}")
        return int(bool(errors or result["dropped"] or not result["events"]))
    finally:
        try:
            if session is not None:
                session.detach()
        finally:
            kernel.CloseHandle(handle)

if __name__ == "__main__":
    raise SystemExit(main())
