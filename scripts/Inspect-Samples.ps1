param([string]$GameDirectory='Z:\YugiRE\passion', [string]$OutputDirectory='Z:\YugiRE\YugiRE\docs\evidence')
$ErrorActionPreference='Stop'
$samples=foreach($name in 'data.dat','Voice.dat') {
    $f=[IO.File]::OpenRead((Join-Path $GameDirectory $name))
    try {
        foreach($offset in 0x100,0x200,0x400,0x800,0x1000) {
            $f.Position=$offset; $buffer=New-Object byte[] 96
            $count=$f.Read($buffer,0,$buffer.Length)
            [ordered]@{file=$name;offset=$offset;hex=[Convert]::ToHexString($buffer,0,$count)}
        }
    } finally {$f.Dispose()}
}
$samples | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'archive-samples.json') -Encoding utf8
