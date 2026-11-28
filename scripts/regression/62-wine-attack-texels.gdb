set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

capture = None
active_row = None
samples = []
requested = gdb.convenience_variable("packed_intensity")
intensity = 224 if requested is None else int(requested)
if intensity not in range(32, 256, 32):
    raise ValueError("Choose an impact intensity from 32 to 224 in steps of 32")

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
        # Return PCs in the wrapper and the battle impact draw.
        if capture or read(sp) != 0x00422e10 or read(sp + 0x7c) != 0x00414777:
            return False
        if read(sp + 0x0c) != intensity or read(sp + 0x20) != 4:
            return False
        destination, source = read(sp + 4), read(sp + 8)
        if read(destination + 0x74) != 3 or read(source + 0x74) != 10:
            return False
        capture = {
            "thread": thread(),
            "intensity": intensity,
            "extraDivisor": bool(read(sp + 0x1c, 1)),
            "sourceVertices": vertices(read(sp + 0x10)),
            "destinationVertices": vertices(read(sp + 0x14)),
        }
        return False

class Row(HardwareProbe):
    def stop(self):
        global active_row
        if capture is None or thread() != capture["thread"]:
            return False
        y = read(reg("esp") + 0x34, signed=True)
        pixel.enabled = y % 7 == 0
        if pixel.enabled:
            active_row = {"x": reg("ecx"), "y": y, "count": reg("eax"), "i": 0}
        return False

class Pixel(HardwareProbe):
    def stop(self):
        if capture is None or thread() != capture["thread"]:
            return False
        sp = reg("esp")
        if active_row["i"] % 7 == 0:
            # At the call site, the stack includes the two arguments to 00439B60.
            source_base = read(sp + 0xb4)
            source_pitch = read(sp + 0x38)
            v, byte_x = divmod(read(sp + 4) - source_base, source_pitch)
            samples.append({"x": active_row["x"] + active_row["i"], "y": active_row["y"],
                            "u": byte_x // 4, "v": v, "aligned": byte_x % 4 == 0})
        active_row["i"] += 1
        if active_row["i"] == active_row["count"]:
            self.enabled = False
        return len(samples) >= 144

impact = Impact(0x00422e30)
row = Row(0x00423b99)
pixel = Pixel(0x00423bab)
pixel.enabled = False
end
continue
python
if capture and samples:
    print("attack-texel-samples " + json.dumps({**capture, "samples": samples}))
end
detach
quit
