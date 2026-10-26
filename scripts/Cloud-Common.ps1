# Dot-source only. Dry plans never invoke Azure CLI.
$ErrorActionPreference='Stop'
$script:AzureLabRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot))
function Get-LabPrivatePath([string]$Path) {
    $full=if([IO.Path]::IsPathRooted($Path)){[IO.Path]::GetFullPath($Path)}else{[IO.Path]::GetFullPath((Join-Path $script:AzureLabRoot $Path))}
    $owned=(Join-Path $script:AzureLabRoot '.local')+[IO.Path]::DirectorySeparatorChar
    if(!$full.StartsWith($owned,[StringComparison]::OrdinalIgnoreCase)){throw 'Private input/output must stay inside this Azure repository .local.'}
    # Refuse junctions/symlinks in every existing path component.
    $cursor=$full
    while($cursor -and $cursor.Length -ge $script:AzureLabRoot.Length){
        if(Test-Path -LiteralPath $cursor){if((Get-Item -Force -LiteralPath $cursor).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked private paths are forbidden.'}}
        $cursor=Split-Path $cursor
    }
    return $full
}
function Read-LabTarget([string]$Path) {
    $target=Get-Content -Raw -LiteralPath (Get-LabPrivatePath $Path) | ConvertFrom-Json
    foreach($name in @('subscriptionId','tenantId')){ $id=[guid]::Empty; if(![guid]::TryParse($target.$name,[ref]$id) -or $id -eq [guid]::Empty){throw 'Explicit nonempty subscription/tenant IDs required.'} }
    if($target.suffix -cnotmatch '^[a-z0-9]{4,6}$' -or $target.resourceGroup -cne "tourlab-azure-$($target.suffix)" -or $target.location -cne 'eastus'){throw 'Expected reviewed East US lab resource group/suffix required.'}
    if(!$target.owner -or $target.costReviewed -ne $true -or $target.proxyTopologyReviewed -ne $true -or $target.capacityReviewed -ne $true){throw 'Cost, exact proxy topology and capacity reviews must be recorded before cloud execution.'}
    return $target
}
function Invoke-LabAz([string[]]$Arguments) {
    # Capture privately; never print raw Azure responses/errors (may contain configuration).
    $run=Join-Path $script:AzureLabRoot '.local'
    New-Item -ItemType Directory -Force $run | Out-Null
    $err=Join-Path $run ('azure-cli-'+[guid]::NewGuid().ToString('N')+'.err.log')
    $json=& az @Arguments --only-show-errors --output json 2>$err
    if($LASTEXITCODE -ne 0){throw 'Azure CLI operation failed; inspect the private local error file.'}
    if($json){return ($json -join "`n" | ConvertFrom-Json)}
}
function Assert-LabContext($Target) {
    $account=Invoke-LabAz @('account','show')
    if($account.id -ne $Target.subscriptionId -or $account.tenantId -ne $Target.tenantId){throw 'Current Azure subscription/tenant mismatch; no automatic account switch.'}
    $group=Invoke-LabAz @('group','show','--name',$Target.resourceGroup,'--subscription',$Target.subscriptionId)
    if($group.location -ne $Target.location -or $group.tags.Project -cne 'TourLabAzure' -or $group.tags.Environment -cne 'Study' -or $group.tags.Owner -cne $Target.owner){throw 'Resource group location/ownership tags mismatch.'}
}
function Assert-LabResource($Target,[string]$Id) {
    $base="/subscriptions/$($Target.subscriptionId)/resourceGroups/$($Target.resourceGroup)/providers/"
    if(!$Id.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)){throw 'Resource outside exact lab group.'}
    $item=Invoke-LabAz @('resource','show','--ids',$Id,'--subscription',$Target.subscriptionId)
    if($item.tags.Project -cne 'TourLabAzure' -or $item.tags.Environment -cne 'Study' -or $item.tags.Owner -cne $Target.owner -or $item.name -cnotmatch ('^tourlabazure-?'+[regex]::Escape($Target.suffix)+'(?:-|img$|rcp$|hst$|key$)')){throw 'Resource prefix/ownership tags mismatch.'}
    return $item
}
function Get-LabSiteId($Target,[string]$Workload) {
    if(@('gateway','catalog','reservations','payments','notifications','receipts') -cnotcontains $Workload){throw 'Unknown workload.'}
    return "/subscriptions/$($Target.subscriptionId)/resourceGroups/$($Target.resourceGroup)/providers/Microsoft.Web/sites/tourlabazure-$($Target.suffix)-$Workload"
}
function Assert-LabStopped($Target) {
    foreach($w in @('gateway','catalog','reservations','payments','notifications','receipts')){
        $id=Get-LabSiteId $Target $w
        $null=Assert-LabResource $Target $id
        $site=Invoke-LabAz @('webapp','show','--ids',$id,'--subscription',$Target.subscriptionId)
        if($site.state -ne 'Stopped'){throw 'All six runtime apps must be stopped before the migration job.'}
    }
}
function Assert-LabAuthorization([bool]$Execute,[string]$Authorization) {
    if($Execute -and $Authorization -cne 'AZ-012'){throw 'Cloud mutation requires explicit AZ-012 authorization.'}
}
function Read-LabRelease([string]$Directory) {
    $dir=Get-LabPrivatePath $Directory
    $manifest=Get-Content -Raw -LiteralPath (Join-Path $dir 'manifest.json') | ConvertFrom-Json
    if($manifest.schemaVersion -ne 1 -or $manifest.version -cnotmatch '^[a-z0-9][a-z0-9.-]{2,63}$' -or $manifest.commit -cnotmatch '^[a-f0-9]{40}$'){throw 'Invalid release provenance.'}
    $expected=@('gateway.zip','catalog.zip','reservations.zip','payments.zip','notifications.zip','receipts.zip','migrator.zip','identity.sql','catalog.sql','reservations.sql','payments.sql','notifications.sql') | Sort-Object
    if((@($manifest.files | ForEach-Object name | Sort-Object) -join '|') -cne ($expected -join '|')){throw 'Release must have exactly six app ZIPs, one migrator ZIP and five migration scripts.'}
    foreach($f in $manifest.files){
        if($f.sha256 -cnotmatch '^[A-F0-9]{64}$' -or (Get-FileHash -LiteralPath (Join-Path $dir $f.name) -Algorithm SHA256).Hash -cne $f.sha256){throw 'Release content hash mismatch.'}
    }
    return $manifest
}

