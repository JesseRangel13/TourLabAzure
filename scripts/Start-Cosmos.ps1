[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
Push-Location $root
try {
    $existing=& docker compose --env-file .local/dependencies.env ps --all --quiet cosmos
    if($LASTEXITCODE -ne 0){throw 'Cannot inspect isolated Cosmos container.'}
    if($existing){Write-Host 'Cosmos container exists; reuse it without recreation or data deletion.';return}
    if(!(Test-Path .local/cosmos-account.key)){
        $random=New-Object byte[] 64
        $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
        try{$rng.GetBytes($random)}finally{$rng.Dispose()}
        [IO.File]::WriteAllText((Join-Path $root '.local/cosmos-account.key'),[Convert]::ToBase64String($random))
    }
    $previous=$env:AZURELAB_COSMOS_CERT_PASSWORD
    $env:AZURELAB_COSMOS_CERT_PASSWORD=Get-Content -Raw .local/certificates/pfx-password
    try {
        & docker compose --env-file .local/dependencies.env up -d --no-deps --no-recreate cosmos
        if($LASTEXITCODE -ne 0){throw 'Cosmos startup failed; retain and inspect this lab container/data.'}
    }finally{$env:AZURELAB_COSMOS_CERT_PASSWORD=$previous}
}finally{Pop-Location}
