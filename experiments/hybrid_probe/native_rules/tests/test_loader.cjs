// Offline deterministic tests for Frida table adapter, without running Joey.
// Run: node tests/test_loader.cjs (from native_rules).
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.resolve(__dirname, '../../yugire_dual_loader.js'), 'utf8');

function fixture(options = {}) {
  const memory = new Map();
  const sparks = 0x005ED0A8 + 66*24;
  const raimei = 0x005ED0A8 + 172*24;
  const original = 0x00584E60;
  const target = 0x00593CC0;
  for(const [row,id] of [[sparks,342],[raimei,1007]]) {
    for(const off of [0,4,8,12,16,20]) memory.set(row + off, 0);
    memory.set(row, id);
    memory.set(row+4, original);
    memory.set(row+20, target);
  }
  const addrExport = new Map();
  let nextAddress = 0x20000000;
  for(const name of [
    'YugiRE_GetAbiVersion','YugiRE_Initialize','YugiRE_Sparks','YugiRE_Raimei',
    'YugiRE_GetCallCount','YugiRE_GetEmitCount','YugiRE_GetConfiguredDamage',
    'YugiRE_GetConfiguredMode','YugiRE_GetDrawBatchCount'
  ]) { addrExport.set(name, nextAddress); nextAddress += 0x100; }
  let config = [0,0];
  let modes = [1,1];
  let failRaimeiWrite = Boolean(options.failRaimeiWrite);
  class Pointer {
    constructor(value, text) { this.n = typeof value === 'string' ? Number(value) : value; this.text = text; }
    add(n) { return new Pointer(this.n + (n instanceof Pointer ? n.n : n)); }
    sub(n) { return new Pointer(this.n - (n instanceof Pointer ? n.n : n)); }
    equals(n) { return this.n === n.n; }
    isNull() { return this.n === 0; }
    toString() { return '0x' + this.n.toString(16); }
    readU32() { return memory.get(this.n) ?? 0; }
    writeU32(value) { memory.set(this.n, value); }
    readPointer() { return new Pointer(this.readU32()); }
    writePointer(value) {
      if(this.n === raimei+4 && value.n !== original && failRaimeiWrite)
        throw new Error('Simulated write fault for Raimei');
      memory.set(this.n, value.n);
    }
  }
  const ptr = (val)=> new Pointer(val);
  let allocIndex=0;
  const context = {
    Process: {
      platform: 'windows', arch: 'ia32', id: 123,
      mainModule:{name:'joey_pc.exe',path:'Z:\\passion\\joey_pc.exe',base:ptr(0x400000)},
      findRangeByAddress(p){ return {protection: (p.n === 0x005784E0 || p.n === 0x00578B60 || p.n >= 0x20000000)
        ? 'r-x' : 'r--'}; },
    },
    Memory: {
      alloc(){ return ptr(0x30000000+(++allocIndex)*4); },
      allocUtf16String(v){ return {text:v}; },
      allocUtf8String(v){ return {text:v}; },
    },
    Module: {getGlobalExportByName(name){return {sys:name};}},
    NativeFunction: function(addr, ret, args, abi){
      assert.ok(['stdcall','mscdecl'].includes(abi));
      if(addr.sys==='LoadLibraryW')return (p)=>{assert.ok(p.text.includes('.dll'));return ptr(0x10000000);};
      if(addr.sys==='GetProcAddress')return (_h,name)=>ptr(addrExport.get(name.text) ?? 0);
      if(addr.sys==='VirtualProtect')return (_p,_size,_pnew,oldProt)=>{oldProt.writeU32(2);return true;};
      const entry=[...addrExport].find(([name,value])=> value===addr.n)?.[0];
      assert.ok(entry, 'Unknown export address '+addr);
      if(entry==='YugiRE_Initialize')return (_base,s,o,sm,rm)=>{
        config=[s,o];modes=[sm,rm];return 0x59554733;
      };
      if(entry==='YugiRE_GetAbiVersion')return ()=>2;
      if(entry==='YugiRE_GetCallCount')return ()=>0;
      if(entry==='YugiRE_GetEmitCount')return ()=>0;
      if(entry==='YugiRE_GetConfiguredDamage')return (id)=> id===342?config[0] : config[1];
      if(entry==='YugiRE_GetConfiguredMode')return (id)=> id===342?modes[0] : modes[1];
      if(entry==='YugiRE_GetDrawBatchCount')return ()=>0;
      throw Error('Should not call callback directly from JS');
    },
    ptr, rpc:{exports:{}},
  };
  vm.runInNewContext(source,context,{filename:'yugire_dual_loader.js'});
  return {api:context.rpc.exports, memory, sparks: sparks+4, raimei: raimei+4,
    original, exports: addrExport, setFault(value){ failRaimeiWrite=Boolean(value); }};
}

const normal=fixture();
assert.equal(normal.api.status().installed,false);
let status=normal.api.install('Z:\\YugiRE\\native_rules\\build-vs2026\\Release\\yugire_rules.dll',300,400,1,1);
assert.equal(status.installed,true);
assert.equal(status.entries[0].configuredDamage,300);
assert.equal(status.entries[1].configuredDamage,400);
assert.equal(status.entries[0].configuredMode,1);
assert.equal(status.entries[1].configuredMode,1);
assert.equal(status.entries[0].drawBatches,0);
assert.equal(status.entries[1].drawBatches,0);
assert.equal(normal.memory.get(normal.sparks),normal.exports.get('YugiRE_Sparks'));
assert.equal(normal.memory.get(normal.raimei),normal.exports.get('YugiRE_Raimei'));
status=normal.api.restore();
assert.equal(status.installed,false);
assert.equal(normal.memory.get(normal.sparks),normal.original);
assert.equal(normal.memory.get(normal.raimei),normal.original);
normal.api.restore(); // idempotent

const failure=fixture({failRaimeiWrite:true});
assert.throws(()=>failure.api.install('Z:\\YugiRE\\rules.dll',300,400,1,1),
  /Installation failed.*original pointers restored/);
assert.equal(failure.memory.get(failure.sparks),failure.original);
assert.equal(failure.memory.get(failure.raimei),failure.original);
assert.equal(failure.api.restore().installed,false);

const wrongId=fixture();
wrongId.memory.set(wrongId.sparks-4,1234);
assert.throws(()=>wrongId.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1),/resource ID mismatch/);
assert.equal(wrongId.memory.get(wrongId.sparks),wrongId.original);

console.log('PASS: dual install, independent configs, both restore, rollback, wrong-row guard');


// Larger configurable amounts for one-hit-KO experimentation.
for (const [sparksDamage, raimeiDamage] of [[1, 1], [8000, 8000], [9999, 9999]]) {
  const large = fixture();
  const installed = large.api.install('Z:\\YugiRE\\rules.dll', sparksDamage, raimeiDamage,1,1);
  assert.equal(installed.entries[0].configuredDamage, sparksDamage);
  assert.equal(installed.entries[1].configuredDamage, raimeiDamage);
  large.api.restore();
  assert.equal(large.memory.get(large.sparks), large.original);
  assert.equal(large.memory.get(large.raimei), large.original);
}
for (const invalid of [-1, 0, 10000, 2.5, '8000', NaN, Infinity, null]) {
  for (const [sparksDamage, raimeiDamage] of [[invalid, 8000], [8000, invalid]]) {
    const rejected = fixture();
    assert.throws(() => rejected.api.install('Z:\\YugiRE\\rules.dll', sparksDamage, raimeiDamage,1,1),
      /Damage must be an integer from 1 to 9999/);
    assert.equal(rejected.memory.get(rejected.sparks), rejected.original);
    assert.equal(rejected.memory.get(rejected.raimei), rejected.original);
  }
}
console.log('PASS: damage bounds 1..9999, OHKO 8000, rejection before DLL load');


// Modes 2/3 are configured independently and preserved in the installed DLL.
const mixed=fixture();
let choice=mixed.api.install('Z:\\YugiRE\\rules.dll',300,400,2,3);
assert.equal(choice.entries[0].configuredMode,2);
assert.equal(choice.entries[1].configuredMode,3);
assert.equal(choice.entries[0].configuredDamage,300);
assert.equal(choice.entries[1].configuredDamage,400);
assert.equal(choice.entries[0].drawBatches,0);
assert.equal(choice.entries[1].drawBatches,0);
choice=mixed.api.restore();
assert.equal(mixed.memory.get(mixed.sparks),mixed.original);
assert.equal(mixed.memory.get(mixed.raimei),mixed.original);
for(const invalid of [-1,0,4,5,2.5,'2',NaN,Infinity,null]){
  for(const [sm,rm] of [[invalid,3],[2,invalid]]){
    const fixtureBad=fixture();
    assert.throws(
      ()=>fixtureBad.api.install('Z:\\YugiRE\\rules.dll',300,400,sm,rm),
      /Modes must be integers 1 \(damage\), 2 \(draw\), or 3 \(damage-draw\)/
    );
    assert.equal(fixtureBad.memory.get(fixtureBad.sparks),fixtureBad.original);
    assert.equal(fixtureBad.memory.get(fixtureBad.raimei),fixtureBad.original);
  }
}
console.log('PASS: ABI 2, independent modes, invalid-mode rejection and restore');
