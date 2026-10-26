[CmdletBinding()]
param([Parameter(Mandatory)][string]$Release,[switch]$TestGuards)
. "$PSScriptRoot/Cloud-Common.ps1"
$m=Read-LabRelease $Release
$dir=Get-LabPrivatePath $Release
Add-Type -AssemblyName System.IO.Compression.FileSystem
$entriesChecked=0
foreach($workload in @('gateway','catalog','reservations','payments','notifications','receipts','migrator')){
    $zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $dir ($workload+'.zip')))
    try{
        foreach($entry in $zip.Entries){
            $entriesChecked++
            if($entry.FullName -match '(^[\\/]|^[A-Za-z]:|(^|[\\/])\.\.([\\/]|$)|(^|[\\/])\.local([\\/]|$)|local.settings.json$|(^|[\\/])\.env$|\.(pem|pfx|key)$)'){throw 'Unsafe/private package entry rejected.'}
            if($entry.FullName -match '\.(json|sql|html|js|css)$'){
                $reader=[IO.StreamReader]::new($entry.Open())
                try{if($reader.ReadToEnd() -match '-----BEGIN (RSA |EC )?PRIVATE KEY-----'){throw 'Private key material rejected.'}}finally{$reader.Dispose()}
            }
        }
        if($workload -eq 'gateway' -and !($zip.Entries | Where-Object FullName -eq 'wwwroot/index.html')){throw 'Gateway SPA missing.'}
        if($workload -eq 'receipts'){
            foreach($required in @('host.json','worker.config.json','Receipts.Functions.dll')){if(!($zip.Entries | Where-Object FullName -eq $required)){throw 'Functions ZIP root layout invalid.'}}
            if(!($zip.Entries | Where-Object FullName -like '.azurefunctions/*')){throw 'Functions hidden extension directory missing.'}
        }
    }finally{$zip.Dispose()}
}
if($TestGuards){
    $original=(Get-FileHash (Join-Path $dir 'catalog.sql')).Hash
    $probe=Get-LabPrivatePath ('.local/release-tamper-'+[guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $probe | Out-Null
    Copy-Item (Join-Path $dir 'manifest.json') (Join-Path $probe 'manifest.json')
    Copy-Item (Join-Path $dir 'catalog.sql') (Join-Path $probe 'catalog.sql')
    Add-Content (Join-Path $probe 'catalog.sql') '-- tampered COPY; original immutable artifact preserved'
    $rejected=$false
    try{$null=Read-LabRelease $probe}catch{if($_.Exception.Message -eq 'Release content hash mismatch.'){$rejected=$true}else{throw}}
    if(!$rejected -or (Get-FileHash (Join-Path $dir 'catalog.sql')).Hash -cne $original){throw 'Tamper/isolation guard failed.'}
}
Write-Host "Release $($m.version):12 hash-verified payloads;7 ZIPs/$entriesChecked entries; SPA/Functions layout and private-key/name checks passed. Tamper probe=$($TestGuards.IsPresent). Cloud delivery NOT VERIFIED."
