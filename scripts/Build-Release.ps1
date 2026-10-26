[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9.-]{2,63}$')][string]$Version)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
. "$PSScriptRoot/Cloud-Common.ps1"
$root=$script:AzureLabRoot
$release=Get-LabPrivatePath ".local/releases/$Version"
if(Test-Path -LiteralPath $release){throw 'Version already exists; immutable releases are never overwritten.'}
$commit=(& git --no-optional-locks -C $root rev-parse HEAD).Trim()
if($LASTEXITCODE -ne 0 -or $commit -cnotmatch '^[a-f0-9]{40}$'){throw 'Git provenance unavailable.'}
$dirty=@(& git --no-optional-locks -C $root status --porcelain=v1 --untracked-files=all).Count -gt 0
New-Item -ItemType Directory -Path $release | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
Push-Location $root
try {
    & dotnet restore TourLab.Azure.slnx --locked-mode
    if($LASTEXITCODE -ne 0){throw 'Locked restore failed.'}
    & dotnet tool restore
    if($LASTEXITCODE -ne 0){throw 'Pinned EF tool restore failed.'}
    & "$PSScriptRoot/Build-Web.ps1"
    & dotnet build TourLab.Azure.slnx -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Release build failed.'}
    $projects=[ordered]@{gateway='src/Gateway.Api';catalog='src/Catalog.Api';reservations='src/Reservations.Api';payments='src/Payments.Api';notifications='src/Notifications.Api';receipts='src/Receipts.Functions';migrator='tools/Lab.Provisioner'}
    $work=Get-LabPrivatePath ('.local/release-build-'+[guid]::NewGuid().ToString('N'))
    foreach($entry in $projects.GetEnumerator()){
        $out=Join-Path $work $entry.Key
        & dotnet publish $entry.Value -c Release --no-build --no-restore -o $out
        if($LASTEXITCODE -ne 0){throw 'Package publish failed.'}
        if($entry.Key -eq 'gateway' -and !(Test-Path "$out/wwwroot/index.html")){throw 'Gateway SPA missing.'}
        if($entry.Key -eq 'receipts' -and (!(Test-Path "$out/host.json") -or !(Test-Path "$out/worker.config.json") -or !(Test-Path "$out/.azurefunctions"))){throw 'Functions host metadata missing.'}
        if($entry.Key -eq 'receipts' -and (Get-Content -Raw "$out/worker.config.json" | ConvertFrom-Json).description.workerIndexing -ne 'true'){throw 'Functions runtime indexing missing.'}
        $unsafe=@(Get-ChildItem -Recurse -Force -File $out | Where-Object { $_.Name -match '^(local.settings.json|\.env)$|\.(pfx|pem|key)$' -or $_.FullName -match '[\\/]\.local[\\/]' -and !$_.FullName.StartsWith($work,[StringComparison]::OrdinalIgnoreCase) })
        if($unsafe.Count){throw 'Private/local files are forbidden in release packages.'}
        [IO.Compression.ZipFile]::CreateFromDirectory($out,(Join-Path $release ($entry.Key+'.zip')))
    }
    $schemaProjects=[ordered]@{identity='src/Gateway.Api';catalog='src/Catalog.Api';reservations='src/Reservations.Api';payments='src/Payments.Api';notifications='src/Notifications.Api'}
    foreach($entry in $schemaProjects.GetEnumerator()){
        $context=if($entry.Key -eq 'identity'){'IdentityDb'}else{(Get-Culture).TextInfo.ToTitleCase($entry.Key)+'Db'}
        & dotnet ef migrations script --idempotent --project $entry.Value --context $context --configuration Release --no-build --output (Join-Path $release ($entry.Key+'.sql'))
        if($LASTEXITCODE -ne 0){throw 'Idempotent schema migration artifact failed.'}
    }
    $files=@(Get-ChildItem -File $release | Sort-Object Name | ForEach-Object { [ordered]@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length} })
    [ordered]@{schemaVersion=1;version=$Version;commit=$commit;workingTreeDirty=$dirty;createdUtc=[DateTime]::UtcNow.ToString('O');files=$files} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $release 'manifest.json')
    $null=Read-LabRelease $release
    Write-Host "Release package created: $Version. Local artifact only; dirty=$dirty. No cloud operations."
} finally { Pop-Location }
