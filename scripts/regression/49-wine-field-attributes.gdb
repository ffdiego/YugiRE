set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json
import time

DURATION_NS = 55_000_000_000
started = None
pending = {}
entries = {}

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little")

def thread():
    return gdb.selected_thread().num

def submit(frame):
    if not frame:
        return
    key = json.dumps(frame, sort_keys=True)
    if key in entries:
        entries[key]["occurrences"] += 1
    else:
        entries[key] = {"occurrences": 1, **frame}

class HardwareProbe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Attributes(HardwareProbe):
    def stop(self):
        global started
        now = time.monotonic_ns()
        if started is None:
            started = now
        current = thread()
        submit(pending.pop(current, None))
        if now - started >= DURATION_NS:
            return True
        obj = reg("ecx")
        descriptor = read(obj + 0x24, 2)
        if descriptor & 0x3e:
            return False
        pending[current] = {
            "object": obj, "descriptor": descriptor, "perspective": read(0x00A54E5C, 1) & 1,
            "card": read(obj + 0x10, 2), "center": [read(obj + 0x30), read(obj + 0x34)],
            "passes": [],
        }
        return False

class Glyphs(HardwareProbe):
    def __init__(self, address, kind):
        super().__init__(address)
        self.kind = kind

    def stop(self):
        frame = pending.get(thread())
        if frame is None or frame["object"] != reg("edi"):
            return False
        sp = reg("esp")
        font = reg("ecx")
        atlas = read(font + 4)
        manager = read(0x005F33EC)
        bank = read(manager + 0x60)
        index = [i for i in range(1, 4) if atlas and read(bank + 8 * i + 4) == atlas]
        frame["passes"].append({
            "kind": self.kind, "atlas": index, "columns": read(font + 8),
            "right": read(sp + 4), "top": read(sp + 8), "value": read(sp + 12),
            "intensity": read(sp + 16),
        })
        return False

probes = [
    Attributes(0x004194D0),
    Glyphs(0x0041972F, "def"),
    Glyphs(0x0041987B, "separator"),
    Glyphs(0x00419A11, "atk"),
]
end
continue
python
if entries:
    print("field-attributes " + json.dumps({"entries": list(entries.values())}))
end
detach
quit
