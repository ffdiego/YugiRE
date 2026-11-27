set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

capture = None
active_row = None

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def memory(address, length):
    return bytes(gdb.selected_inferior().read_memory(address, length))

def read(address, signed=False):
    return int.from_bytes(memory(address, 4), "little", signed=signed)

def ints(address, count):
    return [read(address+4*i, signed=True) for i in range(count)]

def thread():
    return gdb.selected_thread().num

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Impact(HardwareProbe):
    def stop(self):
        global capture
        if capture is not None:
            return False
        sp = reg("esp")
        if read(sp) != 0x00422e10 or read(sp+12) != 32 or read(sp+32) != 4:
            return False
        dst, src = read(sp+4), read(sp+8)
        if read(dst+0x74) != 3 or read(src+0x74) != 10:
            return False
        bounds = ints(dst+0x18, 4)
        points = ints(read(sp+20), 8)
        top = min(points[1::2])
        bottom = max(points[1::2])
        if top >= bounds[1] and bottom < bounds[3]:
            return False
        capture = {"thread":thread(), "destination":dst, "source":src, "bounds":bounds,
                   "quad": list(zip(points[::2], points[1::2])), "rows":[]}
        return False

class BeforeSpan(HardwareProbe):
    def stop(self):
        global active_row
        if capture is None or capture["thread"] != thread():
            return False
        sp = reg("esp")
        y = read(sp+0x34, signed=True)
        x = reg("ecx")
        length = reg("eax")
        ptr = reg("edx")
        if not 1 <= length <= 800:
            raise ValueError(f"Unexpected span length {length}")
        stride = read(capture["destination"]+0x14, signed=True)
        offset = ptr - read(capture["destination"]+0x10)
        clip = ints(read(sp+0x64), 4)
        active_row = (ptr, memory(ptr, 2*length),
                      {"y":y, "x":x, "length":length, "offset":offset, "stride":stride, "clip":clip})
        return False

class AfterSpan(HardwareProbe):
    def stop(self):
        global active_row
        if active_row is None or capture["thread"] != thread():
            return False
        ptr, before, row = active_row
        after = memory(ptr, len(before))
        row["changed"] = sum(before[i:i+2] != after[i:i+2] for i in range(0, len(before), 2))
        capture["rows"].append(row)
        active_row = None
        return False

class Return(HardwareProbe):
    def stop(self):
        if capture is None or capture["thread"] != thread():
            return False
        capture["result"] = reg("eax")
        return True

probes = [Impact(0x00422e30), BeforeSpan(0x00423b99), AfterSpan(0x00423bf6), Return(0x00422e10)]
end
continue
python
if capture and "result" in capture:
    print("attack-impact-clip " + json.dumps(capture))
end
detach
quit
