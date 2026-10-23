[CmdletBinding()]
param([Parameter(Mandatory)][string]$SourcePath,[Parameter(Mandatory)][ValidateSet('before','after')][string]$Phase)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot)).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
$source = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $SourcePath).Path).TrimEnd([IO.Path]::DirectorySeparatorChar,[IO.Path]::AltDirectorySeparatorChar)
if ($root.Equals($source,[StringComparison]::OrdinalIgnoreCase) -or
    $root.StartsWith($source+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
    $source.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe source/target overlap.' }
$tracked = & git --no-optional-locks -C $source ls-files
if ($LASTEXITCODE -ne 0) { throw 'Source is not an available Git repository.' }
$entries = foreach ($relative in $tracked) {
    $full = Join-Path $source $relative
    if (Test-Path -LiteralPath $full -PathType Leaf) {
        [ordered]@{ path=$relative; sha256=(Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash }
    }
}
$snapshot = [ordered]@{
    capturedUtc=[DateTime]::UtcNow.ToString('O'); source=$source; target=$root
    commit=(& git --no-optional-locks -C $source rev-parse HEAD)
    status=@(& git --no-optional-locks -C $source status --porcelain=v1 --untracked-files=all)
    files=@($entries)
}
$output = "$root/docs/evidence/AZ-001-source-$Phase.json"
if ($Phase -eq 'before' -and (Test-Path -LiteralPath $output)) { throw 'Initial source snapshot already exists; preserve it.' }
$snapshot | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $output
if ($Phase -eq 'after') {
    $before = Get-Content -Raw "$root/docs/evidence/AZ-001-source-before.json" | ConvertFrom-Json
    $changes = @(Compare-Object ($before.files | ForEach-Object {"$($_.path):$($_.sha256)"}) ($entries | ForEach-Object {"$($_.path):$($_.sha256)"}))
    $sameStatus = ($before.status -join "`n") -ceq ($snapshot.status -join "`n")
    if ($changes.Count -or !$sameStatus -or $before.commit -ne $snapshot.commit) {
        Write-Warning 'Source changed externally during the task. Inspect differences; do not reset or attribute changes without evidence.'
        exit 1
    }
    Write-Host "Source unchanged: commit, status and $($entries.Count) tracked content hashes match."
} else { Write-Host "Recorded $($entries.Count) source content hashes read-only." }
