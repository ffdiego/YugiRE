'use strict';
// Only observes natural entries. No game NativeFunction, writes, replacement or RPC actions.
let listeners = [], selected = null, finished = false, armed = false, sequence = 0;
let overflow = false, readErrors = 0, maxEvents = 1024;
const scopes = new Map();
let moduleInfo, base, qpc, frequency;
function va(address) { return base.add(address - 0x400000); }
function ticks() {
  const buffer = Memory.alloc(8);
  if (!qpc(buffer)) throw new Error('QueryPerformanceCounter failed');
  return buffer.readS64().toString();
}
function emit(kind, fields) {
  if (sequence >= maxEvents) { overflow = true; finished = true; return; }
  send(Object.assign({kind, seq: ++sequence, ticks: ticks(), thread: Process.getCurrentThreadId()}, fields));
}
function guard(fn) { try { fn(); } catch (e) { readErrors++; finished = true; emit('read_error', {message: String(e)}); } }
function snapshot(p) {
  const scratch = p.add(0xac);
  return {object: p.toString(), vtable: p.readPointer().sub(base).toString(),
    counter: p.add(0x10).readS32(), active: p.add(4).readU8(),
    x: p.add(0x14).readS32(), y: p.add(0x18).readS32(), monster: p.add(0x1c).readU8(),
    cardWidth: scratch.add(0x3c).readS32(), cardHeight: scratch.add(0x40).readS32()};
}
function imageInfo(p) {
  return {pointer: p.toString(), format: p.add(0x74).readS32(),
    alphaFlag: p.add(0x78).readU8(), colorKey: p.add(0x70).readU32(),
    width: p.add(0x3c).readS32(), height: p.add(0x40).readS32(),
    clip: [0x18,0x1c,0x20,0x24].map(n => p.add(n).readS32())};
}
function intArgs(sp, count) { return Array.from({length:count}, (_,i)=>sp.add(4+i*4).readS32()); }
function attach(address, callbacks) { listeners.push(Interceptor.attach(va(address), callbacks)); }
function isSelected(ctx) { return selected !== null && ctx.ecx.equals(selected); }
rpc.exports = {
  identity() {
    const m = Process.enumerateModules()[0];
    return {path:m.path, base:m.base.toString(), arch:Process.arch, platform:Process.platform};
  },
  arm(limit) {
    if (armed) throw new Error('Already armed');
    if (Process.platform !== 'windows' || Process.arch !== 'ia32') throw new Error('Requires Windows x86');
    if (!Number.isInteger(limit) || limit < 128 || limit > 4096) throw new Error('Invalid event limit');
    maxEvents = limit; moduleInfo = Process.enumerateModules()[0]; base = moduleInfo.base;
    const kernel = Process.getModuleByName('kernel32.dll');
    qpc = new NativeFunction(kernel.getExportByName('QueryPerformanceCounter'), 'int', ['pointer'], 'stdcall');
    const qpf = new NativeFunction(kernel.getExportByName('QueryPerformanceFrequency'), 'int', ['pointer'], 'stdcall');
    const f = Memory.alloc(8);
    if (!qpf(f)) throw new Error('QueryPerformanceFrequency failed');
    frequency = f.readS64().toString();
    try {
      attach(0x450590, {
        onEnter() {
          this.record = false;
          if (finished) return;
          guard(() => {
            const p = this.context.ecx;
            if (selected === null && p.readPointer().equals(va(0x5db2f4)) &&
                p.add(0x10).readS32() === 0 && p.add(0x1c).readU8() === 0) {
              selected = p;
              emit('start', {state:snapshot(p), qpcFrequency:frequency,
                cosineTable:Array.from({length:512},(_,i)=>va(0xa52ee8).add(i*4).readS32())});
            }
            if (!isSelected(this.context)) return;
            this.record = true; this.object = p;
            emit('update_enter', {state:snapshot(p)});
          });
        },
        onLeave() {
          if (!this.record) return;
          guard(() => {
            const state = snapshot(this.object); emit('update_leave', {state});
            if (!state.active) { finished = true; emit('complete', {state}); }
          });
        }
      });
      attach(0x4505d0, {
        onEnter() {
          this.record = false;
          if (finished || !isSelected(this.context)) return;
          guard(() => {
            this.record = true; this.tid = Process.getCurrentThreadId();
            if (scopes.has(this.tid)) throw new Error('Nested selected draw');
            scopes.set(this.tid, true);
            emit('draw_enter', {state:snapshot(selected)});
          });
        },
        onLeave() {
          if (!this.record) return;
          guard(() => emit('draw_leave', {state:snapshot(selected)}));
          scopes.delete(this.tid);
        }
      });
      attach(0x422c00, {onEnter() {
        if (finished || !scopes.has(Process.getCurrentThreadId())) return;
        guard(() => {
          const sp = this.context.esp;
          emit('transform', {args:intArgs(sp,8), source:imageInfo(sp.add(8).readPointer()),
            destination:imageInfo(sp.add(4).readPointer())});
        });
      }});
      attach(0x422e30, {onEnter() {
        if (finished || !scopes.has(Process.getCurrentThreadId())) return;
        guard(() => {
          const sp = this.context.esp, count = sp.add(32).readU32();
          if (count !== 4) throw new Error('Expected four vertices');
          const uv = sp.add(16).readPointer(), xy = sp.add(20).readPointer();
          emit('polygon', {opacity:sp.add(12).readS32(),
            uv:Array.from({length:8},(_,i)=>uv.add(i*4).readS32()),
            xy:Array.from({length:8},(_,i)=>xy.add(i*4).readS32()),
            source:imageInfo(sp.add(8).readPointer()), destination:imageInfo(sp.add(4).readPointer())});
        });
      }});
      attach(0x41ab90, {onEnter() {
        if (finished || !scopes.has(Process.getCurrentThreadId())) return;
        guard(() => {
          const sp = this.context.esp;
          emit('blit', {args:intArgs(sp,7), source:imageInfo(sp.add(4).readPointer()),
            destination:imageInfo(this.context.ecx)});
        });
      }});
      Interceptor.flush(); armed = true;
      emit('armed', {qpcFrequency:frequency});
      return {armed:true};
    } catch (e) { for (const l of listeners) l.detach(); listeners=[]; throw e; }
  },
  status() { return {armed, finished, emitted:sequence, overflow, readErrors, selected:selected !== null}; },
  stop() {
    let detachErrors = 0;
    for (const listener of listeners) { try { listener.detach(); } catch (_) { detachErrors++; } }
    listeners=[]; Interceptor.flush(); armed=false;
    return {finished, emitted:sequence, overflow, readErrors, detached:detachErrors === 0};
  }
};
