// Offline deterministic tests for Frida table adapter, without running Joey.
// Run: node tests/test_loader.cjs (from native_hooks).
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.resolve(__dirname, '../yugire_dual_loader.js'), 'utf8');

function fixture(options = {}) {
  let currentThread = 778;
  const memory = new Map();
  const sparks = 0x005ED0A8 + 66*24;
  const raimei = 0x005ED0A8 + 172*24;
  const original = 0x00584E60;
  const target = 0x00593CC0;
  memory.set(0x00A55D69,20);                 // deck of side 0
  memory.set(0x00A55D69 + 0xD44,20);         // deck of side 1
  memory.set(0x00A55080,0x4001);           // placement context
  memory.set(0x00A55084,0x12345678);       // placement source word
  memory.set(0x00A5508E,0x21);             // placement stage
  memory.set(0x00A577F8,1);                // duel running
  memory.set(0x00A577FA,5);                // main phase 1 (2), side 1
  memory.set(0x00A54E5C,1);                // active side 1
  memory.set(0x00A56430+0xD44,0);          // normal summon unused
  memory.set(0x00A55D68+0xD44,6);          // player hand size
  memory.set(0x00A56434+0xD44+4*4,0x02A8038B);  // index 907 at hand[4]
  memory.set(0x00A55D74,0x120015A2);         // monster 0: owner 1, key 0x12, index 0x5A2
  memory.set(0x00A55D74 + 0xD44 + 3*0x90,0x250000AB); // side 1 slot 3
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
    'YugiRE_GetConfiguredMode','YugiRE_GetDrawBatchCount',
    'YugiRE_ConsoleDraw','YugiRE_GetConsoleDrawBatchCount',
    'YugiRE_ConsoleDamage','YugiRE_GetConsoleDamageCount','YugiRE_ReadDuelState',
    'YugiRE_ConsoleDestroy','YugiRE_GetConsoleDestroyCount'
  ]) { addrExport.set(name, nextAddress); nextAddress += 0x100; }
  let config = [0,0];
  let consoleDrawCalls = 0;
  let damageCalls = 0;
  let destroyCalls = 0;
  let summonBuilderCalls = [];
  let summonFlagCalls = [];
  let summonChecks = [];
  let lastManualCall = null;
  let lastDamageCall = null;
  let lastDestroyCall = null;
  let tickHandler = null;
  let traceHandler = null;
  const prepHandlers = new Map();
  const aiHandlers = new Map();
  const aiAddresses = [0x005B1360,0x005B16C0,0x005B0F40,0x0056DD30,
    0x005B08B0,0x005B1610,0x005B0A50,0x005B0B90,0x005B0950,
    0x005AE3E0,0x005AE220,0x005ADD60,0x005ADDC0,0x0056D790,
    0x0056D8D0,0x005699A0,0x0056B220,0x004022E0,
    0x005ADD8D,0x005ADE2C,0x005ADDF0,0x005B0F7B,0x005B108A,0x005B119B,0x005B11C6,0x005B11CF,0x005B11DD];
  for (const [address,hex] of [[0x005ADD8D,"A802751B0C02"],[0x005ADE2C,"8B4C240C51"],[0x005ADDF0,"668B91745DA500"],[0x005B0F7B,"33C089442440"],[0x005B108A,"0BD9C1E304"],
    [0x005B119B,"BA00004000"],[0x005B11C6,"85148D3064A500"],
    [0x005B11CF,"66816500FFFB"],[0x005B11DD,"8B44243485C0"]])
    Buffer.from(hex,"hex").forEach((b,i)=>memory.set(address+i,b));
  let modes = [1,1];
  let failRaimeiWrite = Boolean(options.failRaimeiWrite);
  class Pointer {
    constructor(value, text) { this.n = typeof value === 'string' ? Number(value) : value; this.text = text; }
    add(n) { return new Pointer(this.n + (n instanceof Pointer ? n.n : n)); }
    sub(n) { return new Pointer(this.n - (n instanceof Pointer ? n.n : n)); }
    equals(n) { return this.n === n.n; }
    isNull() { return this.n === 0; }
    toString() { return '0x' + this.n.toString(16); }
    toUInt32() { return this.n >>> 0; }
    toInt32() { return this.n | 0; }
    compare(p) { return Math.sign(this.n - p.n); }
    readU32() { return memory.get(this.n) ?? 0; }
    readU16() {
      if (options.failLpRead &&
          [0x00A55D64, 0x00A55D64 + 0xD44].includes(this.n))
        throw new Error('simulated unreadable LP memory');
      return this.readU32() & 0xffff;
    }
    readU8() { return this.readU32() & 0xff; }
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
      getCurrentThreadId(){return currentThread;},
      mainModule:{name:'joey_pc.exe',path:'Z:\\passion\\joey_pc.exe',base:ptr(0x400000)},
      findRangeByAddress(p){ return {protection: (p.n === 0x005784E0 || p.n === 0x00578B60 ||
        p.n === 0x005BC700 || p.n === 0x00576960 ||
        p.n === 0x005B9170 ||
        [0x005AAB90,0x005AD740,0x005AB820,0x005ABD00,0x005AC870,
         0x0056A030,0x00569F70,0x00402690,0x004026C0,0x00579930].includes(p.n) ||
        p.n >= 0x20000000 || aiAddresses.includes(p.n))
         ? 'r-x' : 'r--', base:ptr(0x00A579A0), size:0x1000}; },
    },
    Memory: {
      alloc(){ return ptr(0x30000000+(++allocIndex)*4); },
      allocUtf16String(v){ return {text:v}; },
      allocUtf8String(v){ return {text:v}; },
    },
    Module: {getGlobalExportByName(name){return {sys:name};}},
    Interceptor: {
      attach(address, callbacks) {
        if (address.n === 0x005B9170) {
          if (options.failTraceAttach) throw new Error('Simulated trace hook failure');
          assert.equal(traceHandler, null);
          traceHandler = callbacks.onEnter;
          return {detach(){traceHandler=null;}};
        }
        if (aiAddresses.includes(address.n) ||
            (address.n === 0x005AAB90 && aiHandlers.has(0x005B0950))) {
          if (options.failAiHookAt === address.n) throw new Error('Simulated AI detail hook failure');
          assert.equal(aiHandlers.has(address.n),false);
          aiHandlers.set(address.n,callbacks);
          return {detach(){aiHandlers.delete(address.n);}};
        }
        if ([0x005AAB90,0x005AD740,0x005AB820,0x005ABD00,0x005AC870].includes(address.n)) {
          if (options.failPrepAttachAt === address.n) throw new Error('Simulated prep attach fault');
          assert.equal(prepHandlers.has(address.n), false);
          prepHandlers.set(address.n, callbacks);
          return {detach(){prepHandlers.delete(address.n);}};
        }
        assert.equal(address.n, 0x005BC700, 'Hook must be on the duel scheduler');
        if (options.failTickAttach) throw new Error('Simulated scheduler hook failure');
        assert.equal(tickHandler, null);
        tickHandler = callbacks.onLeave;
        return {detach(){tickHandler=null;}};
      }
    },
    NativeFunction: function(addr, ret, args, abi){
      assert.ok(['stdcall','mscdecl'].includes(abi));
      if(addr.sys==='LoadLibraryW')return (p)=>{assert.ok(p.text.includes('.dll'));return ptr(0x10000000);};
      if(addr.sys==='GetProcAddress')return (_h,name)=>ptr(addrExport.get(name.text) ?? 0);
      if(addr.sys==='VirtualProtect')return (_p,_size,_pnew,oldProt)=>{oldProt.writeU32(2);return true;};
      if(addr.n===0x005AAB90)return (side,card)=>{
        summonChecks.push([side,card]);
        return options.eligibilityMask ?? 0x11;
      };
      if(addr.n===0x0056A030)return side=>options.selectedSlot ?? 2;
      if(addr.n===0x00569F70)return (side,slot)=>options.slotLegal===false?0:1;
      if(addr.n===0x00402690)return card=>options.monster===false?0:1;
      if(addr.n===0x004026C0)return card=>options.levelClass ?? 3;
      if(addr.n===0x005AD740)return (side,hand,slot,tribute,mode)=>{
        summonBuilderCalls.push([side,hand,slot,tribute,mode]);
        memory.set(0x00A55080,(mode === 0 ? 0x8000 : 0x4000) |
          (side&1) | (hand<<6) | (slot<<1));
        memory.set(0x00A55084,memory.get(0x00A56434+side*0xD44+hand*4)&0xFFF);
        memory.set(0x00A5508E,5);
      };
      if(addr.n===0x00579930)return (side,bit,value)=>{
        summonFlagCalls.push([side,bit,value]);
        const at=0x00A56430+side*0xD44;
        memory.set(at,(memory.get(at)??0) | ((value?1:0)<<bit));
      };
      const entry=[...addrExport].find(([name,value])=> value===addr.n)?.[0];
      assert.ok(entry, 'Unknown export address '+addr);
      if(entry==='YugiRE_Initialize')return (_base,s,o,sm,rm)=>{
        config=[s,o];modes=[sm,rm];return 0x59554735;
      };
      if(entry==='YugiRE_GetAbiVersion')return ()=>6;
      if(entry==='YugiRE_GetCallCount')return ()=>0;
      if(entry==='YugiRE_GetEmitCount')return ()=>0;
      if(entry==='YugiRE_GetConfiguredDamage')return (id)=> id===342?config[0] : config[1];
      if(entry==='YugiRE_GetConfiguredMode')return (id)=> id===342?modes[0] : modes[1];
      if(entry==='YugiRE_GetDrawBatchCount')return ()=>0;
      if(entry==='YugiRE_ConsoleDraw')return (side,count)=>{
        consoleDrawCalls++;
        lastManualCall={side,count};
        return 1;
      };
      if(entry==='YugiRE_GetConsoleDrawBatchCount')return ()=>consoleDrawCalls;
      if(entry==='YugiRE_ConsoleDamage')return (side,amount)=>{
        damageCalls++;
        lastDamageCall={side,amount};
        return 1;
      };
      if(entry==='YugiRE_GetConsoleDamageCount')return ()=>damageCalls;
      if(entry==='YugiRE_ConsoleDestroy')return (side,slot,expectedWord)=>{
        destroyCalls++;
        lastDestroyCall={side,slot,expectedWord};
        return 1;
      };
      if(entry==='YugiRE_GetConsoleDestroyCount')return ()=>destroyCalls;
      if(entry==='YugiRE_ReadDuelState')return (_base,out)=>{
        const cpu=Array(108).fill(0), player=Array(108).fill(0);
        cpu.splice(0,11,8000,4,27,1,0,0,727,0,0,0,0);
        cpu[11]=342; cpu[16]=1007;
        cpu[22]=2; cpu[27]=3;
        cpu.splice(28,4,323,444,555,666);
        player.splice(0,11,7600,6,22,2,1,0x100,0,503,0,0,0);
        player[11]=501; player[16]=907;
        player.splice(28,6,101,102,103,104,105,106);
        [...cpu,...player].forEach((value,i)=>out.add(i*4).writeU32(value));
        return 1;
      };
      throw Error('Should not call callback directly from JS');
    },
    ptr, rpc:{exports:{}},
  };
  if (options.deckFactory) context.createCpuDeckObserver = options.deckFactory;
  vm.runInNewContext(source,context,{filename:'yugire_dual_loader.js'});
  return {api:context.rpc.exports, memory, sparks: sparks+4, raimei: raimei+4,
    original, exports: addrExport, setFault(value){ failRaimeiWrite=Boolean(value); },
    tick(){ if(tickHandler !== null) tickHandler(); },
    command(a0, a1, a2, a3, caller=0x00578123) {
      if (traceHandler !== null) {
        traceHandler.call({returnAddress:ptr(caller)},
          [a0,a1,a2,a3].map(v=>ptr(v)));
      }
    },
    traceActive(){return traceHandler !== null;},
    prepCount(){return prepHandlers.size;},
    aiHookCount(){return aiHandlers.size;},
    setThread(id){currentThread=id;},
    aiCall(address,args=[],retval=0,body=null,caller=0x005B1377) {
      const callbacks=aiHandlers.get(address);
      if (!callbacks) throw new Error('AI hook not installed at '+address.toString(16));
      const receiver={returnAddress:ptr(caller)};
      callbacks.onEnter.call(receiver,args.map(v=>ptr(v)));
      if(body) body();
      if (callbacks.onLeave) callbacks.onLeave.call(receiver,ptr(retval));
    },
    instruction(address, registers={}) {
      const callbacks=aiHandlers.get(address);
      if (!callbacks) throw new Error('Instruction hook not installed');
      callbacks.onEnter.call({context:Object.fromEntries(Object.entries(registers).map(([k,v])=>[k,ptr(v)]))});
    },
    prepCall(address,args=[],retval=1,beforeLeave=null,caller=0x005B1337) {
      const callback=prepHandlers.get(address);
      if(!callback)return;
      const receiver={returnAddress:ptr(caller)};
      callback.onEnter.call(receiver,args.map(v=>ptr(v)));
      if(beforeLeave)beforeLeave();
      callback.onLeave.call(receiver,ptr(retval));
    },
    consoleCalls(){return consoleDrawCalls;},
    damageCalls(){return damageCalls;},
    destroyCalls(){return destroyCalls;},
    summonBuilders(){return summonBuilderCalls;},
    summonFlags(){return summonFlagCalls;},
    summonEligibilityChecks(){return summonChecks;},
    lastManualCall(){return lastManualCall;},
    lastDamageCall(){return lastDamageCall;},
    lastDestroyCall(){return lastDestroyCall;},
    hookActive(){return tickHandler !== null;} };
}

const normal=fixture();
assert.equal(normal.api.status().installed,false);
let status=normal.api.install('Z:\\YugiRE\\native_hooks\\build-vs2026\\Release\\yugire_rules.dll',300,400,1,1);
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
console.log('PASS: ABI 6, independent modes, invalid-mode rejection and restore');


// Manual draw is staged by RPC, then executed ONLY on the duel scheduler
// callback (simulating the thread-affine Frida Interceptor.onLeave).
const terminal=fixture();
const running=terminal.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
assert.equal(running.console.batchesQueued,0);
assert.equal(terminal.hookActive(),true);
let requested=terminal.api.queuedraw(0,5);
assert.equal(requested.state,'pending');
assert.equal(requested.side,0);
assert.equal(terminal.consoleCalls(),0, 'RPC must not invoke the engine directly');
assert.throws(()=>terminal.api.queuedraw(0,5),/already pending/);
terminal.memory.set(0x00A558A8,1); // queue busy; should not dispatch
terminal.tick();
assert.equal(terminal.consoleCalls(),0);
assert.equal(terminal.api.status().console.pending.id,requested.id);
terminal.memory.set(0x00A558A8,0);
terminal.tick();
assert.equal(terminal.consoleCalls(),1);
assert.equal(terminal.lastManualCall().side,0);
assert.equal(terminal.lastManualCall().count,5);
assert.equal(terminal.api.status().console.last.state,'enqueued');
assert.equal(terminal.api.status().console.batchesQueued,1);
terminal.tick(); // no repeated work
assert.equal(terminal.consoleCalls(),1);
requested=terminal.api.queuedraw(1,5);
terminal.tick();
assert.equal(terminal.consoleCalls(),2);
assert.equal(terminal.lastManualCall().side,1);

for(const [side,count] of [[-1,5],[2,5],['0',5],[0,1],[0,6],[0,'5'],[0,0]]) {
  assert.throws(()=>terminal.api.queuedraw(side,count),/only 5 cards and side 0\/1/);
}
terminal.memory.set(0x00A55D69,4); // avoid console-induced deck-out
terminal.api.queuedraw(0,5);
terminal.tick();
assert.equal(terminal.consoleCalls(),2);
assert.equal(terminal.api.status().console.last.state,'rejected');
terminal.memory.set(0x00A55D69,20);
terminal.api.queuedraw(0,5);
assert.equal(terminal.api.canceldraw().state,'cancelled');
terminal.tick();
assert.equal(terminal.consoleCalls(),2,'Cancelled request must not dispatch');
terminal.api.queuedraw(0,5);
terminal.api.restore();
assert.equal(terminal.hookActive(),false);
terminal.tick();
assert.equal(terminal.consoleCalls(),2,'Restored/unhooked process must not dispatch');
assert.equal(terminal.memory.get(terminal.sparks),terminal.original);
assert.equal(terminal.memory.get(terminal.raimei),terminal.original);

const hookFailure=fixture({failTickAttach:true});
assert.throws(()=>hookFailure.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1),
  /Installation failed.*original pointers restored/);
assert.equal(hookFailure.memory.get(hookFailure.sparks),hookFailure.original);
assert.equal(hookFailure.memory.get(hookFailure.raimei),hookFailure.original);
assert.equal(hookFailure.hookActive(),false);
console.log('PASS: console draw is scheduled on idle duel hook, cancellation, restore, attach rollback');


// New native damage command shares the same safe, game-thread-only
// dispatcher queue as draw. Read-only snapshots inspect known offsets.
const lab=fixture();
assert.throws(()=>lab.api.duelstate(),/requires an installed patch/);
lab.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
const snapshot=lab.api.duelstate();
assert.equal(snapshot.sides.length,2);
assert.equal(snapshot.sides[0].lifePoints,8000);
assert.equal(snapshot.sides[0].deckCount,27);
assert.equal(snapshot.sides[0].monsterIndices[0],727);
assert.equal(snapshot.sides[1].lifePoints,7600);
assert.equal(snapshot.sides[1].handCount,6);
assert.equal(snapshot.sides[1].terminalFlags,0x100);
assert.equal(snapshot.sides[1].monsterIndices[1],503);
assert.equal(snapshot.sides[0].otherFieldIndices[0],342);
assert.equal(snapshot.sides[0].otherFieldIndices[5],1007);
assert.equal(snapshot.sides[0].fieldPositionWords[5],2);
assert.equal(snapshot.sides[0].fieldPositionWords[10],3);
assert.equal(snapshot.sides[0].handIndices.length,4);
assert.equal(snapshot.sides[1].handIndices.length,6);
assert.equal(snapshot.sides[1].handIndices[0],101);

const queuedDamage=lab.api.queuedamage(0,1200);
assert.equal(queuedDamage.kind,'damage');
assert.equal(queuedDamage.side,0);
assert.equal(queuedDamage.amount,1200);
assert.equal(lab.damageCalls(),0,'RPC must NOT call native damage directly');
assert.throws(()=>lab.api.queuedraw(1,5),/already pending/);
lab.memory.set(0x00A558A8,1); // native queue busy
lab.tick();
assert.equal(lab.damageCalls(),0);
lab.memory.set(0x00A558A8,0);
lab.tick();
assert.equal(lab.damageCalls(),1);
assert.equal(lab.lastDamageCall().side,0);
assert.equal(lab.lastDamageCall().amount,1200);
assert.equal(lab.api.status().console.damageCommandsQueued,1);
assert.equal(lab.api.status().console.last.state,'enqueued');
lab.tick();
assert.equal(lab.damageCalls(),1);
for (const [side,amount] of [[-1,1],[2,1],['0',1],[0,0],[0,10000],[0,1.5],[0,'100']]) {
  assert.throws(()=>lab.api.queuedamage(side,amount),/explicit target side required/);
}
lab.api.queuedamage(1,300);
assert.equal(lab.api.canceldraw().state,'cancelled');
lab.tick();
assert.equal(lab.damageCalls(),1);
lab.api.queuedamage(1,500);
lab.api.restore();
assert.equal(lab.hookActive(),false);
lab.tick();
assert.equal(lab.damageCalls(),1);
console.log('PASS: native duel snapshot, damage scheduling, input limits, cancel and restore');


// Native destruction: RPC only stages. Identity changes or an empty slot
// while waiting MUST reject rather than hitting a replacement monster.
const destruction=fixture();
destruction.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
assert.throws(()=>destruction.api.queuedestroy(0,1),/slot is empty/);
for(const [side,slot] of [[-1,0],[2,0],['0',0],[0,-1],[0,5],[0,'0'],[0,0.5]]) {
  assert.throws(()=>destruction.api.queuedestroy(side,slot),/explicit side and monster slot/);
}
const destroyReq=destruction.api.queuedestroy(0,0);
assert.equal(destroyReq.expectedIndex,0x5A2);
assert.equal(destroyReq.state,'pending');
assert.equal(destruction.destroyCalls(),0);
assert.throws(()=>destruction.api.queuedamage(0,100),/already pending/);
destruction.memory.set(0x00A558A8,1); destruction.tick();
assert.equal(destruction.destroyCalls(),0);
destruction.memory.set(0x00A558A8,0); destruction.tick();
assert.equal(destruction.destroyCalls(),1);
assert.equal(destruction.lastDestroyCall().side,0);
assert.equal(destruction.lastDestroyCall().slot,0);
assert.equal(destruction.lastDestroyCall().expectedWord,0x120015A2);
assert.equal(destruction.api.status().console.last.state,'submitted');
assert.equal(destruction.api.status().console.destroyAttempts,1);
destruction.tick();
assert.equal(destruction.destroyCalls(),1);
destruction.api.queuedestroy(1,3);
destruction.memory.set(0x00A55D74 + 0xD44 + 3*0x90,0x260000AB); // replaced key
destruction.tick();
assert.equal(destruction.api.status().console.last.state,'rejected');
assert.equal(destruction.destroyCalls(),1);
destruction.api.queuedestroy(0,0);
assert.equal(destruction.api.canceldraw().state,'cancelled');
destruction.tick();
assert.equal(destruction.destroyCalls(),1);
destruction.api.queuedestroy(0,0);
destruction.api.restore();
destruction.tick();
assert.equal(destruction.destroyCalls(),1);
console.log('PASS: destruction staged on duel thread, identity recheck, validation and cancel');


// Passive producer trace: captures raw args, stable caller/thread, markers,
// bounded history, pagination, failures and teardown without changing inputs.
const traceFixture=fixture();
assert.throws(()=>traceFixture.api.tracestart(16),/requires installed native adapter/);
traceFixture.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
assert.equal(traceFixture.api.tracestatus().active,false);
for (const cap of [0,15,20001,-3,1.5,'64',null]) {
  assert.throws(()=>traceFixture.api.tracestart(cap),/capacity must be an integer/);
}
const firstTrace=traceFixture.api.tracestart(16);
assert.equal(firstTrace.active,true);
assert.equal(firstTrace.session,1);
assert.equal(firstTrace.hasUnsaved,false);
assert.equal(traceFixture.traceActive(),true);
assert.throws(()=>traceFixture.api.tracestart(16),/already active/);
assert.throws(()=>traceFixture.api.traceevents(0,10),/Stop trace before exporting/);
for(let i=0;i<20;i++){
  traceFixture.command(i%2===0?0x8079:0x00CE,i,2,3);
}
traceFixture.api.tracemark('Summon normal');
let summary=traceFixture.api.tracestatus();
assert.equal(summary.seen,21);
assert.equal(summary.retained,16);
assert.equal(summary.overwritten,5);
assert.equal(summary.firstSeq,6);
assert.equal(summary.hasUnsaved,true);
assert.equal(traceFixture.api.tracestop().active,false);
assert.equal(traceFixture.api.tracestop().active,false);
assert.throws(()=>traceFixture.api.traceacknowledge(1,20),/Trace changed/);
const ev1=traceFixture.api.traceevents(0,8);
const ev2=traceFixture.api.traceevents(8,8);
assert.equal(ev1.length,8);
assert.equal(ev2.length,8);
assert.equal(ev1[0].seq,6);
assert.equal(ev1[0].args[0],0xCE);
assert.equal(ev1[0].threadId,778);
assert.equal(ev1[0].caller,'0x578123');
assert.equal(ev1[0].ceNativeState,undefined); // only CE callsite reads globals
assert.equal(ev2[7].kind,'marker');
assert.equal(ev2[7].label,'Summon normal');
assert.equal(traceFixture.api.traceevents(16,8).length,0);
assert.equal(traceFixture.api.traceacknowledge(1,21).hasUnsaved,false);
for(const [offset,count] of [[-1,1],[0,0],[0,257],['0',2]]){
 assert.throws(()=>traceFixture.api.traceevents(offset,count),/Trace pagination/);
}
traceFixture.command(0x61,1,0,0);
assert.equal(traceFixture.api.tracestatus().seen,21,'Stopped hook must not capture');
traceFixture.api.tracestart(16);
assert.equal(traceFixture.api.tracestatus().session,2);
assert.equal(traceFixture.api.tracestatus().seen,0);
assert.equal(traceFixture.api.tracestatus().hasUnsaved,false);
traceFixture.command(0x61,0,5,0);
assert.equal(traceFixture.api.tracestatus().hasUnsaved,true);
traceFixture.api.restore();
assert.equal(traceFixture.traceActive(),false);
assert.equal(traceFixture.api.tracestatus().seen,1);
assert.equal(traceFixture.api.traceevents(0,20).length,1);
// Validate that the CE-only snapshot captures full, unshifted native state.
const capturedCe=fixture();
capturedCe.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
capturedCe.api.tracestart(16);
capturedCe.memory.set(0x00A55080,0x8101);
capturedCe.memory.set(0x00A55084,0xD10419);
capturedCe.memory.set(0x00A5508E,0x41);
capturedCe.command(0x408080CE,0x00D10419,0,0,0x005ABEAB);
capturedCe.command(0x408080CE,0x00D10419,0,0,0x00579984);
capturedCe.api.tracestop();
const ceEvents=capturedCe.api.traceevents(0,5);
assert.equal(ceEvents.length,2);
assert.equal(ceEvents[0].ceNativeState.context,0x8101);
assert.equal(ceEvents[0].ceNativeState.source,0xD10419);
assert.equal(ceEvents[0].ceNativeState.stage,0x41);
assert.equal(ceEvents[1].ceNativeState,undefined);
capturedCe.api.restore();
const failedTrace=fixture({failTraceAttach:true});
failedTrace.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
assert.throws(()=>failedTrace.api.tracestart(16),/Simulated trace hook failure/);
assert.equal(failedTrace.api.tracestatus().active,false);
assert.equal(failedTrace.hookActive(),true);
failedTrace.api.restore();
console.log('PASS: bounded passive command trace, marker, export pagination, hook rollback and teardown');


// Observe actual native LP at producer entry, not a predicted post-damage
// value. An LP change is recorded on the NEXT producer call after resolution.
const lpFixture=fixture();
lpFixture.api.installobserver();
lpFixture.memory.set(0x00A55D64,8000);
lpFixture.memory.set(0x00A55D64 + 0xD44,8000);
lpFixture.api.tracestart(64);
lpFixture.command(0x61,0,5,0); // First sample of an active duel.
lpFixture.command(0x61,1,1,0); // No change: no repeated LP payload.
lpFixture.command(0x43,1900,0,1); // Queued damage: LP not yet changed.
lpFixture.memory.set(0x00A55D64,6100); // Simulate native resolution.
lpFixture.command(0x52,0,0,0); // Next command observes the applied damage.
lpFixture.memory.set(0x00A55D64 + 0xD44,7200); // Simulated LP change on side 1.
lpFixture.command(0x61,0,1,0);
lpFixture.command(0x8043,500,0,1); // Queue another change to side 1.
lpFixture.memory.set(0x00A55D64 + 0xD44,0); // Damage can be lethal.
lpFixture.command(0x8004,0,0,0,0x005BC545);
lpFixture.command(0x61,0,1,0); // Terminal reset must mark a new baseline.
lpFixture.api.tracestop();
const lpEvents=lpFixture.api.traceevents(0,16);
assert.equal(lpEvents.length,8);
assert.deepEqual(Array.from(lpEvents[0].lifePointsObserved.lifePoints),[8000,8000]);
assert.equal(lpEvents[0].lifePointsObserved.reason,'first_active_sample');
assert.equal(lpEvents[0].lifePointsObserved.samplePoint,'command_producer_onEnter');
assert.equal(lpEvents[1].lifePointsObserved,undefined);
assert.deepEqual(Array.from(lpEvents[2].lifePointsObserved.lifePoints),[8000,8000]);
assert.equal(lpEvents[2].lifePointsObserved.reason,'lp_command');
assert.deepEqual(Array.from(lpEvents[3].lifePointsObserved.lifePoints),[6100,8000]);
assert.equal(lpEvents[3].lifePointsObserved.reason,'changed');
assert.deepEqual(Array.from(lpEvents[4].lifePointsObserved.lifePoints),[6100,7200]);
assert.deepEqual(Array.from(lpEvents[5].lifePointsObserved.lifePoints),[6100,7200]);
assert.equal(lpEvents[5].lifePointsObserved.reason,'lp_command');
assert.deepEqual(Array.from(lpEvents[6].lifePointsObserved.lifePoints),[6100,0]);
assert.equal(lpEvents[6].lifePointsObserved.reason,'terminal');
assert.deepEqual(Array.from(lpEvents[6].terminalNativeState.lifePoints),[6100,0]);
assert.equal(lpEvents[7].lifePointsObserved.reason,'first_active_sample');
lpFixture.api.restore();

const lpNoDuel=fixture();
lpNoDuel.api.installobserver();
lpNoDuel.api.tracestart(16);
lpNoDuel.memory.set(0x00A577F8,0);
lpNoDuel.command(0x61,1,0,0); // No stale/menu sample.
lpNoDuel.command(0x43,100,0,0); // Forced opcode retains runningWord=0.
lpNoDuel.api.tracestop();
const idleLp=lpNoDuel.api.traceevents(0,3);
assert.equal(idleLp[0].lifePointsObserved,undefined);
assert.equal(idleLp[1].lifePointsObserved.duelRunningWord,0);
lpNoDuel.api.restore();

const badLp=fixture({failLpRead:true});
badLp.api.installobserver();
badLp.api.tracestart(16);
badLp.command(0x43,100,0,0);
badLp.command(0x8004,0,0,0,0x005BC545);
badLp.api.tracestop();
const badLpEvents=badLp.api.traceevents(0,3);
assert.match(badLpEvents[0].lifePointsReadError,/unreadable LP/);
assert.equal(badLpEvents[0].lifePointsObserved,undefined);
assert.equal(badLpEvents[1].terminalNativeState.lifePoints,null);
assert.match(badLpEvents[1].lifePointsReadError,/unreadable LP/);
badLp.api.restore();
console.log('PASS: passive LP transitions, enqueue timing, terminal, reset, idle and safe read failure');


// Passive high-level probe: eligibility arguments, mode 1 filter, per-call
// entry/leave state, and complete rollback even if the third hook fails.
const prepFixture=fixture();
assert.throws(()=>prepFixture.api.traceprepstart(),/Start trace on/);
prepFixture.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
assert.throws(()=>prepFixture.api.traceprepstart(),/Start trace on/);
prepFixture.api.tracestart(64);
let prepStatus=prepFixture.api.traceprepstart();
assert.equal(prepStatus.prepActive,true);
assert.equal(prepStatus.prepHooks,5);
assert.equal(prepFixture.prepCount(),5);
assert.throws(()=>prepFixture.api.traceprepstart(),/already active/);
prepFixture.prepCall(0x005AAB90,[1,705],0x11);
prepFixture.prepCall(0x005AD740,[1,4,1,0,1],0,()=>{
  prepFixture.memory.set(0x00A55080,0x4103);
  prepFixture.memory.set(0x00A55084,705);
  prepFixture.memory.set(0x00A5508E,5);
  prepFixture.memory.set(0x00A5508C,2);
});
let prepEvents=prepFixture.api.traceevents; // not permitted while trace active
assert.throws(()=>prepEvents(0,1),/Stop trace/);
// Dispatcher idle should not create events.
prepFixture.memory.set(0x00A5508E,0);
prepFixture.prepCall(0x005AB820,[],0);
prepFixture.memory.set(0x00A5508E,0x25);
prepFixture.memory.set(0x00A55080,0x4103);
prepFixture.memory.set(0x00A55084,705);
prepFixture.prepCall(0x005AB820,[],1,()=> {
  prepFixture.memory.set(0x00A5508E,0x45);
});
prepFixture.prepCall(0x005ABD00,[],0);
assert.equal(prepFixture.api.tracestop().prepActive,false);
assert.equal(prepFixture.prepCount(),0);
assert.equal(prepFixture.traceActive(),false);
prepEvents=prepFixture.api.traceevents(0,20);
assert.equal(prepEvents.length,8); // 4 function invocations, paired enter/leave
assert.equal(prepEvents[0].function,'eligibility');
assert.equal(prepEvents[0].argsRaw[0],1);
assert.equal(prepEvents[0].argsRaw[1],705);
assert.equal(prepEvents[1].returnRaw,0x11);
assert.equal(prepEvents[0].callId,prepEvents[1].callId);
assert.equal(prepEvents[2].function,'normal_builder');
assert.equal(prepEvents[2].argsRaw.length,5);
assert.equal(prepEvents[2].builderIntent.side,1);
assert.equal(prepEvents[2].builderIntent.handIndex,4);
assert.equal(prepEvents[2].builderIntent.fieldSlot,1);
assert.equal(prepEvents[2].builderIntent.tributeData,0);
assert.equal(prepEvents[2].builderIntent.modeCandidate,'summon');
assert.equal(prepEvents[2].state.context,0x4001); // previous descriptor
assert.equal(prepEvents[3].state.context,0x4103);
assert.equal(prepEvents[3].state.source,705);
assert.equal(prepEvents[3].state.stage,5);
assert.equal(prepEvents[3].state.auxFlags,2);
assert.equal(prepEvents[4].function,'dispatcher');
assert.equal(prepEvents[4].state.context,0x4103);
assert.equal(prepEvents[4].state.source,705);
assert.equal(prepEvents[4].state.stage,0x25);
assert.equal(prepEvents[5].state.stage,0x45);
assert.equal(prepEvents[6].function,'placement');
assert.equal(prepEvents[7].phase,'leave');
assert.equal(prepFixture.api.tracestatus().retained,8);
// The builder also covers set, including a slot destination other than 0.
prepFixture.api.tracestart(32);
prepFixture.api.traceprepstart();
prepFixture.prepCall(0x005AD740,[1,0,1,0,0],0,()=>{
  prepFixture.memory.set(0x00A55080,0x8003);
  prepFixture.memory.set(0x00A55084,261);
  prepFixture.memory.set(0x00A5508E,5);
});
prepFixture.api.tracestop();
const setBuilder=prepFixture.api.traceevents(0,8);
assert.equal(setBuilder.length,2);
assert.equal(setBuilder[0].builderIntent.modeCandidate,'set');
assert.equal(setBuilder[0].builderIntent.handIndex,0);
assert.equal(setBuilder[0].builderIntent.fieldSlot,1);
assert.equal(setBuilder[1].state.context,0x8003);
// Builder with invalid slot must be only observed, never executed/queued.
prepFixture.api.tracestart(32);
prepFixture.api.traceprepstart();
prepFixture.prepCall(0x005AD740,[1,0,0xFFFFFFFF,0,0],0);
prepFixture.api.tracestop();
assert.equal(prepFixture.api.traceevents(0,2)[0].builderIntent.fieldSlotValid,false);
// Verify explicitly turning prep off preserves plain command tracing.
prepFixture.api.tracestart(32);
prepFixture.api.traceprepstart();
assert.equal(prepFixture.api.traceprepstop().prepActive,false);
assert.equal(prepFixture.traceActive(),true);
prepFixture.command(0xCE,0x123,0,0);
prepFixture.api.restore();
assert.equal(prepFixture.prepCount(),0);
assert.equal(prepFixture.api.traceevents(0,10).length,1);
const brokenPrep=fixture({failPrepAttachAt:0x005ABD00});
brokenPrep.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
brokenPrep.api.tracestart(32);
assert.throws(()=>brokenPrep.api.traceprepstart(),/hooks rolled back/);
assert.equal(brokenPrep.prepCount(),0);
assert.equal(brokenPrep.traceActive(),true);
brokenPrep.api.tracestop();
brokenPrep.api.restore();
console.log('PASS: passive summon preparation probe, paired calls, filtering and rollback');


// Ground Collapse regression: the UI chooses constrained slots 2 and 3
// BEFORE calling the native descriptor builder (same player callsite).
for (const scenario of [
  {label:'summon',args:[1,5,2,0,1],context:0x4145,source:907,resource:174,
   raw:0x20A280CE,hand:5,slot:2},
  {label:'set',args:[1,4,3,0,0],context:0x8107,source:273,resource:1049,
   raw:0x408380CE,hand:4,slot:3},
]) {
  const f=fixture();
  f.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
  f.api.tracestart(32);
  f.api.traceprepstart();
  f.prepCall(0x005AD740,scenario.args,0,()=>{
    f.memory.set(0x00A55080,scenario.context);
    f.memory.set(0x00A55084,scenario.source);
    f.memory.set(0x00A5508E,5);
  },0x0059E208);
  f.command(scenario.raw,scenario.resource,0,0,0x005ABEAB);
  f.api.tracestop();
  const events=f.api.traceevents(0,10);
  assert.equal(events.length,3);
  assert.equal(events[0].caller,'0x59e208');
  assert.equal(events[0].builderIntent.modeCandidate,scenario.label);
  assert.equal(events[0].builderIntent.handIndex,scenario.hand);
  assert.equal(events[0].builderIntent.fieldSlot,scenario.slot);
  assert.equal(events[0].builderIntent.fieldSlotValid,true);
  assert.equal(events[1].state.context,scenario.context);
  assert.equal(events[1].state.source,scenario.source);
  assert.equal(events[2].ceNativeState.context,scenario.context);
  assert.equal((events[2].ceNativeState.context>>1)&0x1f,scenario.slot);
  f.api.restore();
}
console.log('PASS: Ground Collapse observed builder calls retain available monster slots 2 and 3');


// New normal summon: an explicitly opt-in request, validated and executed
// only inside the duel tick hook. Ghidra evidence supports levels 1..4,
// side 1, phase 2/4, slot availability and normal-summon flag bit 17.
const summon = fixture();
summon.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
summon.memory.set(0x00A5508E,0); // no placement pending
assert.throws(()=>summon.api.queuesummon(0,4,false),/player side only/);
assert.throws(()=>summon.api.queuesummon(1,-1,false),/player side only/);
assert.throws(()=>summon.api.queuesummon(1,80,false),/player side only/);
assert.throws(()=>summon.api.queuesummon(1,6,false),/not present/);
const checkReq=summon.api.queuesummon(1,4,true);
assert.equal(checkReq.state,'pending');
assert.equal(checkReq.expectedIndex,907);
assert.equal(summon.summonBuilders().length,0);
summon.memory.set(0x00A558A8,1); // queue busy -> cannot execute check
summon.tick();
assert.equal(summon.api.status().console.pending.id,checkReq.id);
summon.memory.set(0x00A558A8,0);
summon.tick();
let result=summon.api.status().console.last;
assert.equal(result.state,'checked');
assert.equal(result.allowed,true);
assert.equal(result.selectedSlot,2);
assert.equal(result.levelClass,3);
assert.equal(result.eligibility,0x11);
assert.equal(summon.summonBuilders().length,0,'check cannot invoke builder');
assert.equal(summon.summonFlags().length,0,'check cannot consume summon');
const summonReq=summon.api.queuesummon(1,4,false);
summon.tick();
result=summon.api.status().console.last;
assert.equal(result.state,'submitted');
assert.equal(result.allowed,true);
assert.equal(summon.summonBuilders().length,1);
assert.deepEqual(Array.from(summon.summonBuilders()[0]),[1,4,2,0,1]);
assert.equal(summon.memory.get(0x00A55080),0x4105);
assert.equal(summon.memory.get(0x00A5508E),5);
assert.deepEqual(Array.from(summon.summonFlags()[0]),[1,0x11,1]);
assert.ok((summon.memory.get(0x00A56430+0xD44)&0x20000)!==0);
// A second queued summon is rejected when the bit 17 marker is already set.
summon.memory.set(0x00A5508E,0);
summon.api.queuesummon(1,4,false);
summon.tick();
assert.equal(summon.api.status().console.last.state,'rejected');
assert.match(summon.api.status().console.last.detail,/already used/);
assert.equal(summon.summonBuilders().length,1);
summon.api.restore();
console.log('PASS: guarded summon check and builder scheduling on duel update thread');

function summonFailure(config,edit,expected) {
  const f=fixture(config);
  f.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
  f.memory.set(0x00A5508E,0);
  f.api.queuesummon(1,4,false);
  // Simulate an asynchronous change AFTER the RPC request is staged.
  if(edit)edit(f);
  f.tick();
  assert.equal(f.api.status().console.last.state,'rejected');
  assert.match(f.api.status().console.last.detail,expected);
  assert.equal(f.summonBuilders().length,0);
  assert.equal(f.summonFlags().length,0);
  f.api.restore();
}
summonFailure({},f=>f.memory.set(0x00A577F8,0),/No active duel/);
summonFailure({},f=>f.memory.set(0x00A54E5C,0),/player turn/);
summonFailure({},f=>f.memory.set(0x00A577FA,7),/main phase/);
summonFailure({},f=>f.memory.set(0x00A56430+0xD44,0x20000),/already used/);
summonFailure({monster:false},null,/not a monster/);
summonFailure({levelClass:5},null,/level 1..4/);
summonFailure({eligibilityMask:0x10},null,/Engine rejects/);
summonFailure({selectedSlot:0xFFFFFFFF},null,/No permitted/);
summonFailure({slotLegal:false},null,/no longer permitted/);
summonFailure({},f=>f.memory.set(0x00A55D68+0xD44,5),/Hand size/);
summonFailure({},f=>f.memory.set(0x00A56434+0xD44+4*4,0x02A8038A),/card changed/);
const pendingPlacement=fixture();
pendingPlacement.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
pendingPlacement.api.queuesummon(1,4,false);
pendingPlacement.tick();
assert.equal(pendingPlacement.api.status().console.pending.kind,'summon');
assert.equal(pendingPlacement.summonBuilders().length,0);
pendingPlacement.memory.set(0x00A5508E,0);
pendingPlacement.api.canceldraw();
pendingPlacement.tick();
assert.equal(pendingPlacement.summonBuilders().length,0);
pendingPlacement.api.restore();
console.log('PASS: summon preflight fail-closed rules, stale-hand rejection and pending cancellation');


// Offline normal monster set: separate legality bit 0x10 and mode=0.
function setFixture(options={}) {
  const f=fixture(options);
  f.api.install('Z:\\YugiRE\\rules.dll',200,300,1,1);
  f.memory.set(0x00A5508E,0);
  return f;
}
const setAction=setFixture({eligibilityMask:0x10,selectedSlot:3});
// Test fixture contains a monster at side 1 slot 3 by default; clear it.
setAction.memory.set(0x00A55D74+0xD44+3*0x90,0);
assert.throws(()=>setAction.api.queueset(0,4),/player side only/);
assert.throws(()=>setAction.api.queueset(1,-1),/player side only/);
assert.throws(()=>setAction.api.queueset(1,80),/player side only/);
assert.throws(()=>setAction.api.queueset(1,6),/not present/);
setAction.api.queueset(1,4,true);
setAction.memory.set(0x00A558A8,1);
setAction.tick();
assert.equal(setAction.api.status().console.pending.kind,'set');
setAction.memory.set(0x00A558A8,0);
setAction.tick();
let setResult=setAction.api.status().console.last;
assert.equal(setResult.state,'checked');
assert.equal(setResult.allowed,true);
assert.equal(setResult.selectedSlot,3);
assert.equal(setResult.eligibility,0x10);
assert.equal(setResult.eligibilityMask,0x10);
assert.equal(setAction.summonBuilders().length,0);
assert.equal(setAction.summonFlags().length,0);
setAction.api.queueset(1,4,false);
setAction.tick();
setResult=setAction.api.status().console.last;
assert.equal(setResult.state,'submitted');
assert.equal(setResult.mode,0);
assert.equal(setResult.kind,'set');
assert.deepEqual(Array.from(setAction.summonBuilders()[0]),[1,4,3,0,0]);
assert.equal(setAction.memory.get(0x00A55080),0x8107);
assert.deepEqual(Array.from(setAction.summonFlags()[0]),[1,0x11,1]);
// Set consumes the summon opportunity for the entire turn.
setAction.memory.set(0x00A5508E,0);
setAction.api.queuesummon(1,4,false);
setAction.tick();
setResult=setAction.api.status().console.last;
assert.equal(setResult.state,'rejected');
assert.equal(setResult.normalSummonUsed,true);
assert.equal(setResult.playerFlags & 0x20000,0x20000);
assert.match(setResult.detail,/flag 0x20000/);
assert.equal(setAction.summonBuilders().length,1);
setAction.api.restore();
console.log('PASS: face-down set uses eligibility 0x10, mode 0 and common turn flag');

// The authorization bits are intentionally NOT interchangeable.
const summonOnly=setFixture({eligibilityMask:0x01});
summonOnly.api.queueset(1,4,false);
summonOnly.tick();
assert.equal(summonOnly.api.status().console.last.state,'rejected');
assert.equal(summonOnly.api.status().console.last.eligibilityMask,0x10);
assert.match(summonOnly.api.status().console.last.detail,/face-down set/);
assert.equal(summonOnly.summonBuilders().length,0);
summonOnly.api.restore();
const setOnly=setFixture({eligibilityMask:0x10});
setOnly.api.queuesummon(1,4,false);
setOnly.tick();
assert.equal(setOnly.api.status().console.last.state,'rejected');
assert.equal(setOnly.summonBuilders().length,0);
setOnly.api.restore();

const postSummon=setFixture();
postSummon.api.queuesummon(1,4,false);
postSummon.tick();
postSummon.memory.set(0x00A5508E,0);
postSummon.api.queueset(1,4,false);
postSummon.tick();
assert.equal(postSummon.api.status().console.last.state,'rejected');
assert.equal(postSummon.api.status().console.last.normalSummonUsed,true);
assert.equal(postSummon.summonBuilders().length,1);
postSummon.api.restore();

function setFailure(options,afterRequest,expectedReason) {
  const f=setFixture(options);
  f.api.queueset(1,4,false);
  if(afterRequest)afterRequest(f);
  f.tick();
  assert.equal(f.api.status().console.last.state,'rejected');
  assert.match(f.api.status().console.last.detail,expectedReason);
  assert.equal(f.summonBuilders().length,0);
  assert.equal(f.summonFlags().length,0);
  f.api.restore();
}
setFailure({},f=>f.memory.set(0x00A577F8,0),/No active duel/);
setFailure({},f=>f.memory.set(0x00A54E5C,0),/player turn/);
setFailure({},f=>f.memory.set(0x00A577FA,7),/main phase/);
setFailure({},f=>f.memory.set(0x00A56430+0xD44,0x20000),/flag 0x20000/);
setFailure({monster:false},null,/not a monster/);
setFailure({levelClass:5},null,/level 1..4/);
setFailure({eligibilityMask:0x01},null,/face-down set/);
setFailure({selectedSlot:0xFFFFFFFF},null,/No permitted/);
setFailure({slotLegal:false},null,/no longer permitted/);
setFailure({},f=>f.memory.set(0x00A55D68+0xD44,5),/Hand size/);
setFailure({},f=>f.memory.set(0x00A56434+0xD44+4*4,0x02A8038A),/card changed/);
const waitingSet=setFixture();
waitingSet.memory.set(0x00A5508E,0x21);
waitingSet.api.queueset(1,4,false);
waitingSet.tick();
assert.equal(waitingSet.api.status().console.pending.kind,'set');
assert.equal(waitingSet.summonBuilders().length,0);
assert.equal(waitingSet.api.canceldraw().state,'cancelled');
waitingSet.memory.set(0x00A5508E,0);
waitingSet.tick();
assert.equal(waitingSet.summonBuilders().length,0);
waitingSet.api.restore();
console.log('PASS: set preflight failures, unchanged hand and pending cancellation');

// Integration contract: real loader RPC lifecycle with a passive module double.
let deckStarts=0, deckStops=0, deckEnv;
const deckFixture=fixture({deckFactory(env) {
  deckEnv=env; let active=false;
  return {
    status(){return {deckActive:active,deckHooks:active?5:0};},
    start(mt,hash){
      assert.equal(hash,'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de');
      assert.equal(mt,true); active=true; deckStarts++;
      env.record({kind:'deck_probe',phase:'on'});
    },
    stop(){if(active){deckStops++;env.record({kind:'deck_probe',phase:'off'});} active=false;}
  };
}});
assert.throws(()=>deckFixture.api.tracedeckstart(true),/observe-only/);
deckFixture.api.installobserver();
assert.throws(()=>deckFixture.api.tracedeckstart(true),/trace on/);
assert.throws(()=>deckFixture.api.authorizedeckobserver('wrong'),/fingerprint/);
deckFixture.api.authorizedeckobserver('db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de');
deckFixture.api.tracestart(16);
assert.equal(deckFixture.api.tracedeckstart(true).deckActive,true);
assert.equal(deckEnv.session(),1);
assert.throws(()=>deckFixture.api.tracedemostart(),/Stop trace deck/);
assert.throws(()=>deckFixture.api.armspeedoverride(240),/Stop trace deck/);
assert.throws(()=>deckFixture.api.armmultiinstance(),/Stop trace deck/);
deckFixture.api.tracestop();
assert.equal(deckFixture.api.tracestatus().deckActive,false);
assert.equal(deckStarts,1);assert.equal(deckStops,1);
assert.equal(deckFixture.api.traceevents(0,16).filter(e=>e.kind==='deck_probe').length,2);
deckFixture.api.restore();
console.log('PASS: deck console RPC, host attestation, observer/trace gates, mutator exclusion and trace export lifecycle');


// Detailed CPU decision probe (no Joey execution): baseline before selector,
// nested candidate/score/event/restore, exact selector ID, and opt-in lifecycle.
const aiDetail=fixture();
aiDetail.api.installobserver();
aiDetail.memory.set(0x00A577F8,1);
aiDetail.memory.set(0x00A55D68,6);
for (const [index,card] of [894,359,893,307,364,550].entries())
  aiDetail.memory.set(0x00A56434+index*4,card);
aiDetail.api.tracestart(200);
assert.equal(aiDetail.api.traceaistart(true).aiDetailActive,true);
assert.equal(aiDetail.aiHookCount(),9);
assert.throws(()=>aiDetail.api.traceaistart(true),/already active/);
aiDetail.memory.set(0x00A579AA,8);
aiDetail.aiCall(0x005B08B0,[0],0,null,0x005B738A); // external baseline
aiDetail.aiCall(0x005B1360,[0,0xFFFFFFFF],5,()=>{
  aiDetail.aiCall(0x005B08B0,[0],0,null,0x005B1377);
  aiDetail.aiCall(0x005B1610,[0,550,0,0],0);
  aiDetail.memory.set(0x00A55D70,0x1f);
  aiDetail.memory.set(0x00A579AA,72);
  aiDetail.aiCall(0x005B0A50,[0],200,()=>{
    for (const [i,byte] of [0x08,0x08,0xc8,0x00].entries())
      aiDetail.memory.set(0x00A579A6+i,byte);
    aiDetail.aiCall(0x005B0B90,[0],1);
    aiDetail.aiCall(0x005B0B90,[0],0);
  });
  aiDetail.aiCall(0x005B0950,[0],0,()=>{
    aiDetail.memory.set(0x00A55D70,0);
    aiDetail.memory.set(0x00A579AC,0x1f);
  });
});
aiDetail.api.tracestop();
assert.equal(aiDetail.aiHookCount(),0);
assert.equal(aiDetail.api.tracestatus().aiDetailActive,false);
const aiEvents=aiDetail.api.traceevents(0,200);
const details=aiEvents.filter(e=>e.kind==='ai_detail');
assert.equal(details.length,7);
assert.deepEqual(Array.from(details.map(e=>e.function)),[
  'baseline','baseline','simulate_card','select_combat_event',
  'select_combat_event','aggregate_score','restore_field'
]);
assert.equal(details[0].selectorCallId,null);
assert.equal(details[0].caller,'0x5b738a');
assert.equal(details[1].selectorCallId,1);
assert.equal(details[2].internalCardIndex,550);
assert.equal(details[2].returnSigned,0);
assert.equal(details[3].selectedEventHex,'0808C800');
assert.equal(details[4].selectedEventHex,undefined);
assert.equal(details[5].returnSigned,200);
assert.equal(details[5].summaryRaw,72);
assert.equal(details[6].actionMaskBeforeRestore,31);
assert.equal(details[6].actionMaskOutput,31);
assert.equal(details[6].actionMaskCurrent,0);
assert.equal(aiEvents.at(-1).selectedCardIndexAtEntry,550);
aiDetail.api.restore();

const aiSimple=fixture();
aiSimple.api.installobserver();
aiSimple.api.tracestart(100);
assert.equal(aiSimple.api.traceaistart().aiDetailActive,false);
assert.equal(aiSimple.aiHookCount(),4);
aiSimple.api.tracestop();
assert.equal(aiSimple.aiHookCount(),0);
aiSimple.api.restore();

const aiRollback=fixture({failAiHookAt:0x005B1610});
aiRollback.api.installobserver();
aiRollback.api.tracestart(100);
assert.throws(()=>aiRollback.api.traceaistart(true),/rolled back/);
assert.equal(aiRollback.api.tracestatus().aiActive,false);
assert.equal(aiRollback.aiHookCount(),0);
aiRollback.api.tracestop();
aiRollback.api.restore();
console.log('PASS: opt-in AI detail capture, signed score, selected event, masks, teardown and rollback');

// The gate pilot must never infer outcomes from other callers or detached
// listeners. AAB90 is intentionally shared with the independent summon-prep
// observer; the two modes cannot coexist.
const gateFixture=fixture();
gateFixture.api.installobserver();
gateFixture.memory.set(0x00A577F8,1);
gateFixture.memory.set(0x00A55D68,3);
[834,375,178].forEach((v,i)=>gateFixture.memory.set(0x00A56434+i*4,v));
gateFixture.api.tracestart(200);
assert.equal(gateFixture.api.traceaistart('gates').aiGateActive,true);
assert.equal(gateFixture.api.tracestatus().aiDetailActive,true);
assert.equal(gateFixture.aiHookCount(),12);
assert.throws(()=>gateFixture.api.traceprepstart(),/Stop AI gate trace/);
gateFixture.aiCall(0x005AAB90,[0,834],0x11,null,0x005B13E5); // outside selector: ignore
gateFixture.aiCall(0x005B1360,[0,0xFFFFFFFF],0,()=>{
  gateFixture.aiCall(0x005AAB90,[0,834],0x11,null,0x005B19D9); // wrong caller
  gateFixture.aiCall(0x005AAB90,[0,834],0x11,null,0x005B13E5);
  gateFixture.aiCall(0x005AE3E0,[0,2,0x213],0,null,0x005B13FF);
  gateFixture.aiCall(0x005AE220,[834],0,null,0x005B1418);
  gateFixture.aiCall(0x005AAB90,[0,375],0,null,0x005B13E5); // reject early
  gateFixture.aiCall(0x005AAB90,[0,178],0x11,null,0x005B13E5);
  gateFixture.aiCall(0x005AE3E0,[0,2,1114],1,null,0x005B13FF); // reject policy
});
assert.equal(gateFixture.api.tracestatus().aiGateActive,true);
assert.equal(gateFixture.api.traceaistop().aiGateActive,false);
assert.equal(gateFixture.aiHookCount(),0);
gateFixture.api.tracestop();
const gateEvents=gateFixture.api.traceevents(0,200);
const gates=gateEvents.filter(e=>e.kind==='ai_gate');
assert.equal(gates.length,6);
assert.deepEqual(Array.from(gates.map(e=>e.function)),[
  'eligibility_mask','resource_policy','special_policy',
  'eligibility_mask','eligibility_mask','resource_policy'
]);
assert.deepEqual(Array.from(gates.map(e=>e.candidateCardIndex)),[834,834,834,375,178,178]);
assert.deepEqual(Array.from(gates.map(e=>e.gatePassed)),[true,true,true,false,true,false]);
assert.deepEqual(Array.from(gates.map(e=>e.handPositions[0])),[0,0,0,1,2,2]);
assert.equal(gates[1].argsRaw[2],0x213);
assert.equal(gates[2].returnSigned,0);
assert.ok(gates.every(e=>e.selectorCallId===1 && e.side===0));
assert.ok(gates.every(e=>e.callsiteReturn.startsWith('0x005B')));
assert.equal(gateFixture.api.tracestatus().aiGateActive,false);
gateFixture.api.restore();

const gateSuppressed=fixture();
gateSuppressed.api.installobserver();
gateSuppressed.api.tracestart(64);
gateSuppressed.api.traceprepstart();
assert.throws(()=>gateSuppressed.api.traceaistart('gates'),/trace prep off/);
assert.equal(gateSuppressed.aiHookCount(),0);
gateSuppressed.api.tracestop();
gateSuppressed.api.restore();

const gateRollback=fixture({failAiHookAt:0x005AE3E0});
gateRollback.api.installobserver();
gateRollback.api.tracestart(64);
assert.throws(()=>gateRollback.api.traceaistart('gates'),/rolled back/);
assert.equal(gateRollback.aiHookCount(),0);
assert.equal(gateRollback.api.tracestatus().aiGateActive,false);
gateRollback.api.tracestop();
gateRollback.api.restore();

const gateNormalMode=fixture();
gateNormalMode.api.installobserver();
gateNormalMode.api.tracestart(64);
gateNormalMode.api.traceaistart(true);
assert.equal(gateNormalMode.aiHookCount(),9);
assert.equal(gateNormalMode.api.tracestatus().aiGateActive,false);
gateNormalMode.api.tracestop();
gateNormalMode.api.restore();
console.log('PASS: CPU AI filter gates, direct callers, candidate identities, mode isolation and hook rollback');

// Synthetic call trees only: these tests do not execute or emulate Joey rules.
const combat=fixture();
combat.api.installobserver();
assert.equal(combat.api.tracestatus().hostAttestedExeSha256,null);
combat.api.authorizedeckobserver('db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de');
assert.equal(combat.api.tracestatus().hostAttestedExeSha256,
  'db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de');
assert.equal(combat.api.tracestatus().deckActive,false);
assert.equal(combat.api.tracestatus().processId,123);
assert.equal(combat.api.tracestatus().modulePath,'Z:\\passion\\joey_pc.exe');
combat.api.tracestart(2000);
assert.equal(combat.api.traceaistart('combat-context').combatContextActive,true);
assert.equal(combat.aiHookCount(),10);
assert.equal(combat.api.tracestatus().combatContextSchemaVersion,1);
assert.deepEqual(combat.api.tracestatus().combatReadPointsArmed,[]);
assert.throws(()=>combat.api.traceprepstart(),/combat-context/);
assert.throws(()=>combat.api.tracedemostart(),/combat-context/);
const buffer=0x00A579A6, own=buffer+16, rival=buffer+32;
for (const [offset,byte] of [5,0xf0,0xaa,0xbb].entries()) combat.memory.set(buffer+offset,byte);
combat.memory.set(0x00A55D7A,2);
combat.memory.set(0x00A55D7A+0xD44+3*0x90,1); // hypothetical defense, face-down
const beforeCombat=new Map(combat.memory);
combat.aiCall(0x005B1360,[0,0],0,()=>{
  combat.aiCall(0x005B0F40,[0,0,3,buffer],0,()=>{
    combat.aiCall(0x005ADD60,[0,0,own],0,null,0x005B0F97);
    combat.aiCall(0x005ADDC0,[1,3,rival],0,null,0x005B0FAE);
    combat.aiCall(0x0056D790,[0,0,0x291],2,null,0x005B1060);
    combat.aiCall(0x0056D790,[0,0,0x291],99,null,0x0056DD90); // nested stats: ignore
    combat.setThread(779);
    combat.aiCall(0x0056D790,[0,0,0x291],99,null,0x005B1060); // other thread: ignore
    combat.setThread(778);
    combat.aiCall(0x004022E0,[24],0x004D,null,0x005B0FB8);
    combat.aiCall(0x005B0F40,[0,1,2,buffer],0,()=>{
      combat.aiCall(0x0056B220,[0x14D],1,null,0x005B1205);
    });
    combat.aiCall(0x005699A0,[1,0x58F],1,null,0x005B11F4);
  });
});
assert.deepEqual(combat.memory,beforeCombat); // observer made no mock game writes
// Bound observation and make truncation explicit; unknown args never index memory.
combat.aiCall(0x005B1360,[0,0],0,()=>{
  for (let i=0; i<70; i++) combat.aiCall(0x005B0F40,[2,99,5,buffer],0);
});
assert.equal(combat.api.tracestatus().combatContextCount,64);
combat.api.tracestop();
const capturedContexts=combat.api.traceevents(0,256).filter(e=>e.kind==='combat_context');
const initialIds=new Set(capturedContexts.filter(e=>e.phase==='enter').slice(0,2).map(e=>e.combatCallId));
let contexts=capturedContexts.filter(e=>initialIds.has(e.combatCallId));
const entries=contexts.filter(e=>e.phase==='enter');
const leaves=contexts.filter(e=>e.phase==='leave' && e.function==='combat_event');
assert.equal(entries.length,2);
assert.equal(leaves.length,2);
assert.equal(entries[0].seedHex,'05F0AABB');
assert.equal(entries[0].defender.defensePosition,true);
assert.equal(entries[0].defender.faceUp,false);
assert.equal(entries[0].defender.slot,3);
assert.equal(entries[0].defender.resourceId,null);
assert.equal(entries[0].stateKind,'selector-window');
assert.equal(entries[0].selectorCallId,1);
assert.equal(leaves[1].combatCallId,entries[0].combatCallId);
assert.equal(contexts.find(e=>e.function==='phase_query').combatCallId,entries[1].combatCallId);
assert.equal(contexts.find(e=>e.function==='field_query').combatCallId,entries[0].combatCallId);
assert.equal(contexts.filter(e=>e.function==='equipment_count').length,1);
assert.equal(contexts.find(e=>e.function==='equipment_count').returnSigned,2);
assert.equal(contexts.find(e=>e.function==='resource').resourceId,0x4D);
assert.equal(contexts.filter(e=>e.function==='own_stats')[0].outputHex.length,24);
assert.equal(contexts.filter(e=>e.function==='opponent_stats')[0].outputHex.length,24);
assert.ok(contexts.every(e=>e.schemaVersion===1 && e.threadId===778 && e.seq>0 && e.timestampMs>0));
const allCombatEvents=combat.api.traceevents(0,256);
assert.equal(allCombatEvents.filter(e=>e.kind==='combat_context_limit').length,1);
contexts=allCombatEvents.filter(e=>e.kind==='combat_context' && e.phase==='enter');
assert.equal(contexts.length,64);
assert.equal(contexts.at(-1).attacker,null);
assert.equal(contexts.at(-1).readErrors[0].field,'argsRaw');
combat.api.traceaistop();
assert.equal(combat.aiHookCount(),0);
assert.equal(combat.api.tracestatus().combatContextActive,false);
combat.api.restore();

const combatReadError=fixture({failLpRead:true});
combatReadError.api.installobserver();
combatReadError.api.tracestart(100);
combatReadError.api.traceaistart('combat-context');
combatReadError.aiCall(0x005B1360,[0,0],0,()=>{
  combatReadError.aiCall(0x005B0F40,[0,0,1,0],0);
});
combatReadError.api.tracestop();
const failedCombat=combatReadError.api.traceevents(0,100).filter(e=>e.kind==='combat_context');
assert.equal(failedCombat[0].attacker.lifePoints,null);
assert.equal(failedCombat[0].seedHex,null);
assert.equal(failedCombat[0].readErrors.length,3);
assert.equal(failedCombat[1].outputHex,null);
assert.match(failedCombat[1].readError,/null output/);
combatReadError.api.restore();

const combatRollback=fixture({failAiHookAt:0x0056D790});
combatRollback.api.installobserver();
combatRollback.api.tracestart(100);
assert.throws(()=>combatRollback.api.traceaistart('combat-context'),/rolled back/);
assert.equal(combatRollback.aiHookCount(),0);
assert.equal(combatRollback.api.tracestatus().combatContextActive,false);
combatRollback.api.restore();
const combatActive=fixture();
combatActive.api.install('Z:\\YugiRE\\rules.dll',300,400,1,1);
combatActive.api.tracestart(100);
assert.throws(()=>combatActive.api.traceaistart('combat-context'),/observe-only/);
combatActive.api.restore();
const combatOverflow=fixture();
combatOverflow.api.installobserver();
combatOverflow.api.tracestart(100);
combatOverflow.api.traceaistart('combat-context');
combatOverflow.aiCall(0x005B1360,[0,0],0,()=>{
  combatOverflow.aiCall(0x005B0F40,[0,0,1,buffer],0,()=>{
    for (let i=0; i<25; i++)
      combatOverflow.aiCall(0x0056D790,[0,0,0x291],0,null,0x005B1060);
  });
});
combatOverflow.api.tracestop();
assert.equal(combatOverflow.api.traceevents(0,100).find(e=>e.childOverflow).childCount,25);
combatOverflow.api.restore();
const combatStopped=fixture();
combatStopped.api.installobserver();
combatStopped.api.tracestart(100);
combatStopped.api.traceaistart('combat-context');
combatStopped.aiCall(0x005B1360,[0,0],0,()=>{
  combatStopped.aiCall(0x005B0F40,[0,0,1,buffer],0,()=>{
    combatStopped.aiCall(0x005ADD60,[0,0,own],0,()=>{
      combatStopped.api.traceaistop();
    },0x005B0F97);
  });
});
combatStopped.api.tracestop();
assert.equal(combatStopped.api.traceevents(0,100).filter(e=>e.kind==='combat_context').length,1);
combatStopped.api.restore();
console.log('PASS: bounded passive combat context, wrapper outputs, seed, nulls, thread/nesting/caller correlation, rollback and mode gates');

// Instruction witnesses are synthetic CPU contexts, not dynamic validation.
const points=fixture();
points.api.installobserver(); points.api.tracestart(2000);
points.api.traceaistart('combat-context-all-unsafe');
assert.equal(points.aiHookCount(),19);
assert.equal(points.api.tracestatus().combatContextSchemaVersion,2);
points.aiCall(0x005B1360,[0,0],0,()=>{
  points.aiCall(0x005B0F40,[0,0,1,buffer],0,()=>{
    points.aiCall(0x005ADD60,[0,0,own],0,()=>{
      points.instruction(0x005ADD8D,{eax:0xAB02});
    },0x005B0F97);
    points.aiCall(0x005ADDC0,[1,1,rival],0,()=>{
      points.instruction(0x005ADE2C);
    },0x005B0FAE);
    points.instruction(0x005B0F7B,{ecx:0xABCDEF03});
    points.instruction(0x005B108A,{edx:0x1234F005});
    points.instruction(0x005B119B,{eax:0x400123,esi:0xD44});
    points.instruction(0x005B11C6,{ecx:0});
    points.setThread(779); // cannot attach a branch to the other thread's frame
    points.instruction(0x005B11CF);
    points.setThread(778);
    points.aiCall(0x005B0F40,[1,0,1,buffer],0,()=>{
      points.instruction(0x005B11C6,{ecx:0x351});
      points.instruction(0x005B11CF);
      points.instruction(0x005B11DD);
    });
    points.instruction(0x005B11DD);
    points.instruction(0x005B119B,{}); // failed register read remains null
  });
});
points.api.tracestop();
const witnesses=points.api.traceevents(0,256).filter(e=>e.kind==='combat_context');
assert.equal(witnesses.find(e=>e.function==='own_visibility_consumed').valueRaw,0xAB02);
assert.equal(witnesses.find(e=>e.function==='opponent_visible_taken').faceUpConsumed,true);
assert.equal(witnesses.find(e=>e.function==='own_stats').positionBeforeRaw,0);
assert.equal(witnesses.find(e=>e.function==='own_stats').positionAfterRaw,0);
assert.equal(witnesses.find(e=>e.function==='target_position_consumed').valueRaw,3);
assert.equal(witnesses.find(e=>e.function==='descriptor_seed_consumed').preservedMaskRaw,0xF005);
assert.equal(witnesses.find(e=>e.function==='defender_flags_consumed').valueRaw,0x400123);
assert.equal(JSON.stringify(witnesses.filter(e=>e.function==='attacker_flags_consumed').map(e=>e.protectionConsumed)),'[true,false]');
assert.equal(witnesses.filter(e=>e.function==='attacker_protection_taken').length,1);
const missingRegister=witnesses.filter(e=>e.function==='defender_flags_consumed').at(-1);
assert.equal(missingRegister.valueRaw,null); assert.ok(missingRegister.readError);
points.api.tracestop(); points.api.restore();
const badSignature=fixture(); badSignature.api.installobserver(); badSignature.api.tracestart(100);
badSignature.memory.set(0x005B11C6,0);
assert.throws(()=>badSignature.api.traceaistart('combat-context-point:attacker_flags_sample'),/signature mismatch/);
assert.equal(badSignature.aiHookCount(),0);
badSignature.api.restore();
// The legacy observer must not validate or install a v2 point at all.
const legacyFallback=fixture(); legacyFallback.api.installobserver(); legacyFallback.api.tracestart(100);
legacyFallback.memory.set(0x005B11C6,0);
legacyFallback.api.traceaistart('combat-context');
assert.equal(legacyFallback.aiHookCount(),10);
assert.deepEqual(legacyFallback.api.tracestatus().combatReadPointsArmed,[]);
assert.equal(legacyFallback.api.tracestatus().combatContextSchemaVersion,1);
legacyFallback.api.tracestop(); legacyFallback.api.restore();
const pointRollback=fixture({failAiHookAt:0x005B11DD});
pointRollback.api.installobserver(); pointRollback.api.tracestart(100);
assert.throws(()=>pointRollback.api.traceaistart('combat-context-point:attacker_flags_consumed'),/rolled back/);
assert.equal(pointRollback.aiHookCount(),0); pointRollback.api.restore();
console.log('PASS: consumed register/branch witnesses, thread and reentrant isolation, unknowns and instruction preflight');
// A single diagnostic point is the only mode offered by the operator console.
const invalidPoint=fixture(); invalidPoint.api.installobserver(); invalidPoint.api.tracestart(100);
assert.throws(()=>invalidPoint.api.traceaistart('combat-context-point:not_a_point'),/Unknown combat-context observation point/);
assert.equal(invalidPoint.aiHookCount(),0);
invalidPoint.api.restore();

const pointLoss=fixture(); pointLoss.api.installobserver(); pointLoss.api.tracestart(16);
pointLoss.api.traceaistart('combat-context-all-unsafe');
const pointLossBefore=new Map(pointLoss.memory);
pointLoss.aiCall(0x005B1360,[0,0],0,()=>{
  pointLoss.aiCall(0x005B0F40,[0,0,1,buffer],0,()=>{
    // Missing TEST cannot produce a false bit at the join.
    pointLoss.instruction(0x005B11DD);
    for(let i=0;i<30;i++) pointLoss.instruction(0x005B108A,{edx:i});
  });
});
pointLoss.api.tracestop();
assert.deepEqual(pointLoss.memory,pointLossBefore);
assert.ok(pointLoss.api.tracestatus().overwritten>0);
assert.equal(pointLoss.api.traceevents(0,16).find(e=>e.function==='combat_event').childOverflow,true);
pointLoss.api.restore();
const missingTest=fixture(); missingTest.api.installobserver(); missingTest.api.tracestart(100);
missingTest.api.traceaistart('combat-context-point:attacker_flags_consumed');
assert.equal(missingTest.aiHookCount(),11);
assert.deepEqual(missingTest.api.tracestatus().combatReadPointsArmed,['attacker_flags_consumed']);
missingTest.aiCall(0x005B1360,[0,0],0,()=>{
  missingTest.aiCall(0x005B0F40,[0,0,1,buffer],0,()=>missingTest.instruction(0x005B11DD));
});
missingTest.api.tracestop();
const unknownBit=missingTest.api.traceevents(0,100).find(e=>e.function==='attacker_flags_consumed');
assert.equal(unknownBit.protectionConsumed,null); assert.ok(unknownBit.correlationError);
missingTest.api.restore();
console.log('PASS: point event loss and overflow remain explicit, missing TEST stays unknown, no observer game writes');
