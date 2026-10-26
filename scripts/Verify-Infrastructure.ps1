[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Cloud-Common.ps1"
& "$PSScriptRoot/Install-Bicep.ps1"
$bicep=Join-Path $script:AzureLabRoot $(if($env:OS -like '*Windows*'){'.local/tools/bicep-0.48.1/bicep.exe'}else{'.local/tools/bicep-0.48.1/bicep'})
$out=Get-LabPrivatePath '.local/az011-template.json'
& $bicep build "$script:AzureLabRoot/infra/main.bicep" --outfile $out
if($LASTEXITCODE -ne 0){throw 'Offline Bicep build/lint failed.'}
foreach($file in Get-ChildItem "$script:AzureLabRoot/scripts" -Filter '*.ps1'){
    $tokens=$null;$errors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
    if($errors.Count){throw "PowerShell syntax error: $($file.Name)"}
}
# Dynamic boundary tests: a sentinel Azure CLI must never be called by these offline paths.
$global:AzureLabUnexpectedCalls=0
function global:az { $global:AzureLabUnexpectedCalls++; throw 'Unexpected cloud call during offline verification.' }
$folder=Get-LabPrivatePath ('.local/infra-guards-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder | Out-Null
$target=Join-Path $folder 'target.json'
@{subscriptionId='11111111-1111-4111-8111-111111111111';tenantId='22222222-2222-4222-8222-222222222222';resourceGroup='tourlab-azure-test11';suffix='test11';location='eastus';owner='offline-test';costReviewed=$true;proxyTopologyReviewed=$true;capacityReviewed=$true} | ConvertTo-Json | Set-Content $target
try{
    & "$PSScriptRoot/Test-AzurePreflight.ps1" -Target $target
    & "$PSScriptRoot/Stop-AzureLab.ps1" -Target $target
    & "$PSScriptRoot/Remove-AzureLab.ps1" -Target $target
    & "$PSScriptRoot/Test-AzureSmoke.ps1" -Target $target
    foreach($script in @('Stop-AzureLab.ps1','Remove-AzureLab.ps1')){
        $rejected=$false
        try{& "$PSScriptRoot/$script" -Target $target -Execute}catch{$rejected=$true}
        if(!$rejected){throw 'Unauthorized execution was accepted.'}
    }
    foreach($bad in @('../outside.json','C:\Windows\target.json')){
        $rejected=$false;try{$null=Get-LabPrivatePath $bad}catch{$rejected=$true}
        if(!$rejected){throw 'Private path escape accepted.'}
    }
    if($global:AzureLabUnexpectedCalls -ne 0){throw 'Offline commands attempted Azure access.'}
    Write-Host 'Offline Bicep build/lint, all PowerShell parsers and 8 cloud/path boundary cases passed. Actual Azure what-if/deployment NOT VERIFIED.'
}finally{Remove-Item Function:\az -ErrorAction SilentlyContinue;Remove-Variable AzureLabUnexpectedCalls -Scope Global -ErrorAction SilentlyContinue}
