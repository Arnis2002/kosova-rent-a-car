$ErrorActionPreference='Stop'
Set-Location (Join-Path $PSScriptRoot '..')
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:ConnectionStrings__Database='Host=127.0.0.1;Port=55432;Database=kosova_test;Username=kosova;Password='+(Get-Content .local/runtime/db-password.txt -Raw).Trim()
$env:DemoMode='true'
$env:DemoPassword=(Get-Content .local/runtime/demo-password.txt -Raw).Trim()
$env:TestPaymentSecret='local-test-webhook-key-not-for-production'
$env:RateLimit__RequestsPerMinute='2000'
$env:RateLimit__AuthRequestsPerMinute='100'
dotnet run --project apps/api --no-build --no-restore --no-launch-profile --urls http://127.0.0.1:5081
