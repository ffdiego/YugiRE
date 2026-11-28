set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

opening = None
ticks = []
producers = {}

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4, signed=False):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little", signed=signed)

class Probe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Request(Probe):
    def stop(self):
        sp = reg("esp")
        key = read(sp + 4)
        if key == 0:
            caller = read(sp)
            producers[caller] = producers.get(caller, 0) + 1
        return False

class Open(Probe):
    def stop(self):
        global opening
        if opening is None and read(0x005ebee4) == 0:
            opening = {"object": reg("esi"), "key": read(0x005ebee4),
                       "context": reg("ebx") & 0xffff, "thread": gdb.selected_thread().num}
            print(f"opened help: key={opening['key']}, context={opening['context']:#06x}", flush=True)
        return False

def belongs(obj):
    return (opening is not None and opening["object"] == obj
            and opening["thread"] == gdb.selected_thread().num)

class Draw(Probe):
    def stop(self):
        obj = reg("edi")
        if belongs(obj):
            ticks.append(read(obj + 0x14))
        return False

class Close(Probe):
    def stop(self):
        obj = reg("esi")
        if belongs(obj):
            opening["closing_request"] = read(0x005ebee4, signed=True)
            opening["closing_tick"] = read(obj + 0x14)
            return True
        return False

probes = [Open(0x0045e473), Draw(0x0045e4b1), Close(0x0045e429), Request(0x0045e240)]
end
continue
python
if opening and "closing_tick" in opening:
    print("help-live " + json.dumps({"key": opening["key"], "context": opening["context"],
          "draws": ticks, "producers": producers, "closing_tick": opening["closing_tick"], "closing_request": opening["closing_request"]}))
end
detach
quit
