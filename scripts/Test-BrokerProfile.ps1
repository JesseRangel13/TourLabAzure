# Dot-source from verification orchestration. Only explicit callers start licensed resources.
function Start-IsolatedTestBroker([string]$Run,[bool]$SqlAccepted,[bool]$BusAccepted){
    if(!$SqlAccepted -or !$BusAccepted){throw 'Isolated test broker requires both explicit license acceptance switches.'}
    if($Run -notmatch '^[a-f0-9]{32}$'){throw 'Invalid isolated broker run ID.'}
    foreach($port in 5674,5302){
        $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$port)
        try{$listener.Start()}catch{throw "Test broker port $port is occupied; no existing broker will be stopped."}finally{$listener.Stop()}
    }
    $directory=Join-Path $root ".local/isolatedbroker-$Run"
    New-Item -ItemType Directory $directory|Out-Null
    $bytes=New-Object byte[] 32;$rng=[Security.Cryptography.RandomNumberGenerator]::Create()
    try{$rng.GetBytes($bytes)}finally{$rng.Dispose()}
    $password='Az0!'+[BitConverter]::ToString($bytes).Replace('-','')
    $file=Join-Path $directory 'credentials.env'
    @("AZURELAB_TEST_ACCEPT_EULA=Y","AZURELAB_TEST_BROKER_SQL_PASSWORD=$password")|Set-Content $file
    $project="tourlab-azure-az010-$Run"
    $state=@{Project=$project;File=$file;Started=$true}
    try{
        & docker compose --project-directory $root -f "$root/compose.tests.yaml" --env-file $file -p $project up -d | Out-Host
        if($LASTEXITCODE -ne 0){throw 'Isolated test emulator startup failed; data retained.'}
        $container=& docker compose --project-directory $root -f "$root/compose.tests.yaml" --env-file $file -p $project ps --all --quiet servicebus
        if($LASTEXITCODE -ne 0 -or !$container){throw 'Cannot inspect owned emulator startup.'}
        for($attempt=0;$attempt -lt 90;$attempt++){
            $status=& docker inspect --format '{{.State.Status}}' $container
            if($LASTEXITCODE -ne 0 -or $status -eq 'exited'){throw 'Owned emulator exited before health; inspect its private logs without retrying or deleting data.'}
            try{
                $health=Invoke-WebRequest 'http://127.0.0.1:5302/health' -UseBasicParsing -TimeoutSec 5
                if($health.StatusCode -eq 200){return $state}
            }catch{}
            Start-Sleep -Seconds 2
        }
        throw 'Isolated test emulator health timed out; private data/logs retained.'
    }catch{Stop-IsolatedTestBroker $state;throw}
}
function Stop-IsolatedTestBroker($State){
    if($null -eq $State){return}
    if($State.Project -notmatch '^tourlab-azure-az010-[a-f0-9]{32}$'){throw 'Refusing non-test Compose project.'}
    & docker compose --project-directory $root -f "$root/compose.tests.yaml" --env-file $State.File -p $State.Project stop
    if($LASTEXITCODE -ne 0){throw 'Owned test broker stop failed; inspect only its recorded project.'}
}
