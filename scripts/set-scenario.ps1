param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('healthy', 'slow', 'failing')]
    [string]$Name
)

$ErrorActionPreference = 'Stop'
Invoke-RestMethod -Uri 'http://127.0.0.1:8082/scenario' `
    -Method Post `
    -ContentType 'application/json' `
    -Body (@{ name = $Name } | ConvertTo-Json -Compress)
