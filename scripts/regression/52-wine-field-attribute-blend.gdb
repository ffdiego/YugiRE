set confirm off
set pagination off
set debuginfod enabled off
set $pending = 0
set $seen = 0

# Match return addresses in the enclosing numeric and separator helpers.
hbreak *0x004805c6
commands
silent
set $caller = *(unsigned int *)($esp + 0x44)
if $caller == 0x00419734 && ($seen & 1) == 0
set $pending = 1
end
if $caller == 0x00419a16 && ($seen & 4) == 0
set $pending = 4
end
if $pending != 0
set $dst = (unsigned int)$ebp
set $src = *(unsigned int *)$esp
end
continue
end

hbreak *0x004803a1
commands
silent
if *(unsigned int *)($esp + 0x3c) == 0x00419880 && ($seen & 2) == 0
set $pending = 2
set $dst = (unsigned int)$ecx
set $src = *(unsigned int *)$esp
end
continue
end

# The RGB565 rectangular branch at 004CB251 calls this kernel.
hbreak *0x004df170
commands
silent
if $pending && *(unsigned int *)$esp == 0x004cb256 && *(unsigned int *)($esp+8) == $src+0xc && *(unsigned int *)($esp+16) == $dst+0xc
printf "glyph pass=%u src-format=%u dst-format=%u key=0x%04x src-weight=%u dst-weight=%u\n", $pending, *(unsigned int *)($src+0x74), *(unsigned int *)($dst+0x74), *(unsigned short *)($esp+20), *(unsigned int *)($esp+24), *(unsigned int *)($esp+28)
set $seen = $seen | $pending
set $pending = 0
if $seen == 7
detach
quit
end
end
continue
end
continue
