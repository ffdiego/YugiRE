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

EXPECTED = "db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de"
AGENT = r'''
'use strict';
const image = Process.mainModule, base = image.base;
const hooks = [], events = [], frames = new Map();
const limit = 100000;
let frame = 0, dropped = 0, origin = Date.now();
function at(va) { return base.add(va - 0x400000); }
function va(p) { const n = p.sub(base).toUInt32(); return n < image.size ? n + 0x400000 : null; }
function read(p, offset, kind) { try { return p.add(offset)[kind](); } catch (_) { return null; } }
function push(event) { if (events.length >= limit) { dropped++; return; } events.push(event); }
function record(kind, ctx, fields) {
    push(Object.assign({kind, ms:Date.now()-origin, thread:ctx.threadId,
        frame:frames.get(ctx.threadId)||null, caller:va(ctx.returnAddress),
        scene:read(at(0x005f2fb4),0,'readU32'),
        opcode:read(at(0x00a550a0),0,'readU8')},fields));
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
        if(profile==='all'||profile==='placement'||profile==='activation'){
            hook(0x00418b60,{onEnter(){this.obj=this.context.ecx;this.before=snapshot(this.obj,travelFields);},
                onLeave(){const after=snapshot(this.obj,travelFields),b=this.before;
                    if(b.counter!==after.counter||b.tail!==after.tail||b.sourceDescriptor!==after.sourceDescriptor)
                        record('travel-update',this,{before:b,after});}});
        }
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
    mark(){push({kind:'marker',ms:Date.now()-origin,frame,scene:read(at(0x005f2fb4),0,'readU32'),
        opcode:read(at(0x00a550a0),0,'readU8'),label:'F9'});},
    stop(){hooks.forEach(h=>h.detach());return {frames:frame,dropped,events};}
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
    p.add_argument("--profile", choices=["placement", "activation", "reactions", "all"], default="all")
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
            json.dump(dict(schemaVersion=2, exeSha256=digest, modeLabel=a.label,
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
