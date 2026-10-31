# Reference source for patch_all_cards.py STUB_PREFIX; GNU as --32.
# Fixed original image base: relocations stripped, DllCharacteristics = 0.
.intel_syntax noprefix
.code32
.global _start
_start:
    pushfd
    pushad
    xor ebp, ebp
    mov esi, 1
again:
    lea edi, [esi*2 + 0xa53ccc]
    push esi
    mov eax, 0x4024e0       # card_pack accessor, caller cleans argument
    call eax
    add esp, 4
    test al, 7             # Union of Yugi, Kaiba, Joey playable catalog
    jz count
    cmp byte ptr [edi], 3
    jae count
    mov byte ptr [edi], 3  # High flags byte and quantities above 3 survive
count:
    movzx eax, byte ptr [edi]
    add ebp, eax
    inc esi
    cmp esi, 1115
    jl again
    mov word ptr [0xa53cca], bp
    mov byte ptr [0xa54f42], 1
    popad
    popfd
# Python appends exact displaced bytes 51 53 55 33 DB, then a relative jump
# back to 005BEA85. The original routine continues normal deck validation.
