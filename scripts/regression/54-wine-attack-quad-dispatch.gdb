set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

SITES = {
    0x004146EA: ("flight", 0),
    0x00414777: ("impact", 2),
    0x00414805: ("partial-sword", 1),
    0x004148FA: ("aim", 0),
}
samples = []
coefficients = {}
tracked = None
pending = None
aim = None
direct_only = bool(gdb.convenience_variable("attack_direct"))

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, signed=False, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little", signed=signed)

def thread():
    return gdb.selected_thread().num

def quad(pointer):
    return [[read(pointer+i*8, signed=True), read(pointer+i*8+4, signed=True)] for i in range(4)]

def surface(pointer):
    table = read(pointer)
    result = {"vtable": table}
    if table == 0x005dc6ec:
        result.update(format=read(pointer+0x74), key=read(pointer+0x64),
                      keyFromPixel=read(pointer+0x55, size=1) != 0,
                      keyX=read(pointer+0x5c, signed=True),
                      keyY=read(pointer+0x60, signed=True))
    return result

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Request(HardwareProbe):
    def stop(self):
        global tracked, pending, aim
        sp = reg("esp")
        site = SITES.get(read(sp))
        if site is None:
            return False
        kind, key = site
        obj = reg("esi") - 0x38
        if direct_only and read(obj+0x24, signed=True) != -1:
            return False
        count = read(obj+0x14)
        if tracked is None and kind == "flight":
            tracked = {"object": obj, "thread": thread(),
                       "attackerSide": read(obj+0x18), "attackerSlot": read(obj+0x1c, signed=True),
                       "targetSide": read(obj+0x20), "targetSlot": read(obj+0x24, signed=True)}
            if aim is not None and aim[0] == obj and aim[1] == thread():
                samples.append(aim[2])
        if tracked is not None and (obj != tracked["object"] or thread() != tracked["thread"]):
            return False
        dst, src = read(sp+4), read(sp+8)
        bank = read(reg("esi")+0x60)
        row = {"pass": kind, "key": key, "bankMatch": read(bank+key*8+4) == src,
               "state": read(obj+0x10), "counter": count,
               "x": read(sp+12, signed=True), "y": read(sp+16, signed=True),
               "angle": read(sp+20, signed=True), "scale": [read(sp+24), read(sp+28)],
               "intensity": read(sp+32) if kind in ("impact", "partial-sword") else None,
               "source": surface(src), "destination": surface(dst), "compositor": None}
        pending = (src, dst, thread(), row)
        if tracked is None:
            aim = (obj, thread(), row)
        else:
            samples.append(row)
        return False

class Compositor(HardwareProbe):
    def __init__(self, address, name):
        super().__init__(address)
        self.name = name

    def stop(self):
        global pending
        if pending is None or pending[2] != thread():
            return False
        sp = reg("esp")
        caller = read(sp)
        if self.name == "base" and caller == 0x00422A07:
            src, dst = read(sp+4), reg("ecx")
            source_quad, dest_quad, count = read(sp+8), read(sp+12), read(sp+24)
        elif self.name == "intensity" and caller == 0x00422E10:
            src, dst = read(sp+8), read(sp+4)
            source_quad, dest_quad, count = read(sp+16), read(sp+20), read(sp+32)
        else:
            return False
        if (src, dst) != pending[:2]:
            return False
        row = pending[3]
        row["compositor"] = self.name
        if count == 4:
            row["sourceQuad"] = quad(source_quad)
            row["destQuad"] = quad(dest_quad)
            angle = row["angle"] & 511
            if angle not in coefficients:
                coefficients[angle] = [read(0x00a52ee8+angle*4, signed=True),
                                       read(0x00a52ee8+((angle-128)&511)*4, signed=True)]
        pending = None
        return tracked is not None and row["pass"] == "partial-sword" and row["counter"] == 496

probes = [Request(0x00422840), Request(0x00422c00),
          Compositor(0x0051ebd0, "base"), Compositor(0x00422e30, "intensity")]
end
continue
python
if samples:
    print("attack-quad-dispatch " + json.dumps({"tracked": tracked, "samples": samples, "coefficients": coefficients}))
end
detach
quit
