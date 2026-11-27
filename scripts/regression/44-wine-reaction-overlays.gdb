set confirm off
set pagination off
set debuginfod enabled off
set $reaction = 0
set $dispatches = 0
set $layers = 0

# Two group headers may enter this dispatch on the same Draw.
hbreak *0x00416d79
commands
silent
set $obj = (unsigned int)$edi
set $counter = *(unsigned int *)($obj + 0x10)
set $group = *(unsigned int *)($esp + 0x1c)
if $reaction == 0 && $counter == 8 && $group == 6 && (int)$eax == 1
set $reaction = $obj
end
if $reaction != 0 && $obj == $reaction
set $header = (int)$eax
set $dispatches = $dispatches + 1
printf "dispatch counter=%u group=%u header=%d state=0x%08x\n", $counter, $group, $header, *(unsigned int *)0x005f2fb4
end
continue
end

# Both call sites have the source surface and destination coordinates on the stack.
hbreak *0x00416e55
commands
silent
if $reaction != 0 && (unsigned int)$edi == $reaction
set $layers = $layers + 1
printf "layer counter=%u group=%u selected=%u slot=4c x=%d y=%d\n", *(unsigned int *)($reaction + 0x10), *(unsigned int *)($esp + 0x34), *(unsigned int *)($esp + 0x28), *(int *)($esp + 4), *(int *)($esp + 8)
end
continue
end
hbreak *0x00416ede
commands
silent
if $reaction != 0 && (unsigned int)$edi == $reaction
set $layers = $layers + 1
printf "layer counter=%u group=%u selected=%u slot=0c x=%d y=%d\n", *(unsigned int *)($reaction + 0x10), *(unsigned int *)($esp + 0x34), *(unsigned int *)($esp + 0x28), *(int *)($esp + 4), *(int *)($esp + 8)
end
continue
end
hbreak *0x004179f0
commands
silent
if $reaction != 0 && (unsigned int)$edi == $reaction
printf "reaction-end counter=%u active=%u dispatches=%u layers=%u\n", *(unsigned int *)($reaction + 0x10), *(unsigned char *)($reaction + 0x04), $dispatches, $layers
detach
quit
end
continue
end
continue
