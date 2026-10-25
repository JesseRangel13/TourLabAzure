[CmdletBinding()]
param([switch]$IncludeHttps)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Use-Toolchain.ps1"
$root=Split-Path $PSScriptRoot
$run=[Guid]::NewGuid().ToString('N')
$directory=".local/security-$run"
$previous=@{}
foreach($name in 'AZURELAB_INTEGRATION_CONFIG','AZURELAB_IMAGES','AZURELAB_COSMOS','AZURELAB_RECEIPTS'){
    $previous[$name]=[Environment]::GetEnvironmentVariable($name,'Process')
}
Push-Location $root
try{
    & "$PSScriptRoot/Initialize-SqlIdentity.ps1" -OutputDirectory $directory -Database "tourlab_azure_security_$run"
    foreach($feature in 'images-local','dashboard-local','receipts-local'){
        & dotnet run --project tools/Lab.Provisioner -c Release --no-build -- $feature $directory
        if($LASTEXITCODE -ne 0){throw "Local $feature initialization failed; existing data/containers retained."}
    }
    $env:AZURELAB_INTEGRATION_CONFIG=Join-Path $root "$directory/provisioner.json"
    $env:AZURELAB_IMAGES='1';$env:AZURELAB_COSMOS='1';$env:AZURELAB_RECEIPTS='1'
    & dotnet test tests/Scaffold.Tests -c Release --no-build --filter 'FullyQualifiedName!~LiveReceiptTests&FullyQualifiedName!~ReceiptHostTests' --logger "trx;LogFileName=AZ-009-security-$run.trx"
    if($LASTEXITCODE -ne 0){throw 'Real SQL/storage/security regression failed; retain private profiles and data.'}
    if($IncludeHttps){
        # The receipt helper uses a separate fresh SQL profile for actual live workers.
        $env:AZURELAB_IMAGES=$null;$env:AZURELAB_COSMOS=$null
        & "$PSScriptRoot/Verify-Receipts.ps1" -IncludeHttps -SkipRelational
    }
    Write-Host "Security regression passed. Retained private SQL/storage profile $directory. Azure identity/RBAC/Key Vault remain NOT VERIFIED."
}finally{
    foreach($name in $previous.Keys){[Environment]::SetEnvironmentVariable($name,$previous[$name],'Process')}
    Pop-Location
}