[CmdletBinding(SupportsShouldProcess,ConfirmImpact='High')]
param([Parameter(Mandatory)][string]$Target,[switch]$Execute,[string]$Authorization,[switch]$IncludeData,[switch]$RetentionAcknowledged,[string]$InventorySha256)
. "$PSScriptRoot/Cloud-Common.ps1"
Assert-LabAuthorization $Execute.IsPresent $Authorization
$t=Read-LabTarget $Target
if(!$Execute){Write-Host 'Plan only: default removes reviewed app/plan compute; all data, broker, identities, vault and monitoring retained. IncludeData additionally requires explicit retention acknowledgement and reviewed inventory hash. No group deletion or vault purge exists.';return}
if($IncludeData -and !$RetentionAcknowledged){throw 'Explicit data/backup/queued-message retention acknowledgement required.'}
Assert-LabContext $t
$items=@(Invoke-LabAz @('resource','list','--resource-group',$t.resourceGroup,'--subscription',$t.subscriptionId))
foreach($item in $items){$null=Assert-LabResource $t $item.id}
$inventory=@($items | Sort-Object id | ForEach-Object {[ordered]@{id=$_.id;type=$_.type;tags=$_.tags}}) | ConvertTo-Json -Depth 8
$path=Get-LabPrivatePath '.local/cloud-destroy-inventory.json'
$inventory | Set-Content -LiteralPath $path
$hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
Write-Host "Private inventory SHA256: $hash"
if($InventorySha256 -cne $hash){throw 'Inventory written for review. Re-run only after review with its exact SHA256.'}
$types=@('Microsoft.Web/sites','Microsoft.Web/serverfarms')
if($IncludeData){$types+=@('Microsoft.ServiceBus/namespaces','Microsoft.DocumentDB/databaseAccounts','Microsoft.Sql/servers','Microsoft.Storage/storageAccounts')}
# Sites precede plans. Vault/signing/protection keys, logs, identities, role definitions and resource group always retained.
foreach($type in $types){foreach($item in @($items | Where-Object type -eq $type)){
    if($PSCmdlet.ShouldProcess($item.id,'Delete exactly reviewed lab resource; irreversible data loss where selected')){
        $null=Assert-LabResource $t $item.id
        $null=Invoke-LabAz @('resource','delete','--ids',$item.id,'--subscription',$t.subscriptionId)
    }
}}
Write-Host 'Review retained inventory and billing separately. No zero-cost or complete-erasure claim.'
