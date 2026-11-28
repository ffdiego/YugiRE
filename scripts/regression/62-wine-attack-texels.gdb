set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

capture = None
active_row = None
samples = []
clip_requested = gdb.convenience_variable("packed_bottom_clip")
bottom_clip = clip_requested is not None and bool(int(clip_requested))
requested = gdb.convenience_variable("packed_intensity")
intensity = (32 if bottom_clip else 224) if requested is None else int(requested)
if intensity not in range(32, 257, 32):
    raise ValueError("Choose an impact intensity from 32 to 256 in steps of 32")
if bottom_clip and intensity != 32:
    raise ValueError("The bottom clip sample uses the last seven rows of an intensity-32 impact")
base = intensity == 256
step = 5 if base else 7

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4, signed=False):
    data = gdb.selected_inferior().read_memory(address, size)
    return int.from_bytes(data, "little", signed=signed)

def vertices(address):
    return [[read(address + 8*i + axis, signed=True) for axis in (0, 4)] for i in range(4)]

def thread():
    return gdb.selected_thread().num

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Impact(HardwareProbe):
    def stop(self):
        global capture
        sp = reg("esp")
        if capture:
            return False
        # Return PCs in the wrappers and the battle impact draw.
        if base:
            if (read(sp), read(sp + 0x74), read(sp + 0xf0)) != (0x00422a07, 0x00422c3c, 0x00414777):
                return False
            if read(sp + 0x110) != intensity or read(sp + 0x18) != 4:
                return False
            destination, source = reg("ecx"), read(sp + 4)
            source_offset, destination_offset, flag_offset = 8, 0xc, 0x14
        else:
            if read(sp) != 0x00422e10 or read(sp + 0x7c) != 0x00414777:
                return False
            if read(sp + 0x0c) != intensity or read(sp + 0x20) != 4:
                return False
            destination, source = read(sp + 4), read(sp + 8)
            source_offset, destination_offset, flag_offset = 0x10, 0x14, 0x1c
        if read(destination + 0x74) != 3 or read(source + 0x74) != 10:
            return False
        points = vertices(read(sp + destination_offset))
        if bottom_clip:
            clip = read(sp + flag_offset - 4) or destination + 0x18
            if [read(clip + axis) for axis in (0, 4, 8, 12)] != [0, 0, 800, 600]:
                return False
            xs = {x for x, _ in points}
            ys = {y for _, y in points}
            # Seven visible rows must supply all 144 samples, including Y=599.
            if len(xs) != 2 or len(ys) != 2 or min(ys) > 593 or max(ys) < 600:
                return False
            if min(xs) < 0 or max(xs) >= 800 or (max(xs) - min(xs)) // step != 21:
                return False
        capture = {
            "thread": thread(),
            "intensity": intensity,
            "extraDivisor": bool(read(sp + flag_offset, 1)),
            "sourceVertices": vertices(read(sp + source_offset)),
            "destinationVertices": points,
        }
        return False

class Row(HardwareProbe):
    def stop(self):
        global active_row
        if capture is None or thread() != capture["thread"]:
            return False
        y = read(reg("esp") + 0x34, signed=True)
        pixel.enabled = 593 <= y < 600 if bottom_clip else y % step == 0
        if pixel.enabled:
            active_row = {"x": reg("ecx"), "y": y, "count": reg("eax"), "i": 0}
        return False

class Pixel(HardwareProbe):
    def stop(self):
        if capture is None or thread() != capture["thread"]:
            return False
        sp = reg("esp")
        if active_row["i"] % step == 0:
            # One stacked argument to the base helper, two to the intensity helper.
            address = read(sp if base else sp + 4)
            source_base = read(sp + (0xac if base else 0xb4))
            source_pitch = read(sp + (0x58 if base else 0x38))
            v, byte_x = divmod(address - source_base, source_pitch)
            samples.append({"x": active_row["x"] + active_row["i"], "y": active_row["y"],
                            "u": byte_x // 4, "v": v, "aligned": byte_x % 4 == 0})
        active_row["i"] += 1
        if active_row["i"] == active_row["count"]:
            self.enabled = False
        return len(samples) >= 144

impact = Impact(0x0051ebd0 if base else 0x00422e30)
row = Row(0x0051f88a if base else 0x00423b99)
pixel = Pixel(0x0051f897 if base else 0x00423bab)
pixel.enabled = False
end
continue
python
if capture and samples:
    print("attack-texel-samples " + json.dumps({**capture, "samples": samples}))
end
detach
quit
