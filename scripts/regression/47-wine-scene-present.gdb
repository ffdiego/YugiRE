set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json
import time

SAMPLE_COUNT = 120
samples = []
active = {}

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def u32(address):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, 4), "little")

def rect(address):
    if not address:
        return None
    raw = gdb.selected_inferior().read_memory(address, 16)
    return [int.from_bytes(raw[i:i + 4], "little", signed=True) for i in range(0, 16, 4)]

def thread():
    return gdb.selected_thread().num

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Scene(HardwareProbe):
    def stop(self):
        current = thread()
        if current in active:
            samples.append(active[current])
            if len(samples) == SAMPLE_COUNT:
                return True
        scene = reg("ecx")
        active[current] = {
            "thread": current, "sceneNs": time.monotonic_ns(), "loopCounter": u32(0x005F2FB4),
            "sceneReturn": u32(reg("esp")), "flag14": u32(scene + 0x14) & 0xff,
            "field1c": u32(scene + 0x1c), "appCalls": [], "backendCalls": [],
        }
        return False

class AppCall(HardwareProbe):
    def stop(self):
        row = active.get(thread())
        if row is not None:
            table = reg("edx")
            row["appCalls"].append([time.monotonic_ns(), table, u32(table + 4)])
        return False

class BltFast(HardwareProbe):
    def stop(self):
        row = active.get(thread())
        if row is not None:
            stack = reg("esp")
            implementation = u32(reg("ecx") + 0x1c)
            row["backendCalls"].append({
                "callNs": time.monotonic_ns(), "module": gdb.solib_name(implementation) or "",
                "xy": [u32(stack + 4), u32(stack + 8)],
                "source": rect(u32(stack + 16)), "flags": u32(stack + 20),
            })
        return False

class BltFastReturn(HardwareProbe):
    def stop(self):
        row = active.get(thread())
        if row is not None and row["backendCalls"]:
            calls = row["backendCalls"]
            calls[-1].update(returnNs=time.monotonic_ns(), hresult=reg("eax"))
        return False

probes = [Scene(0x0044DD00), AppCall(0x004112CA), BltFast(0x004A9098),
          BltFastReturn(0x004A909B)]
end
continue
python
if len(samples) == SAMPLE_COUNT:
    print("scene-present " + json.dumps({"nominal": u32(0x005EB618), "samples": samples}))
end
detach
quit
