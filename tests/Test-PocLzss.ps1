$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot\..\scripts\PocLzss.psm1" -Force
function Expect([byte[]]$Source,[int]$Size,[string]$Hex,[int]$Consumed) {
    $result=Expand-PocLzss -InputBytes $Source -OutputSize $Size
    if([Convert]::ToHexString($result.bytes) -cne $Hex -or $result.consumed -ne $Consumed){throw "Unexpected decode for $Hex"}
}
function Reject([byte[]]$Source,[int]$Size) {
    $rejected=$false
    try{$null=Expand-PocLzss -InputBytes $Source -OutputSize $Size}catch{$rejected=$true}
    if(!$rejected){throw 'Expected truncated stream rejection'}
}
Expect @() 0 '' 0
Expect @(7,65,66,67) 3 '414243' 4
# Three literals at dictionary FEE, FEF, FF0; overlapping copy grows ABCABCABC.
Expect @(7,65,66,67,0xee,0xf3) 9 '414243414243414243' 6
# Reference to initial zero-filled dictionary; final token is clipped to output size.
Expect @(0,0,0) 3 '000000' 3
Expect @(0,0,15) 2 '0000' 3
# Eight literal bits exhaust a flag; ninth literal needs a new flag byte.
Expect @(255,65,66,67,68,69,70,71,72,1,73) 9 '414243444546474849' 11
Reject @() 1
Reject @(1) 1
Reject @(0,0) 3
$trailing=Expand-PocLzss -InputBytes @(1,65,99) -OutputSize 1
if($trailing.trailing_bytes -ne 1){throw 'Trailing byte accounting failed'}
'PASS: empty, literals, overlapping reference, zero dictionary, clipped final token, flag reload, truncation, trailing bytes'
