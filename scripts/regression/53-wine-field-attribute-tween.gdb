set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

samples = []
tracked = None
sample = None

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little")

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class BeforeDraw(HardwareProbe):
    def stop(self):
        global tracked, sample
        obj = reg("edi")
        if tracked is not None and tracked["object"] != obj:
            return False
        stack = reg("esp")
        before = [read(obj + 0x4c), read(obj + 0x50)]
        target = [read(stack + 0x30), read(stack + 0x34)]
        if tracked is None and before == target:
            return False
        if tracked is None:
            tracked = {"object": obj, "identity": read(obj + 0x10, 2),
                       "descriptor": read(obj + 0x24, 2), "perspective": read(0x00a54e5c, 1) & 1}
            print("attribute-change " + json.dumps({"before": before, "target": target, "tracked": tracked}))
        sample = {"before": before, "target": target, "loopCounter": read(0x005f2fb4)}
        return False

class Glyphs(HardwareProbe):
    def __init__(self, address, kind):
        super().__init__(address)
        self.kind = kind

    def stop(self):
        if sample is None or tracked["object"] != reg("edi"):
            return False
        atlas = read(reg("ecx") + 4)
        bank = read(read(0x005f33ec) + 0x60)
        sample[self.kind] = {"value": read(reg("esp") + 12),
                             "atlas": [i for i in range(1, 4) if atlas and read(bank + 8*i + 4) == atlas]}
        if self.kind == "atk" and "def" in sample:
            samples.append(sample)
            shown = [sample["atk"]["value"], sample["def"]["value"]]
            return shown == sample["target"] or len(samples) >= 128
        return False

probes = [BeforeDraw(0x00419570), Glyphs(0x0041972f, "def"), Glyphs(0x00419a11, "atk")]
end
continue
python
if samples:
    print("attribute-tween " + json.dumps({"tracked": tracked, "samples": samples}))
end
detach
quit
