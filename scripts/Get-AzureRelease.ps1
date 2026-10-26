[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[0-9]+$')][string]$RunId,[Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{40}$')][string]$Commit)
. "$PSScriptRoot/Cloud-Common.ps1"
if(!$env:GITHUB_REPOSITORY -or !$env:GH_TOKEN){throw 'Repository-scoped Actions read token required.'}
$json=& gh api "repos/$env:GITHUB_REPOSITORY/actions/runs/$RunId"
if($LASTEXITCODE -ne 0){throw 'Release provenance read failed.'}
$run=$json -join "`n" | ConvertFrom-Json
if($run.event -ne 'workflow_dispatch' -or $run.path -ne '.github/workflows/release.yml' -or $run.head_branch -ne 'main' -or $run.head_sha -cne $Commit -or $run.conclusion -ne 'success' -or $run.repository.full_name -cne $env:GITHUB_REPOSITORY){throw 'Release must come from the exact successful manual main-branch package run and repository.'}
$out=Get-LabPrivatePath ".local/releases/run-$RunId"
if(Test-Path -LiteralPath $out){throw 'Existing release preserved; use a clean runner.'}
& gh run download $RunId --repo $env:GITHUB_REPOSITORY --name "azure-release-$RunId" --dir $out
if($LASTEXITCODE -ne 0){throw 'Release download failed.'}
$m=Read-LabRelease $out
if($m.commit -cne $Commit -or $m.version -cne "run-$RunId" -or $m.workingTreeDirty){throw 'Release manifest provenance mismatch.'}
Write-Host 'Exact immutable release provenance and all content hashes verified.'
