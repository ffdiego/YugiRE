set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

opening = None
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
        global opening
        opening = {"caller": read(reg("esp")), "thread": gdb.selected_thread().num}
        return False

def belongs(obj):
    return (opening is not None and opening["thread"] == gdb.selected_thread().num
            and read(obj) == 0x005da800)

class Update(Probe):
    def stop(self):
        obj = reg("ecx")
        if belongs(obj):
            opening["variant"] = read(obj + 0x10)
            updates.append(read(obj + 0x14))
        return False

class Draw(Probe):
    def stop(self):
        obj = reg("ecx")
        if belongs(obj):
            draws.append(read(obj + 0x14))
        return False

class ClearActive(Probe):
    def stop(self):
        obj = reg("ebx")
        if not belongs(obj):
            return False
        opening["terminal"] = read(obj + 0x14)
        opening["previously_active"] = read(obj + 4, 1)
        return True

probes = [Construct(0x00415130), Update(0x00415580), Draw(0x00415840), ClearActive(0x00415acb)]
end
continue
python
if opening and "terminal" in opening:
    del opening["thread"]
    print("opening-bust " + json.dumps({**opening, "updates": updates, "draws": draws}))
end
detach
quit
