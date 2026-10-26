[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$Target,[Parameter(Mandatory)][string]$Parameters,[ValidateSet('Plan','WhatIf','Provision')][string]$Operation='Plan',[switch]$Execute,[string]$Authorization)
. "$PSScriptRoot/Use-Toolchain.ps1"
. "$PSScriptRoot/Cloud-Common.ps1"
Assert-LabAuthorization $Execute.IsPresent $Authorization
$t=Read-LabTarget $Target
$p=Get-LabPrivatePath $Parameters
& dotnet "$script:AzureLabRoot/tools/Lab.Provisioner/bin/Release/net10.0/Lab.Provisioner.dll" validate-infrastructure $p
if($LASTEXITCODE -ne 0){throw 'Offline infrastructure parameters rejected.'}
$values=(Get-Content -Raw -LiteralPath $p | ConvertFrom-Json).parameters
if($values.suffix.value -cne $t.suffix -or $values.location.value -cne $t.location -or $values.owner.value -cne $t.owner -or $values.sqlAdminTenantId.value -ne $t.tenantId){throw 'Infrastructure parameters do not match exact target.'}
if($Operation -eq 'Plan' -or !$Execute){Write-Host "Prepared $Operation for $($t.resourceGroup); no Azure calls. Review cost, quota, secrets, proxy and SQL egress first."; return}
Assert-LabContext $t
& "$PSScriptRoot/Test-AzurePreflight.ps1" -Target $Target -Online
$verb=if($Operation -eq 'WhatIf'){'what-if'}else{'create'}
if($PSCmdlet.ShouldProcess($t.resourceGroup,"Azure infrastructure $verb")){
    $null=Invoke-LabAz @('deployment','group',$verb,'--resource-group',$t.resourceGroup,'--subscription',$t.subscriptionId,'--name',"tourlabazure-$($t.suffix)-infrastructure",'--template-file',"$script:AzureLabRoot/infra/main.bicep",'--parameters',('@'+$p))
    Write-Host 'Requested infrastructure operation completed. No application packages or SQL migration executed.'
}
