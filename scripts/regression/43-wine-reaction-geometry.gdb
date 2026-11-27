set confirm off
set pagination off
set debuginfod enabled off
set $reaction = 0
set $draws = 0
hbreak *0x00416d47
commands
silent
set $candidate = (unsigned int)$edi
set $counter = *(unsigned int *)($candidate + 0x10)
if $reaction == 0 && $counter == 8
set $reaction = $candidate
set $src = *(unsigned int *)$esp
set $dst = (unsigned int)$ecx
set $src_quad = *(int **)($esp + 4)
set $dst_quad = *(int **)($esp + 8)
printf "reaction base count=%u scratch=%u source-vtable=0x%08x destination-vtable=0x%08x\n", *(unsigned int *)($esp + 20), *(unsigned char *)($reaction + 0x1c), *(unsigned int *)$src, *(unsigned int *)$dst
if *(unsigned int *)$src == 0x005dc6ec
printf "source-format=%u\n", *(unsigned int *)($src + 0x74)
end
if *(unsigned int *)$dst == 0x005dc6ec
printf "destination-format=%u\n", *(unsigned int *)($dst + 0x74)
end
set $i = 0
while $i < 8
printf "vertex %u source=(%d,%d) destination=(%d,%d)\n", $i / 2, $src_quad[$i], $src_quad[$i + 1], $dst_quad[$i], $dst_quad[$i + 1]
set $i = $i + 2
end
end
if $reaction != 0 && $candidate == $reaction
set $draws = $draws + 1
printf "base-draw counter=%u primary=0x%08x secondary=0x%08x\n", $counter, *(unsigned int *)($reaction + 0x14), *(unsigned int *)($reaction + 0x18)
end
continue
end
hbreak *0x004179f0
commands
silent
if $reaction != 0 && (unsigned int)$edi == $reaction
printf "reaction-end counter=%u active=%u base-draws=%u\n", *(unsigned int *)($reaction + 0x10), *(unsigned char *)($reaction + 0x04), $draws
detach
quit
end
continue
end
continue
