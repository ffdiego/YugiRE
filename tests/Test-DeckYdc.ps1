$ErrorActionPreference='Stop'
Import-Module "$PSScriptRoot\..\scripts\DeckYdc.psm1" -Force
# 8-byte header; 2 IDs (0,2037); 1 ID (812); empty third list.
[byte[]]$fixture=[Convert]::FromHexString('0100000091B6B15402000000F50701002C030000')
$parsed=Read-DeckYdc $fixture
if($parsed.consumed -ne 20 -or $parsed.groups[0].ids[0] -ne 0 -or $parsed.groups[0].ids[1] -ne 2037 -or $parsed.groups[1].ids[0] -ne 812 -or $parsed.groups[2].count -ne 0){throw 'Fixture mismatch'}
function Reject([byte[]]$Data){$rejected=$false;try{$null=Read-DeckYdc $Data}catch{$rejected=$true};if(!$rejected){throw 'Expected rejection'}}
Reject $fixture[0..18]
Reject ([byte[]]@($fixture+0))
$bad=$fixture.Clone();$bad[8]=81;Reject $bad
$bad=$fixture.Clone();$bad[14]=16;Reject $bad
Reject ([byte[]]@(0,1,2))
'PASS: zero ID, extended ID, list offsets, empty list, truncation, trailing data, capacity limits'
