param([string]$Assembly)
$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ConnectionStrings__Database='Host=127.0.0.1;Port=55432;Database=kosova_test;Username=kosova;Password='+(Get-Content .local/runtime/db-password.txt -Raw).Trim()
$env:DemoMode='true'
$env:DemoPassword=(Get-Content .local/runtime/demo-password.txt -Raw).Trim()
$env:TestPaymentSecret='local-test-webhook-key-not-for-production'
$env:RateLimit__RequestsPerMinute='2000'
$env:RateLimit__AuthRequestsPerMinute='100'
if($Assembly){dotnet $Assembly --contentRoot (Join-Path (Get-Location) 'apps/api') --urls http://127.0.0.1:5081}
else{dotnet run --project apps/api --no-restore --no-launch-profile --urls http://127.0.0.1:5081}
