set confirm off
set pagination off
set debuginfod enabled off
python
import gdb
import json

coin = None
updates = []
draws = []

def reg(name):
    return int(gdb.parse_and_eval("$" + name)) & 0xffffffff

def read(address, size=4, signed=False):
    return int.from_bytes(gdb.selected_inferior().read_memory(address, size), "little", signed=signed)

class Probe(gdb.Breakpoint):
    def __init__(self, address):
        super().__init__(f"*{address:#x}", type=gdb.BP_HARDWARE_BREAKPOINT, internal=True)

class Producer(Probe):
    def stop(self):
        global coin
        if coin is None:
            args = [read(reg("esp") + 4*i) for i in range(4)]
            coin = {"args": args, "thread": gdb.selected_thread().num,
                    "mode": read(0x005f3294), "orientation": read(0x00a54e5c, 1) & 1}
            print("coin-queued " + json.dumps(coin), flush=True)
        return False

def belongs(obj):
    return (coin is not None and coin["thread"] == gdb.selected_thread().num
            and read(obj) == 0x005db404)

class Update(Probe):
    def stop(self):
        obj = reg("ecx")
        if not belongs(obj):
            return False
        counter = read(obj + 0x10, signed=True)
        if not updates:
            coin["count"] = read(obj + 0x14)
        updates.append(counter)
        terminal = (coin["count"] + 4) * 128
        if counter + 8 != terminal:
            return False
        print("coin-card " + json.dumps(dict(
            coin, updates=updates, draws=draws, expected_terminal=terminal)), flush=True)
        return True

class Draw(Probe):
    def stop(self):
        obj = reg("ecx")
        if belongs(obj):
            draws.append(read(obj + 0x10, signed=True))
        return False

probes = [Producer(0x005836cb), Producer(0x00587228), Update(0x00457500), Draw(0x00457520)]
end
continue
detach
quit
