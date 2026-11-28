"""Offline checks for profile isolation and trace correlation."""
from pathlib import Path
import runpy
import shutil
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[1]
COLLECTOR = runpy.run_path(str(ROOT / "scripts/joey_visual_trace.py"))
MOCK = """
const assert = require('node:assert/strict');
const memory = new Map(), listeners = new Map();
class Pointer {
    constructor(address) { this.address = address; }
    add(offset) { return new Pointer(this.address + offset); }
    sub(other) { return new Pointer(this.address - other.address); }
    toUInt32() { return this.address >>> 0; }
    toInt32() { return this.address | 0; }
    toString() { return this.address.toString(16); }
    isNull() { return this.address === 0; }
    readU32() { return memory.get(this.address) ?? 0; }
    readU16() { return this.readU32() & 0xffff; }
    readU8() { return this.readU32() & 0xff; }
    readS32() { return this.readU32() | 0; }
    readPointer() { return new Pointer(this.readU32()); }
}
const Process = {arch:'ia32', mainModule:{base:new Pointer(0x400000),size:0x700000}};
const rpc = {};
const Interceptor = {attach(pointer,callbacks) {
    const address = pointer.address;
    assert(!listeners.has(address));
    listeners.set(address,callbacks);
    return {detach(){listeners.delete(address);}};
}};
function call(address,{args=[],object=0,caller=0x410000,result=0,inside=()=>{}}={}) {
    const ctx={threadId:1,returnAddress:new Pointer(caller),context:{ecx:new Pointer(object)}};
    const hook=listeners.get(address);
    assert(hook, address.toString(16));
    hook.onEnter?.call(ctx,args.map(value=>new Pointer(value)));
    inside();
    hook.onLeave?.call(ctx,new Pointer(result));
}
"""


@unittest.skipUnless(shutil.which("node"), "Node.js required for the Frida API mock")
class JoeyVisualTraceTests(unittest.TestCase):
    def run_agent(self, checks):
        result = subprocess.run(["node"], input=MOCK + COLLECTOR["AGENT"] + checks,
                                text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)

    def test_cursor_pass_tracks_click_and_effect_outside_duel(self):
        self.run_agent("""
rpc.exports.start('cursor');
assert(!listeners.has(0x414110));
call(0x54b030,{args:[0,0x200,0,0]});
call(0x54b030,{args:[0,0x201,0,(400<<16)|700]});
call(0x47fe90,{inside(){
    call(0x451f70,{result:0x90000,caller:0x47ffac});
}});
memory.set(0x90010,16);
call(0x47fe90,{inside(){
    call(0x450fb0,{object:0x90000});
    call(0x451fb0,{object:0x90000});
}});
const trace=rpc.exports.stop();
assert.equal(listeners.size,0);
assert.equal(trace.cursorPasses,2);
const [input,first,create,second,update,draw]=trace.events;
assert.equal(trace.events.length,6);
assert.deepEqual([input.kind,input.message,input.x,input.y],['mouse-button',0x201,700,400]);
assert.deepEqual([first.cursorPass,create.cursorPass,second.cursorPass,update.cursorPass,draw.cursorPass],
                 [1,1,2,2,2]);
assert.deepEqual([draw.frame,create.object,draw.object,draw.counter],[null,'90000','90000',16]);
""")

    def test_battle_pass_tracks_transfer_words_and_gate(self):
        self.run_agent("""
rpc.exports.start('battle');
assert(!listeners.has(0x47fe90));
memory.set(0xa550a0,0x8083);
memory.set(0xa550a2,0x0100);
memory.set(0x90100,0x8000);
memory.set(0x90200,0x8003);
call(0x44dd00,{inside(){
    call(0x5b9170,{args:[0x8083,0x0100,0x0201,0],caller:0x576da4});
    call(0x5b9470,{inside(){
        call(0x40cdb0,{args:[300,0x90100,0x90200],caller:0x571861});
        memory.set(0xa558ac,0x4002);
    }});
}});
const [producer,travel,step]=rpc.exports.stop().events;
assert.deepEqual([producer.kind,producer.opcode,producer.frame,producer.words[0]],
                 ['transfer-command-produced',0x83,1,0x8083]);
assert.deepEqual([travel.sourceDescriptor,travel.destinationDescriptor],[0x8000,0x8003]);
assert.deepEqual([step.frame,step.substate,step.busy],[1,2,true]);
""")

    def test_all_includes_both_profiles_without_duplicate_hooks(self):
        self.run_agent("""
rpc.exports.start('all');
for (const address of [0x47fe90,0x414110,0x40cdb0,0x416bc0]) {
    assert(listeners.has(address));
}
rpc.exports.stop();
""")
