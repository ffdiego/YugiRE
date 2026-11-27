set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json
import time

SAMPLE_COUNT = 360
UPDATE_RETURNS = {0x0044DD47: "regular", 0x0044DD8A: "transition"}
DRAW_RETURNS = {0x0044DD62: "regular", 0x0044DD92: "transition"}
frames = []
active_frames = {}

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little")

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class SceneEntry(HardwareProbe):
    def stop(self):
        started = time.monotonic_ns()
        context = reg("ecx")
        thread = gdb.selected_thread().num
        active_frames[thread] = {
            "thread": thread, "ns": started, "mode": read(0x005F3294),
            "flag14": read(context + 0x14, 1), "field1c": read(context + 0x1c),
            "dispatches": [],
        }
        return False

class Dispatcher(HardwareProbe):
    def __init__(self, address, kind, sites, mask_offset):
        super().__init__(address)
        self.kind = kind
        self.sites = sites
        self.mask_offset = mask_offset

    def stop(self):
        started = time.monotonic_ns()
        frame = active_frames.get(gdb.selected_thread().num)
        if frame is None:
            return False
        stack = reg("esp")
        branch = self.sites.get(read(stack))
        if branch:
            frame["dispatches"].append([self.kind, branch, read(stack + self.mask_offset), started])
        return False

class SceneDispatchEnd(HardwareProbe):
    def stop(self):
        frame = active_frames.pop(gdb.selected_thread().num, None)
        if frame is None:
            return False
        frames.append(frame)
        return len(frames) == SAMPLE_COUNT

probes = [
    SceneEntry(0x0044DD00),
    Dispatcher(0x0045D040, "update", UPDATE_RETURNS, 4),
    Dispatcher(0x0045D170, "draw", DRAW_RETURNS, 8),
    SceneDispatchEnd(0x0044DD95),
]
end
continue
python
if len(frames) == SAMPLE_COUNT:
    print("scene-cadence " + json.dumps({"nominal": read(0x005EB618), "frames": frames}))
end
detach
quit
