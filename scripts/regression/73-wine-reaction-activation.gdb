set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

route = None
candidate = None
draws = []

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little")

def arguments(n):
    return [read(reg("esp") + 4*i) for i in range(1, n + 1)]

class Probe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Activation(Probe):
    def stop(self):
        global route
        route = {
            "side": arguments(3)[0], "resource": arguments(3)[1],
            "orientation": read(0x00a54e5c, 1) & 1, "mode": read(0x005f3294),
            "opcode": read(0x00a550a0, 1),
        }
        return False

class Request(Probe):
    def stop(self):
        global candidate
        if (route is not None and route["opcode"] == 0xcf and
                read(reg("esp")) == 0x00402d1e and arguments(2) == [7, 0x1000003a]):
            candidate = dict(route, manifest=7, request=0x1000003a)
        return False

class Draw(Probe):
    def stop(self):
        if candidate is None:
            return False
        obj = reg("ecx")
        if read(obj + 0x14) == candidate["request"] and read(obj + 0x18) == 0:
            draws.append({
                "counter": read(obj + 0x10), "scene": read(0x005f2fb4),
                "gate": read(obj + 0x1c, 1), "active": read(obj + 4, 1),
            })
        return False

class Terminal(Probe):
    def stop(self):
        if candidate is None or not draws:
            return False
        obj = reg("edi")
        if read(obj + 0x14) != candidate["request"] or read(obj + 0x18) != 0:
            return False
        print("activation-reaction " + json.dumps(dict(candidate, draws=draws)))
        return True

probes = [Activation(0x00402be0), Request(0x00402e00), Draw(0x00416bc0), Terminal(0x004179f0)]
end
continue
detach
quit
