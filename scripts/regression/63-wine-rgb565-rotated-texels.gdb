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
requested = gdb.convenience_variable("rotated_intensity")
intensity = 256 if requested is None else int(requested)
if intensity not in range(32, 257, 32):
    raise ValueError("Choose an intensity from 32 to 256 in steps of 32")
blended = intensity < 256
row_destination_register = "ecx" if blended else "edi"
source_register = "edx" if blended else "eax"
destination_offset = 0x30 if blended else 0x34
key_offset = 0xc4 if blended else 0xc0
remaining_offset = 0x50 if blended else 0x44

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
        if capture:
            return False
        if blended:
            if read(sp) != 0x00422e10 or read(sp + 0x7c) != 0x00414805:
                return False
            if read(sp + 0x20) != 4 or read(sp + 0xc) != intensity:
                return False
            destination, source = read(sp + 4), read(sp + 8)
            source_arg, destination_arg, flag_arg = 0x10, 0x14, 0x1c
            return_stack = sp + 4
        else:
            if read(sp) != 0x00422a07 or read(sp + 0x74) != 0x004146ea:
                return False
            if read(sp + 0x18) != 4:
                return False
            destination, source = reg("ecx"), read(sp + 4)
            source_arg, destination_arg, flag_arg = 8, 0xc, 0x14
            return_stack = sp + 0x1c
        if read(destination + 0x74) != 3 or read(source + 0x74) != 3:
            return False
        points = vertices(read(sp + destination_arg))
        if points[0][0] == points[1][0] or points[0][1] == points[1][1]:
            return False
        capture = {
            "thread": thread(),
            "intensity": intensity,
            "extraDivisor": bool(read(sp + flag_arg, 1)),
            "sourceVertices": vertices(read(sp + source_arg)),
            "destinationVertices": points,
            "intensityBlend" if blended else "keyedCopy": True,
        }
        surface = (destination, source)
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
        y = (reg(row_destination_register) - read(destination + 0x10)) // stride
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
            dst, src = read(sp + destination_offset), reg(source_register)
            y, x = divmod(dst - read(destination + 0x10), read(destination + 0x14))
            v, u = divmod(src - read(source + 0x10), read(source + 0x14))
            pending = (dst, {"x": x // 2, "y": y, "u": u // 2, "v": v,
                             "aligned": u % 2 == 0 and x % 2 == 0,
                             "source": read(src, 2), "key": read(sp + key_offset, 2),
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
        if read(reg("esp") + remaining_offset) == 1:
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

entry = Entry(0x00422e30 if blended else 0x0051ebd0)
row = Row(0x0043232c if blended else 0x0052ce14)
pixel = Pixel(0x00432338 if blended else 0x0052ce20)
after = After(0x0043238b if blended else 0x0052ce34)
finish = Finish(0x00422e10 if blended else 0x00422a07)
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
