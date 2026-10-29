function Expand-PocLzss {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][byte[]]$InputBytes,
        [Parameter(Mandatory)][ValidateRange(0,16777216)][int]$OutputSize
    )
    $dictionary=New-Object byte[] 4096
    $output=New-Object byte[] $OutputSize
    $source=0; $target=0; $write=0xfee; $flags=0
    $literals=0; $references=0; $referenceBytes=0
    while($target -lt $OutputSize) {
        $flags=$flags -shr 1
        if(($flags -band 0x100) -eq 0) {
            if($source -ge $InputBytes.Length){throw 'Truncated flag byte'}
            $flags=0xff00 -bor [int]$InputBytes[$source++]
        }
        if($flags -band 1) {
            if($source -ge $InputBytes.Length){throw 'Truncated literal'}
            $value=$InputBytes[$source++]
            $output[$target++]=$value
            $dictionary[$write]=$value
            $write=($write+1) -band 0xfff
            $literals++
        } else {
            if($source+2 -gt $InputBytes.Length){throw 'Truncated back-reference'}
            $low=[int]$InputBytes[$source++]; $high=[int]$InputBytes[$source++]
            $read=$low -bor (($high -band 0xf0) -shl 4)
            $length=($high -band 15)+3
            $references++
            for($i=0;$i -lt $length -and $target -lt $OutputSize;$i++) {
                $value=$dictionary[$read]
                $read=($read+1) -band 0xfff
                $output[$target++]=$value
                $dictionary[$write]=$value
                $write=($write+1) -band 0xfff
                $referenceBytes++
            }
        }
    }
    [pscustomobject]@{bytes=$output;consumed=$source;produced=$target;trailing_bytes=($InputBytes.Length-$source);literals=$literals;references=$references;reference_bytes=$referenceBytes}
}
Export-ModuleMember -Function Expand-PocLzss
