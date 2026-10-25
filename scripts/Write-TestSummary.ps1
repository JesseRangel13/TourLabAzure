[CmdletBinding()]
param([string]$SqlReport,[string]$BrokerReport,[string]$BrowserReport)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
# Export only fixed category names and integer totals. Never copy assertions, stdout,
# attachment paths, auth state, DOM, network captures or error messages into CI artifacts.
function Private-Report([string]$Path){
    $full=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($Path)){$Path}else{Join-Path $root $Path}))
    if(!$full.StartsWith((Join-Path $root '.local')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Only private lab reports may be summarized.'}
    if(Test-Path -LiteralPath $full -PathType Leaf){Get-Item -LiteralPath $full}
}
$selected=![string]::IsNullOrEmpty($SqlReport) -or ![string]::IsNullOrEmpty($BrokerReport) -or ![string]::IsNullOrEmpty($BrowserReport)
if($selected -and (!$SqlReport -or !$BrokerReport -or !$BrowserReport)){throw 'Select all three reports explicitly, or use fresh-CI discovery.'}
if($selected){
    $sqlFiles=@(Private-Report $SqlReport);$brokerFiles=@(Private-Report $BrokerReport);$browserFiles=@(Private-Report $BrowserReport)
}else{
    $sqlFiles=@(Get-ChildItem "$root/.local/integration-*/results/sql-storage.trx" -ErrorAction SilentlyContinue)
    $brokerFiles=@(Get-ChildItem "$root/.local/integration-*/results/broker.trx" -ErrorAction SilentlyContinue)
    $browserFiles=@(Get-ChildItem "$root/.local/e2e-*/browser-report/junit.xml" -ErrorAction SilentlyContinue)
}
$suites=@();$hostVerified=$false
foreach($group in @(@{Files=$sqlFiles;Category='SQL-Blob-Cosmos'},@{Files=$brokerFiles;Category='ServiceBus'})){
    foreach($file in $group.Files){
        [xml]$trx=Get-Content -Raw $file.FullName
        $c=$trx.TestRun.ResultSummary.Counters
        $suites+=@{category=$group.Category;total=[int]$c.total;passed=[int]$c.passed;failed=[int]$c.failed;skipped=[int]$c.notExecuted}
    }
}
foreach($file in $browserFiles){
    $markerPath=Join-Path $file.Directory.Parent.FullName 'completed.json'
    if(Test-Path $markerPath){
        $marker=Get-Content -Raw $markerPath|ConvertFrom-Json
        $hostVerified=$marker.schemaVersion -eq 1 -and $marker.passed -eq $true -and $marker.functionsTraceVerified -eq $true
    }
    [xml]$junit=Get-Content -Raw $file.FullName;$c=$junit.testsuites
    $suites+=@{category='Chromium-full-stack';total=[int]$c.tests;passed=([int]$c.tests-[int]$c.failures-[int]$c.errors-[int]$c.skipped);failed=([int]$c.failures+[int]$c.errors);skipped=[int]$c.skipped}
}
$minimum=@{'SQL-Blob-Cosmos'=183;ServiceBus=7;'Chromium-full-stack'=12}
$bad=@($suites|Where-Object {$_.failed -ne 0 -or $_.skipped -ne 0 -or $_.passed -ne $_.total -or $_.total -lt $minimum[$_.category]})
$complete=$hostVerified -and $suites.Count -eq 3 -and @($suites.category|Sort-Object -Unique).Count -eq 3 -and $bad.Count -eq 0
@{schemaVersion=1;cloudVerified=$false;functionsTraceVerified=$hostVerified;complete=$complete;suites=$suites}|ConvertTo-Json -Depth 4|Set-Content "$root/.local/ci-summary.json"
Write-Host 'Count-only summary written; private diagnostic evidence excluded.'
