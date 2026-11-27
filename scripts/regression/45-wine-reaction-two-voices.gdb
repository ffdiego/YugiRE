set confirm off
set pagination off
set debuginfod enabled off
set $reaction = 0
set $promotions = 0

hbreak *0x0041797d
commands
silent
set $primary = *(unsigned int *)($edi + 0x14)
set $secondary = *(unsigned int *)($edi + 0x18)
printf "first counter=%u primary=0x%08x secondary=0x%08x request=0x%08x\n", *(unsigned int *)($edi + 0x10), $primary, $secondary, *(unsigned int *)$esp
if $reaction == 0 && $secondary != 0
set $reaction = (unsigned int)$edi
set $expected = $secondary
end
continue
end

hbreak *0x004179a5
commands
silent
if $reaction != 0 && (unsigned int)$edi == $reaction
printf "primary-status counter=%u result=%u\n", *(unsigned int *)($reaction + 0x10), (unsigned int)$eax & 0xff
end
continue
end

hbreak *0x004179bb
commands
silent
if $reaction != 0 && (unsigned int)$edi == $reaction
set $promotions = $promotions + 1
printf "second counter=%u primary=0x%08x secondary=0x%08x request=0x%08x expected=0x%08x active=%u\n", *(unsigned int *)($reaction + 0x10), *(unsigned int *)($reaction + 0x14), *(unsigned int *)($reaction + 0x18), *(unsigned int *)$esp, $expected, *(unsigned char *)($reaction + 0x04)
end
continue
end

hbreak *0x004179f0
commands
silent
if $reaction != 0 && (unsigned int)$edi == $reaction
printf "reaction-end counter=%u active=%u promotions=%u primary=0x%08x secondary=0x%08x\n", *(unsigned int *)($reaction + 0x10), *(unsigned char *)($reaction + 0x04), $promotions, *(unsigned int *)($reaction + 0x14), *(unsigned int *)($reaction + 0x18)
detach
quit
end
continue
end
continue
