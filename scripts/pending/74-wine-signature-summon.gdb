set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

routes = []
scene = None
updates = []
draws = []

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little")

class Probe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Route(Probe):
    def stop(self):
        sp = reg("esp")
        resource = read(sp + 8)
        if resource in (0x51, 0x2e8):
            trace = {"resource": resource, "side": read(sp + 4), "type": read(sp + 12),
                     "orientation": read(0x00a54e5c, 1) & 1, "mode": read(0x005f3294)}
            routes.append(trace)
            print("signature-route " + json.dumps(trace), flush=True)
        return False

class Constructor(Probe):
    def stop(self):
        global scene
        sp = reg("esp")
        scene = (reg("ecx"), read(sp + 4))
        print("signature-construct " + json.dumps({"variant": scene[1], "caller": hex(read(sp))}), flush=True)
        return False

class Update(Probe):
    def stop(self):
        if scene is not None and reg("ecx") == scene[0]:
            updates.append(read(scene[0] + 0x14))
        return False

class Draw(Probe):
    def stop(self):
        if scene is None or reg("ecx") != scene[0]:
            return False
        counter = read(scene[0] + 0x14)
        draws.append(counter)
        if counter < 0x10060:
            return False
        print("signature " + json.dumps(
            {"routes": routes, "variant": scene[1], "updates": updates, "draws": draws}), flush=True)
        return True

probes = [Route(0x00403310), Constructor(0x00414a20), Update(0x00414cf0), Draw(0x00414dc0)]
end
continue
detach
quit
