set confirm off
set pagination off
set debuginfod enabled off
set $single = 0
set $single_done = 0
set $dual = 0
set $dual_done = 0

hbreak *0x0041797d
commands
silent
set $secondary = *(unsigned int *)($edi + 0x18)
if $single == 0 && $secondary == 0
set $single = (unsigned int)$edi
printf "single-start counter=%u audio-enabled=%u primary=0x%08x\n", *(unsigned int *)($edi + 0x10), *(unsigned char *)0x00a53878, *(unsigned int *)$esp
end
if $dual == 0 && $secondary != 0
set $dual = (unsigned int)$edi
set $expected = $secondary
printf "dual-start counter=%u audio-enabled=%u primary=0x%08x secondary=0x%08x\n", *(unsigned int *)($edi + 0x10), *(unsigned char *)0x00a53878, *(unsigned int *)$esp, $secondary
end
continue
end

hbreak *0x004179a5
commands
silent
if $dual != 0 && $dual_done == 0 && (unsigned int)$edi == $dual
printf "primary-status counter=%u result=%u\n", *(unsigned int *)($edi + 0x10), (unsigned int)$eax & 0xff
end
continue
end

hbreak *0x004179bb
commands
silent
if $dual != 0 && $dual_done == 0 && (unsigned int)$edi == $dual
printf "secondary-request counter=%u request=0x%08x expected=0x%08x\n", *(unsigned int *)($edi + 0x10), *(unsigned int *)$esp, $expected
end
continue
end

hbreak *0x004179f0
commands
silent
if $single != 0 && $single_done == 0 && (unsigned int)$edi == $single
set $single_done = 1
printf "single-end counter=%u active=%u\n", *(unsigned int *)($edi + 0x10), *(unsigned char *)($edi + 4)
end
if $dual != 0 && $dual_done == 0 && (unsigned int)$edi == $dual
set $dual_done = 1
printf "dual-end counter=%u active=%u\n", *(unsigned int *)($edi + 0x10), *(unsigned char *)($edi + 4)
end
if $single_done && $dual_done
detach
quit
end
continue
end
continue
