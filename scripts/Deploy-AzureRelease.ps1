[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$Target,[Parameter(Mandatory)][string]$Release,[switch]$Execute,[string]$Authorization,
      [Parameter(Mandatory)][string]$MigrationAttestation)
. "$PSScriptRoot/Cloud-Common.ps1"
Assert-LabAuthorization $Execute.IsPresent $Authorization
$t=Read-LabTarget $Target
$m=Read-LabRelease $Release
$a=Get-Content -Raw -LiteralPath (Get-LabPrivatePath $MigrationAttestation) | ConvertFrom-Json
if($a.releaseManifestSha256 -cne (Get-FileHash (Join-Path (Get-LabPrivatePath $Release) 'manifest.json')).Hash -or
   $a.subscriptionId -ne $t.subscriptionId -or $a.resourceGroup -cne $t.resourceGroup -or
   $a.runtimePermissionsRevalidated -ne $true -or $a.seedVerified -ne $true -or
   (@($a.schemas | Sort-Object) -join '|') -cne 'catalog|identity|notifications|payments|reservations'){throw 'Exact release migration, post-migration denials and synthetic-user seed attestation required.'}
if(!$Execute){Write-Host "Plan: deliver $($m.version), start six apps, strict HTTPS liveness/auth-boundary smoke; no infrastructure changes or Azure calls.";return}
if($m.workingTreeDirty){throw 'Cloud delivery requires a clean committed release.'}
& "$PSScriptRoot/Test-AzurePreflight.ps1" -Target $Target -Online -ExistingApps
Assert-LabStopped $t
$dir=Get-LabPrivatePath $Release
foreach($w in @('catalog','reservations','payments','notifications','receipts','gateway')){
    $id=Get-LabSiteId $t $w
    if(!$PSCmdlet.ShouldProcess($id,"Deploy immutable $($m.version) ZIP")){throw 'Partial delivery declined; apps remain stopped.'}
    $null=Assert-LabResource $t $id
    $kind=if($w -eq 'receipts'){'functionapp'}else{'webapp'}
    if($kind -eq 'functionapp'){
        $null=Invoke-LabAz @('functionapp','deployment','source','config-zip','--resource-group',$t.resourceGroup,'--name',"tourlabazure-$($t.suffix)-$w",'--subscription',$t.subscriptionId,'--src',(Join-Path $dir ($w+'.zip')),'--build-remote','false')
    }else{
        $null=Invoke-LabAz @('webapp','deploy','--ids',$id,'--subscription',$t.subscriptionId,'--src-path',(Join-Path $dir ($w+'.zip')),'--type','zip','--restart','false','--clean','true')
    }
}
try{
    foreach($w in @('catalog','reservations','payments','notifications','receipts','gateway')){
        $id=Get-LabSiteId $t $w
        $null=Assert-LabResource $t $id
        $null=Invoke-LabAz @('webapp','start','--ids',$id,'--subscription',$t.subscriptionId)
    }
    & "$PSScriptRoot/Test-AzureSmoke.ps1" -Target $Target -Online
}catch{
    & "$PSScriptRoot/Stop-AzureLab.ps1" -Target $Target -Execute -Authorization AZ-012 -Confirm:$false
    throw 'Delivery/startup/smoke failed. Apps stopped; retained schema/data require reviewed forward fix or compatible previous release. No automatic database rollback.'
}
Write-Host 'Delivery and liveness/auth-boundary smoke complete; authenticated booking/receipt/RBAC tests still required for AC-A35.'
