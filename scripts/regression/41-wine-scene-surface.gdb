set confirm off
set pagination off
set debuginfod enabled off

# Original PE entrypoint 0044DD00, i386, image base 00400000.
hbreak *0x0044dd00
set $samples = 0
commands
silent
set $surface = *(unsigned int *)($esp + 4)
set $vtable = *(unsigned int *)$surface
printf "scene surface-vtable=0x%08x", $vtable
if $vtable == 0x005dc6ec
printf " format=%u flag78=%u", *(unsigned int *)($surface+0x74), *(unsigned char *)($surface+0x78)
end
printf "\n"
set $samples = $samples + 1
if $samples >= 3
detach
quit
end
continue
end
continue
