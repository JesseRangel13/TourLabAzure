[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$Target,[switch]$Execute,[string]$Authorization)
. "$PSScriptRoot/Cloud-Common.ps1"
Assert-LabAuthorization $Execute.IsPresent $Authorization
$t=Read-LabTarget $Target
Write-Host 'Stopping apps leaves App Service plan, Service Bus base, Cosmos throughput, SQL/storage/backup and monitoring charges. Budget alerts are not hard limits.'
if(!$Execute){Write-Host 'Plan: stop six exact lab apps; retain all data and plans. No Azure calls.';return}
Assert-LabContext $t
# Validate all resources before stopping any, then recheck each at mutation.
$ids=@('gateway','catalog','reservations','payments','notifications','receipts') | ForEach-Object { Get-LabSiteId $t $_ }
foreach($id in $ids){$null=Assert-LabResource $t $id}
foreach($id in $ids){if($PSCmdlet.ShouldProcess($id,'Stop Azure lab app')){$null=Assert-LabResource $t $id;$null=Invoke-LabAz @('webapp','stop','--ids',$id,'--subscription',$t.subscriptionId)}}
