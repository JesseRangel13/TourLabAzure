[CmdletBinding()]
param([Parameter(Mandatory)][string]$Target,[switch]$Online)
. "$PSScriptRoot/Cloud-Common.ps1"
$t=Read-LabTarget $Target
if(!$Online){Write-Host 'Plan: strict platform HTTPS liveness plus anonymous readiness rejection; workflow smoke NOT VERIFIED. No Azure calls.';return}
Assert-LabContext $t
$handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false;$handler.UseCookies=$false
$client=[Net.Http.HttpClient]::new($handler);$client.Timeout=[TimeSpan]::FromSeconds(5)
try {
    foreach($w in @('gateway','catalog','reservations','payments','notifications')){
        $site=Invoke-LabAz @('webapp','show','--ids',(Get-LabSiteId $t $w),'--subscription',$t.subscriptionId)
        if($site.defaultHostName -cnotmatch '^tourlabazure[a-z0-9-]*(?:\.[a-z0-9]+-[0-9]{2})?\.azurewebsites\.net$'){throw 'Unexpected Azure hostname.'}
        $base='https://'+$site.defaultHostName
        $ready=$false
        for($attempt=0;$attempt -lt 30;$attempt++){
            try{$response=$client.GetAsync($base+'/health/live').GetAwaiter().GetResult();$ready=([int]$response.StatusCode -eq 200);$response.Dispose()}catch{$ready=$false}
            if($ready){break};Start-Sleep -Seconds 2
        }
        if(!$ready){throw 'Bounded HTTPS liveness failed.'}
        $response=$client.GetAsync($base+'/health/ready').GetAwaiter().GetResult()
        try{if([int]$response.StatusCode -ne 401){throw 'Anonymous backend readiness must be denied.'}}finally{$response.Dispose()}
    }
}finally{$client.Dispose();$handler.Dispose()}
Write-Host 'Five strict HTTPS liveness checks and anonymous401 checks passed. Authenticated readiness, Functions trigger and business/RBAC smoke are separate AZ-012 evidence.'
