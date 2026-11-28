set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

result = None
updates = []
draws = []

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little")

class Probe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Construct(Probe):
    def stop(self):
        global result
        result = {"caller": read(reg("esp")), "thread": gdb.selected_thread().num}
        return False

def belongs(obj):
    return (result is not None and result["thread"] == gdb.selected_thread().num
            and read(obj) == 0x005da820)

class Update(Probe):
    def stop(self):
        obj = reg("ecx")
        if belongs(obj):
            result["speech_id"] = read(obj + 0x14) + read(obj + 0x18)
            result["result_flag"] = read(0x005f3254, 1)
            updates.append(read(obj + 0x10))
        return False

class Draw(Probe):
    def stop(self):
        obj = reg("ecx")
        if belongs(obj):
            draws.append(read(obj + 0x10))
        return False

class ClearActive(Probe):
    def stop(self):
        obj = reg("ebp")
        if not belongs(obj):
            return False
        result["terminal"] = read(obj + 0x10)
        result["previously_active"] = read(obj + 4, 1)
        return True

probes = [Construct(0x00415af0), Update(0x00416050), Draw(0x00416260), ClearActive(0x004166c3)]
end
continue
python
if result and "terminal" in result:
    del result["thread"]
    print("result-bust " + json.dumps({**result, "updates": updates, "draws": draws}))
end
detach
quit
