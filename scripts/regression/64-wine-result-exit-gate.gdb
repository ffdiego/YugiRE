set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

parent = None
states = []
draws = []
clears = []

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little")

def thread():
    return gdb.selected_thread().num

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Parent(HardwareProbe):
    def stop(self):
        global parent
        obj = reg("ecx")
        if parent is None:
            parent = (thread(), obj)
        if parent != (thread(), obj):
            return False
        phase = read(obj + 0x10)
        # Other substates include a per-frame counter through the prompt.
        subphase = read(obj + 0x14) if phase in (5, 6) else None
        state = {
            "phase": phase, "subphase": subphase,
            "mode": read(0x005f3294), "resultFlag": read(0x005f3254, 1),
            "bit6": bool(read(0x00a54e5c, 1) & 64),
            "active": bool(read(obj + 0x30, 1)), "running": bool(read(obj + 0x40, 1)),
        }
        if not states or state != states[-1]:
            states.append(state)
        return False

class Draw(HardwareProbe):
    def stop(self):
        obj = reg("ecx")
        if parent == (thread(), obj - 0x2c):
            draws.append({
                "counter": read(obj + 0x10), "active": bool(read(obj + 4, 1)),
                "running": bool(read(obj + 0x14, 1)),
            })
        return False

class Clear(HardwareProbe):
    def stop(self):
        obj = reg("esi")
        if parent == (thread(), obj - 0x2c):
            clears.append({"counter": read(obj + 0x10), "active": bool(read(obj + 4, 1))})
        return False

class SceneExit(HardwareProbe):
    def stop(self):
        if parent != (thread(), reg("ebp")):
            return False
        print("result-exit-route " + json.dumps({"states": states, "draws": draws, "clears": clears}))
        return True

probes = [Parent(0x00447000), Draw(0x00444490), Clear(0x0044452d), SceneExit(0x0044777f)]
end
continue
detach
quit
