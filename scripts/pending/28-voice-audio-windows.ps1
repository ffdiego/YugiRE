# V18: private Windows audio smoke. No audio bytes exported or uploaded.
[CmdletBinding()]
param(
    [string]$VoiceDat,
    [string[]]$Samples = @('joey_01.wav','attack.wav','card_move.wav'),
    [switch]$SkipPlayback
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (-not $VoiceDat) {
    $candidates = @((Join-Path $repo '../passion/Voice.dat'),(Join-Path $repo 'passion/Voice.dat'))
    $VoiceDat = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if (-not $VoiceDat -or -not (Test-Path -LiteralPath $VoiceDat -PathType Leaf)) {
    throw 'Voice.dat not found. Pass -VoiceDat "C:\path\Voice.dat".'
}
$VoiceDat = (Resolve-Path -LiteralPath $VoiceDat).Path
$project = Join-Path $repo 'src/Joey.App/Joey.App.csproj'
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$out = Join-Path $repo "scratch/voice-smoke-$stamp"
New-Item -ItemType Directory -Path $out | Out-Null
$results = @()
function Invoke-Check([string]$Label,[string[]]$DotnetArgs,[int]$Expected) {
    Write-Host "=== $Label ==="
    $old = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet @DotnetArgs 2>&1 | Tee-Object -FilePath (Join-Path $out "$Label.log") | Out-Host
        $exit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $old }
    return [pscustomobject]@{ Check=$Label; ExitCode=$exit; Expected=$Expected; Passed=($exit -eq $Expected) }
}
Push-Location $repo
try {
    $revision = & git rev-parse HEAD
    $version = & dotnet --version
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $VoiceDat).Hash
    $results += Invoke-Check 'build' @('build',$project) 0
    if (-not $results[-1].Passed) { throw 'Build failed; retain logs.' }
    $common = @('run','--project',$project,'--no-build','--',"--voice-dat=$VoiceDat")
    $metadata = Join-Path $repo 'scratch/visual-review/voice-metadata.json'
    $started = [DateTime]::UtcNow
    $results += Invoke-Check 'inspect' ($common+@('--inspect-voice-dat')) 0
    if ((Test-Path -LiteralPath $metadata) -and
        (Get-Item -LiteralPath $metadata).LastWriteTimeUtc -ge $started.AddSeconds(-2)) {
        Copy-Item -LiteralPath $metadata -Destination (Join-Path $out 'voice-metadata.json')
    }
    $results += Invoke-Check 'missing-resource' ($common+@('--review-voice=__missing_v18__.wav')) 1
    $results += Invoke-Check 'mute-rejection' ($common+@('--review-voice=joey_01.wav','--mute-joey')) 1
    if (-not $SkipPlayback) {
        foreach ($sample in $Samples) {
            $safe = [IO.Path]::GetFileNameWithoutExtension($sample)
            if ($sample -notmatch '^[a-zA-Z0-9_]+\.wav$') { throw "Invalid sample: $sample" }
            $results += Invoke-Check "play-$safe" ($common+@("--review-voice=$sample")) 0
        }
    }
} finally {
    Pop-Location
    [pscustomobject]@{
        Revision=$revision; Dotnet=$version; VoiceDatSha256=$hash
        PlaybackSkipped=[bool]$SkipPlayback; Checks=$results
        # Successful process exit is NOT proof that sound was audible.
        ManualListening='pending'
    } | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 (Join-Path $out 'summary.json')
    @(
        'V18 manual listening (complete before sharing)'
        "Playback skipped: $([bool]$SkipPlayback)"
        'For each sample: audible? correct voice/effect? starts once? ends cleanly? clipping/silence?'
        'joey_01.wav: '
        'attack.wav: '
        'card_move.wav: '
        'Other samples / device / observations: '
    ) | Set-Content -Encoding UTF8 (Join-Path $out 'listening-notes.txt')
    Write-Host "Private results: $out"
    Write-Host 'Fill listening-notes.txt and send the metadata/summary/logs and notes only; never Voice.dat or WAVs.'
}
if ($results | Where-Object { -not $_.Passed }) { exit 1 }
