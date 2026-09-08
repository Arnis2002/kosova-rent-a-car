param([switch]$Web)
$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$taskPg=Join-Path (Get-Location) '.local/postgres/pgsql/bin/pg_ctl.exe'
if(Test-Path $taskPg) {
 & $taskPg -D '.local/pgdata' status
 if($LASTEXITCODE -ne 0){& $taskPg -D '.local/pgdata' -l '.local/runtime/postgres.log' start}
}
if($Web){npm.cmd run dev --prefix apps/web}else{& (Join-Path $PSScriptRoot 'start-api.ps1')}
