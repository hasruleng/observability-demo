param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('healthy', 'slow', 'failing')]
    [string]$Name
)

# Sets the scenario on Demo.Dependency (Compose service: fulfillment), the fake fulfillment service;
# it does not place an order. In Compose mode, the request reaches it at host port 8082.
# healthy adds a short delay and succeeds; slow delays longer; failing returns a synthetic failure.
# Use healthy to reset the service to a known-good baseline before the UI test.
# Usage: .\scripts\set-scenario.ps1 -Name healthy|slow|failing
$ErrorActionPreference = 'Stop'
Invoke-RestMethod -Uri 'http://127.0.0.1:8082/scenario' `
    -Method Post `
    -ContentType 'application/json' `
    -Body (@{ name = $Name } | ConvertTo-Json -Compress)
