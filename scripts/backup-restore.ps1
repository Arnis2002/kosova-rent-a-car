param([string]$Database='kosova_test')
$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
if($Database -notmatch '^kosova(_test)?$'){throw 'Only workspace application databases are supported.'}
$taskPg=Join-Path (Get-Location) '.local/postgres/pgsql/bin'
$env:PGPASSWORD=(Get-Content .local/runtime/db-password.txt -Raw).Trim()
New-Item -ItemType Directory -Force artifacts | Out-Null
$taskBackup=Join-Path (Get-Location) 'artifacts/restore-check.dump'
$taskRestored='kosova_restore_'+[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
& (Join-Path $taskPg 'pg_dump.exe') -h 127.0.0.1 -p 55432 -U kosova -d $Database -Fc -f $taskBackup
if($LASTEXITCODE -ne 0){throw 'Backup failed'}
& (Join-Path $taskPg 'createdb.exe') -h 127.0.0.1 -p 55432 -U kosova $taskRestored
& (Join-Path $taskPg 'pg_restore.exe') -h 127.0.0.1 -p 55432 -U kosova -d $taskRestored --exit-on-error $taskBackup
if($LASTEXITCODE -ne 0){throw 'Restore failed'}
$taskQuery="SELECT 'bookings',count(*) FROM bookings UNION ALL SELECT 'quotes',count(*) FROM quotes UNION ALL SELECT 'allocations',count(*) FROM allocations UNION ALL SELECT 'schema_versions',count(*) FROM schema_versions ORDER BY 1"
$taskBefore=& (Join-Path $taskPg 'psql.exe') -h 127.0.0.1 -p 55432 -U kosova -d $Database -t -A -c $taskQuery
$taskAfter=& (Join-Path $taskPg 'psql.exe') -h 127.0.0.1 -p 55432 -U kosova -d $taskRestored -t -A -c $taskQuery
if(($taskBefore -join "`n") -ne ($taskAfter -join "`n")){throw 'Restored row counts differ'}
$taskConstraint=& (Join-Path $taskPg 'psql.exe') -h 127.0.0.1 -p 55432 -U kosova -d $taskRestored -t -A -c "SELECT count(*) FROM pg_constraint WHERE conrelid='allocations'::regclass AND contype='x'"
if($taskConstraint.Trim() -ne '1'){throw 'Restored overlap constraint missing'}
"Backup/restore PASS: $Database -> $taskRestored. Counts and GiST exclusion preserved." | Tee-Object -FilePath artifacts/restore-result.txt
$taskAfter | Add-Content artifacts/restore-result.txt
