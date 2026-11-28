set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

capture = None
samples = []
row_index = 0
surface = None
return_stack = None
pending = None
sample_step = 3

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4, signed=False):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little", signed=signed)

def thread():
    return gdb.selected_thread().num

def vertices(address):
    return [[read(address + 8*i + axis, signed=True) for axis in (0, 4)] for i in range(4)]

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Entry(HardwareProbe):
    def stop(self):
        global capture, surface, return_stack
        sp = reg("esp")
        if capture or read(sp) != 0x00422a07 or read(sp + 0x74) != 0x004146ea:
            return False
        destination, source = reg("ecx"), read(sp + 4)
        if read(destination + 0x74) != 3 or read(source + 0x74) != 3:
            return False
        if read(sp + 0x18) != 4:
            return False
        points = vertices(read(sp + 0xc))
        if points[0][0] == points[1][0] or points[0][1] == points[1][1]:
            return False
        capture = {
            "thread": thread(),
            "intensity": 256,
            "extraDivisor": bool(read(sp + 0x14, 1)),
            "sourceVertices": vertices(read(sp + 8)),
            "destinationVertices": points,
            "keyedCopy": True,
        }
        surface = (destination, source)
        return_stack = sp + 0x1c
        self.enabled = False
        finish.enabled = True
        return False

class Row(HardwareProbe):
    def stop(self):
        global row_index
        if capture is None or thread() != capture["thread"]:
            return False
        destination, _ = surface
        stride = read(destination + 0x14)
        if stride == 0:
            return False
        y = (reg("edi") - read(destination + 0x10)) // stride
        row_index = 0
        pixel.enabled = y % sample_step == 0
        after.enabled = pixel.enabled
        return False

class Pixel(HardwareProbe):
    def stop(self):
        global pending
        if capture is None or thread() != capture["thread"]:
            return False
        destination, source = surface
        sp = reg("esp")
        if row_index % sample_step == 0:
            dst, src = read(sp + 0x34), reg("eax")
            y, x = divmod(dst - read(destination + 0x10), read(destination + 0x14))
            v, u = divmod(src - read(source + 0x10), read(source + 0x14))
            pending = (dst, {"x": x // 2, "y": y, "u": u // 2, "v": v,
                             "aligned": u % 2 == 0 and x % 2 == 0,
                             "source": read(src, 2), "key": read(sp + 0xc0, 2),
                             "before": read(dst, 2)})
        return False

class After(HardwareProbe):
    def stop(self):
        global row_index, pending
        if capture is None or thread() != capture["thread"]:
            return False
        if pending is not None:
            dst, sample = pending
            sample["after"] = read(dst, 2)
            samples.append(sample)
            pending = None
        row_index += 1
        if read(reg("esp") + 0x44) == 1:
            pixel.enabled = False
            self.enabled = False
        return len(samples) >= 144

class Finish(HardwareProbe):
    def stop(self):
        global capture, pending
        if capture and thread() == capture["thread"] and reg("esp") == return_stack:
            # All 144 samples must come from one draw call.
            capture = None
            pending = None
            samples.clear()
            pixel.enabled = False
            after.enabled = False
            self.enabled = False
            entry.enabled = True
        return False

entry = Entry(0x0051ebd0)
row = Row(0x0052ce14)
pixel = Pixel(0x0052ce20)
after = After(0x0052ce34)
finish = Finish(0x00422a07)
pixel.enabled = False
after.enabled = False
finish.enabled = False
end
continue
python
if capture and samples:
    print("attack-texel-samples " + json.dumps({**capture, "samples": samples}))
end
detach
quit
