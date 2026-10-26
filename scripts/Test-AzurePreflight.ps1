[CmdletBinding()]
param([Parameter(Mandatory)][string]$Target,[switch]$Online,[switch]$ExistingApps)
. "$PSScriptRoot/Cloud-Common.ps1"
$t=Read-LabTarget $Target
foreach($tool in @('az','dotnet')){if($Online -and !(Get-Command $tool -ErrorAction SilentlyContinue)){throw 'Required cloud tool unavailable.'}}
if($Online){
    $version=Invoke-LabAz @('version')
    if($version.'azure-cli' -cne '2.91.0'){throw 'Reviewed Azure CLI 2.91.0 required; no silent CLI upgrade.'}
    Assert-LabContext $t
    foreach($provider in @('Microsoft.Web','Microsoft.Storage','Microsoft.Sql','Microsoft.DocumentDB','Microsoft.ServiceBus','Microsoft.KeyVault','Microsoft.ManagedIdentity','Microsoft.OperationalInsights','Microsoft.Insights','Microsoft.Consumption')){
        $p=Invoke-LabAz @('provider','show','--namespace',$provider,'--subscription',$t.subscriptionId)
        if($p.registrationState -ne 'Registered'){throw 'Required provider is not registered. This script does not register providers.'}
    }
    $runtimes=Invoke-LabAz @('webapp','list-runtimes','--os','linux')
    if(!(@($runtimes) -match '^DOTNETCORE[:|]10.0$')){throw 'App Service Linux .NET 10 runtime unavailable.'}
    $regions=Invoke-LabAz @('functionapp','list-flexconsumption-locations','--runtime','dotnet-isolated','--runtime-version','10.0')
    if(!(@($regions | ForEach-Object name) -contains $t.location)){throw 'Flex .NET 10 region unavailable.'}
    if($ExistingApps){foreach($w in @('gateway','catalog','reservations','payments','notifications','receipts')){$null=Assert-LabResource $t (Get-LabSiteId $t $w)}}
    if($t.quotaReviewed -ne $true -or $t.sqlEgressReviewed -ne $true -or $t.oidcReviewed -ne $true){throw 'Actual quota, SQL egress and OIDC prerequisite reviews missing.'}
    Write-Host 'Read-only online preflight passed. Runtime data-plane authorization and deployment are NOT VERIFIED.'
}else{Write-Host 'Offline target guards passed. Providers, quotas, runtimes, OIDC, egress and Azure ownership NOT VERIFIED.'}
