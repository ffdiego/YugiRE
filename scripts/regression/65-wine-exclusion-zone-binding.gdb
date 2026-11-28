set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

observed = {}

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, 4), "little")

class Binding(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

    def stop(self):
        sp = reg("esp")
        side, zone = read(sp), read(sp + 4)
        parent = read(0x005f300c)
        receiver = reg("ecx")
        vtable = read(receiver)
        observed[side] = {"zone": zone, "offset": receiver - parent,
                          "vtable": vtable, "slot18": read(vtable + 0x18)}
        return 0 in observed and 1 in observed

hooks = [Binding(0x0044c4d6), Binding(0x0044c516)]
end
continue
python
print("exclusion-bindings " + json.dumps(observed, sort_keys=True))
end
detach
quit
