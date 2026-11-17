// Frida mocks only. These tests do not validate the Windows ABI or run the game.
const fs=require('fs'),vm=require('vm'),assert=require('assert'),path=require('path');
const source=fs.readFileSync(path.join(__dirname,'../capture/activation_observer.js'),'utf8');
function environment(failAt=0) {
  const mem=new Map(),hooks=new Map(),events=[];let allocations=0x2000000,tick=1000n,detaches=0,attached=0;
  class Pointer {
    constructor(n){this.n=Number(n);}
    add(n){return new Pointer(this.n+Number(n));} sub(p){return new Pointer(this.n-Number(p.n??p));}
    equals(p){return this.n===p.n;} toString(){return '0x'+this.n.toString(16);}
    readS32(){const n=mem.get(this.n);if(n===undefined)throw Error('missing memory '+this);return Number(n)|0;}
    readU32(){return this.readS32()>>>0;} readU8(){return this.readU32()&255;}
    readPointer(){return new Pointer(this.readU32());} readS64(){return {toString:()=>String(mem.get(this.n))};}
  }
  const pointer=n=>new Pointer(n);const put=(at,n)=>mem.set(Number(at.n??at),n);
  const ctx={rpc:{exports:{}},Process:{platform:'windows',arch:'ia32',getCurrentThreadId:()=>17,
    enumerateModules:()=>[{path:'C:\\game\\joey_pc.exe',base:pointer(0x400000)}],
    getModuleByName:name=>{assert.equal(name,'kernel32.dll');return {getExportByName:n=>n};}},
    Memory:{alloc:n=>{const p=pointer(allocations);allocations+=n+8;return p;}},
    NativeFunction:function(name){assert(['QueryPerformanceCounter','QueryPerformanceFrequency'].includes(name));return p=>{put(p,name==='QueryPerformanceFrequency'?1000000n:tick++);return 1;};},
    Interceptor:{attach:(p,callbacks)=>{if(++attached===failAt)throw Error('attach failed');hooks.set(p.n,callbacks);return {detach:()=>{detaches++;hooks.delete(p.n);}};},flush:()=>{}},
    send:e=>events.push(structuredClone(e)),Map,Array,Number,Object,String,Error};
  vm.runInNewContext(source,ctx);
  const api=ctx.rpc.exports;
  function object(counter=0) {
    const p=pointer(0x1000000);
    for(const [off,n] of [[0,0x5db2f4],[4,1],[0x10,counter],[0x14,525],[0x18,352],[0x1c,0],[0xac+0x3c,54],[0xac+0x40,78]])put(p.add(off),n);
    for(let i=0;i<512;i++)put(0xa52ee8+i*4,Math.trunc(Math.cos(i*Math.PI/256)*65536));
    return p;
  }
  function image(p,format=12) {
    for(const [off,n] of [[0x74,format],[0x78,1],[0x70,0],[0x3c,164],[0x40,164],[0x18,0],[0x1c,0],[0x20,164],[0x24,164]])put(p.add(off),n);
  }
  return {api,ctx,hooks,events,put,pointer,object,image,detaches:()=>detaches};
}
{
 const e=environment();e.api.arm(1024);assert.equal(e.hooks.size,5);
 const obj=e.object(),update=e.hooks.get(0x450590),call={context:{ecx:obj}};
 update.onEnter.call(call);e.put(obj.add(0x10),16);update.onLeave.call(call);
 assert.deepEqual(e.events.map(x=>x.kind),['armed','start','update_enter','update_leave']);
 assert.equal(e.events[1].state.cardWidth,54);assert.equal(e.events[1].qpcFrequency,'1000000');
 const draw=e.hooks.get(0x4505d0),dc={context:{ecx:obj}};draw.onEnter.call(dc);
 const stack=e.pointer(0x1100000),src=e.pointer(0x1200000),dst=e.pointer(0x1300000);e.image(src);e.image(dst,7);
 const args=[dst.n,src.n,525,352,8,12785,12785,49];args.forEach((x,i)=>e.put(stack.add(4+i*4),x));
 e.hooks.get(0x422c00).onEnter.call({context:{esp:stack}});draw.onLeave.call(dc);
 const transform=e.events.find(x=>x.kind==='transform');assert.deepEqual(transform.args,args);assert.equal(transform.source.pointer,'0x1200000');assert.equal(transform.destination.format,7);
 const footer=e.api.stop();assert.equal(footer.detached,true);assert.equal(e.hooks.size,0);assert.equal(footer.readErrors,0);
}
{
 const e=environment(4);assert.throws(()=>e.api.arm(1024),/attach failed/);assert.equal(e.hooks.size,0);assert.equal(e.detaches(),3);
}
{
 const e=environment();e.ctx.Process.platform='linux';assert.throws(()=>e.api.arm(1024),/Windows x86/);assert.equal(e.hooks.size,0);
}
{
 const e=environment();e.api.arm(128);const obj=e.object(),update=e.hooks.get(0x450590);
 for(let n=0;n<80;n++){e.put(obj.add(0x10),0);const call={context:{ecx:obj}};update.onEnter.call(call);e.put(obj.add(0x10),16);update.onLeave.call(call);}
 assert.equal(e.api.status().overflow,true);assert.equal(e.events.length,128);e.api.stop();
}
console.log('PASS: mocked observer selection, ABI argument reads, QPC events, cleanup, attach rollback, host gate and event bound; Windows unvalidated');
