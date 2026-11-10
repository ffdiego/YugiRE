// Pure Node Frida ABI simulator: no game, DLL or native function execution.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const pathModule = require('node:path');
const vm = require('node:vm');
const memory = Buffer.alloc(0xb00000);
class Pointer {
  constructor(n) { this.n = Number(n); }
  add(n) { return new Pointer(this.n+n); }
  compare(p) { return Math.sign(this.n-p.n); }
  equals(p) { return this.n === p.n; }
  toString() { return '0x'+this.n.toString(16); }
  toUInt32() { return this.n >>> 0; }
  toInt32() { return this.n | 0; }
  readByteArray(n) { return Uint8Array.from(memory.subarray(this.n,this.n+n)).buffer; }
  readPointer() { return new Pointer(this.readU32()); }
  readU32() { return memory.readUInt32LE(this.n); }
  readUtf8String(n) { return memory.subarray(this.n,this.n+n).toString().split('\0')[0]; }
}
const ptr = n => new Pointer(n);
let thread = 7, installed = new Map(), detached = 0, failAt = 0, failDetach = 0, unreadable = false, events = [];
let splitSource = false, sourceTailUnreadable = false;
const context = {ptr, Map, Array, DataView, Uint8Array, Number, String, Error,
  Process:{platform:'windows',arch:'ia32',getCurrentThreadId:()=>thread,
    findRangeByAddress:p => {
      if (unreadable) return null;
      if (splitSource && p.n >= 0xa55000)
        return {base:ptr(0xa55000),size:memory.length-0xa55000,
          protection:sourceTailUnreadable ? '---' : 'rwx'};
      if (splitSource) return {base:ptr(0),size:0xa55000,protection:'rwx'};
      return {base:ptr(0),size:memory.length,protection:'rwx'};
    }},
  Interceptor:{attach(p,callbacks) {
    if (p.n === failAt) throw new Error('simulated attach failure');
    installed.set(p.n,callbacks);
    return {detach(){ if (p.n === failDetach) throw new Error('simulated detach failure'); installed.delete(p.n); detached++; }};
  }}
};
vm.createContext(context);
vm.runInContext(fs.readFileSync(pathModule.resolve(__dirname,'../deck_trace.js'),'utf8'),context);
const hash = 'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de';
for (const [va,hex] of [[0x44bbe0,'6aff6848305d0064a1000000'],
  [0x44bda5,'33d2b905000000f7f1'],[0x44bc84,'33d2f7f78d04f50000'],
  [0x44bee0,'6aff6868305d0064a1000000'],[0x44a920,'6aff68782f5d0064a1000000']])
  Buffer.from(hex,'hex').copy(memory,va);
const observer = context.createCpuDeckObserver({va:ptr,main:{base:ptr(0x400000),size:0x700000},
  session:()=>1,record:e=>events.push(JSON.parse(JSON.stringify(e)))});
assert.throws(()=>observer.start(false,null),/fingerprint/);
memory[0x44bbe0] = 0;
assert.throws(()=>observer.start(false,hash),/mismatch/);
assert.equal(installed.size,0); memory[0x44bbe0]=0x6a;
failAt=0x44bee0;
assert.throws(()=>observer.start(false,hash),/simulated/);
assert.equal(installed.size,0); assert.equal(detached,3); failAt=0;
unreadable=true;
assert.throws(()=>observer.start(true,hash),/Unreadable/);
assert.equal(installed.size,0); unreadable=false;
splitSource=true; sourceTailUnreadable=true;
assert.throws(()=>observer.start(true,hash),/Unreadable range/);
assert.equal(installed.size,0); // Refuse a gap/unreadable second region.
sourceTailUnreadable=false;
observer.start(true,hash); assert.equal(installed.size,5);
console.log('PASS: source deck crossing two adjacent readable VirtualQuery regions');
function write(n,x) { memory.writeUInt32LE(x,n); }
function object(n,data,path) {
  write(n+4,data); write(n+8,path.length); write(n+12,path.length);
  memory.write(path+'\0',data);
}
function enter(va,caller,esp=0x2000,args=[]) {
  const invocation={returnAddress:ptr(caller),context:{esp:ptr(esp),eax:ptr(0xffffffff),ecx:ptr(0x5555)}};
  installed.get(va).onEnter.call(invocation,args);
  return invocation;
}
const path='data/J/file/CPU_004.ydc';
write(0x2004,0x3000); object(0x3000,0x4000,path);
memory[0xa54db3]=1; write(0x5f2fac,625);
const select = enter(0x44bbe0,0x48ad50);
enter(0x44bda5,0);
installed.get(0x44bbe0).onLeave.call(select,ptr(0x3000));
const scene = enter(0x44bee0,0x48ad71,0x2000,[ptr(0),ptr(0x4000)]);
write(0x2104,1); object(0x2108,0x5000,path);
const load = enter(0x44a920,0x44c0e8,0x2100);
memory.writeUInt16LE(1,0xa54f42); memory.writeUInt16LE(1,0xa5501e);
installed.get(0x44a920).onLeave.call(load,ptr(0));
installed.get(0x44bee0).onLeave.call(scene,ptr(0));
const selected=events.find(e=>e.kind==='deck_selection' && e.phase==='enter');
const loaded=events.find(e=>e.kind==='deck_loader' && e.phase==='leave');
assert.equal(loaded.selectionId,selected.selectionId);
assert.equal(loaded.sourceSlot1.main[0],1);
assert.equal(selected.mtBefore.words.length,624);
assert.equal(events.find(e=>e.phase==='rng').rawRoll,4294967295);
// Another thread with the same path cannot inherit this selection.
thread=8;
const unbound=enter(0x44bee0,0x48ad71,0x2000,[ptr(0),ptr(0x4000)]);
assert.equal(events.at(-1).selectionId,null);
installed.get(0x44bee0).onLeave.call(unbound,ptr(0));
// Bad counts are recorded as errors rather than silently returning an empty deck.
const bad=enter(0x44a920,0x44c0e8,0x2100);
memory.writeUInt16LE(81,0xa5501e);
installed.get(0x44a920).onLeave.call(bad,ptr(0));
assert.match(events.at(-1).readError,/count/);
// Stop between enter and leave: stale callbacks must not leak into a new trace.
const callback=installed.get(0x44bbe0).onLeave;
observer.stop(); const length=events.length;
callback.call(select,ptr(0x3000)); assert.equal(events.length,length);
assert.equal(installed.size,0);
console.log('PASS: deck ABI, uint32 RNG, MT, source, correlation, thread isolation, fail-closed preflight, rollback and stop');

// Demo draws outer first, inner second, then loads inner first. Same filenames
// are deliberately used: correlation must use call frames, never filename alone.
events=[]; memory.writeUInt16LE(0,0xa5501e); observer.start(false,hash); thread=7;
write(0x2004,0x3000); object(0x3000,0x4000,path);
const outer=enter(0x44bbe0,0x48a2c4); enter(0x44bda5,0);
installed.get(0x44bbe0).onLeave.call(outer,ptr(0x3000));
const demo=enter(0x44bee0,0x48a2e6,0x2000,[ptr(7),ptr(0x4000)]);
write(0x2204,0x3200); object(0x3200,0x6000,path);
const inner=enter(0x44bbe0,0x44bf50,0x2200); enter(0x44bda5,0);
installed.get(0x44bbe0).onLeave.call(inner,ptr(0x3200));
for (const caller of [0x44bf7a,0x44bfdd]) {
  const invocation=enter(0x44a920,caller,0x2100);
  installed.get(0x44a920).onLeave.call(invocation,ptr(0));
}
const demoLoads=events.filter(e=>e.kind==='deck_loader' && e.phase==='enter');
assert.equal(demoLoads[0].selectionId,inner.deckId);
assert.equal(demoLoads[1].selectionId,outer.deckId);
assert.notEqual(inner.deckId,outer.deckId);
installed.get(0x44bee0).onLeave.call(demo,ptr(0));
// Reentrant selector frames on the same thread pair RNG with the innermost call.
const first=enter(0x44bbe0,0x48ad50);
const second=enter(0x44bbe0,0x48ad50,0x2200);
enter(0x44bda5,0); assert.equal(events.at(-1).selectionId,second.deckId);
installed.get(0x44bbe0).onLeave.call(second,ptr(0x3200));
enter(0x44bda5,0); assert.equal(events.at(-1).selectionId,first.deckId);
installed.get(0x44bbe0).onLeave.call(first,ptr(0x3000));
// Failed detaches retain handles for retry and prevent a second installation.
failDetach=0x44bbe0;
assert.throws(()=>observer.stop(),/detach failed/);
assert.equal(observer.status().deckHooks,1);
assert.throws(()=>observer.start(false,hash),/detach incomplete/);
failDetach=0; observer.stop(); assert.equal(installed.size,0);
console.log('PASS: mapped-range rejection, reversed Demo loads with identical paths, reentrancy and detach retry');
