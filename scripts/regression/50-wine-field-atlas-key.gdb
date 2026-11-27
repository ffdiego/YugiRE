set confirm off
set pagination off
set debuginfod enabled off
hbreak *0x00419a11
commands
silent
if *(unsigned int *)($esp + 16) == 256
set $atlas = *(unsigned int *)($ecx + 4)
printf "atk value=%u atlas-vtable=0x%x\n", *(unsigned int *)($esp + 12), *(unsigned int *)$atlas
if *(unsigned int *)$atlas == 0x005dc6ec
printf "atlas format=%u flag78=%u sample=(%d,%d) packed=0x%x\n", *(unsigned int *)($atlas+0x74), *(unsigned char *)($atlas+0x78), *(int *)($atlas+0x5c), *(int *)($atlas+0x60), *(unsigned int *)($atlas+0x64)
end
detach
quit
end
continue
end
continue
