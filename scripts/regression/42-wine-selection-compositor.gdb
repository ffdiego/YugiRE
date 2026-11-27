set confirm off
set pagination off
set debuginfod enabled off
set $tracked = 0
hbreak *0x0043b47e
commands
silent
set $src = *(unsigned int *)$esp
set $dst = (unsigned int)$ecx
printf "selection key=%u x=%d y=%d src-vtable=0x%08x dst-vtable=0x%08x\n", (unsigned int)$edx, *(int *)($esp+4), *(int *)($esp+8), *(unsigned int *)$src, *(unsigned int *)$dst
if *(unsigned int *)$src == 0x005dc6ec
printf "source format=%u flag78=%u key-origin=%u sample=(%d,%d) packed=0x%x\n", *(unsigned int *)($src+0x74), *(unsigned char *)($src+0x78), *(unsigned char *)($src+0x55), *(int *)($src+0x5c), *(int *)($src+0x60), *(unsigned int *)($src+0x64)
end
if *(unsigned int *)$dst == 0x005dc6ec
printf "destination format=%u\n", *(unsigned int *)($dst+0x74)
end
set $tracked = 1
disable 1
enable 2 3 4
continue
end
hbreak *0x004b1760
commands
silent
if *(unsigned int *)($esp+4) == $src && (unsigned int)$ecx == $dst
printf "keyed compositor 004B1760\n"
end
continue
end
hbreak *0x004b2d29
commands
silent
if (unsigned int)$ecx == $src
printf "RGB565 key query 004B2D29 surface+64=0x%x\n", *(unsigned int *)($src+0x64)
end
continue
end
hbreak *0x0043b481
commands
silent
if $tracked
printf "selection call returned eax=0x%x\n", (unsigned int)$eax
detach
quit
end
continue
end
disable 2 3 4
continue
