[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$Target,[Parameter(Mandatory)][string]$Release,
      [Parameter(Mandatory)][ValidateSet('identity','catalog','reservations','payments','notifications')][string]$Schema,
      [switch]$Execute,[string]$Authorization,[string]$SeedFile)
. "$PSScriptRoot/Use-Toolchain.ps1"
. "$PSScriptRoot/Cloud-Common.ps1"
Assert-LabAuthorization $Execute.IsPresent $Authorization
$t=Read-LabTarget $Target
$m=Read-LabRelease $Release
if(!$Execute){Write-Host "Plan: one-shot $Schema migration from $($m.version); no Azure calls.";return}
if($m.workingTreeDirty){throw 'Cloud delivery requires a clean committed release.'}
Assert-LabContext $t
Assert-LabStopped $t
$principal=$t.migratorObjectIds.$Schema
$id=[guid]::Empty
if(![guid]::TryParse($principal,[ref]$id) -or $id -eq [guid]::Empty){throw 'Expected schema migrator object ID missing.'}
if($Schema -eq 'identity'){$env:AZURELAB_CLOUD_SEED_FILE=Get-LabPrivatePath $SeedFile}
if($PSCmdlet.ShouldProcess("$($t.resourceGroup)/$Schema",'Apply versioned SQL with exact schema migrator identity')){
    $dir=Get-LabPrivatePath $Release
    $tool=Get-LabPrivatePath ('.local/migration-tool-'+[guid]::NewGuid().ToString('N'))
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $dir 'migrator.zip'),$tool)
    try{
        $token=Invoke-LabAz @('account','get-access-token','--resource','https://database.windows.net/','--subscription',$t.subscriptionId)
        $env:AZURELAB_SQL_ACCESS_TOKEN=$token.accessToken
        & dotnet (Join-Path $tool 'Lab.Provisioner.dll') apply-cloud-sql $Schema "tourlabazure-$($t.suffix)-sql.database.windows.net" (Join-Path $dir ($Schema+'.sql')) $principal AZ-012
        if($LASTEXITCODE -ne 0){throw 'Schema migration failed; do not start runtime apps.'}
    }finally{Remove-Item Env:AZURELAB_SQL_ACCESS_TOKEN -ErrorAction SilentlyContinue; $token=$null;Remove-Item Env:AZURELAB_CLOUD_SEED_FILE -ErrorAction SilentlyContinue}
}
