# Exercises the order API, worker, and fulfillment service over their local Compose ports.
# The services must already be running; this verifies health, successful processing,
# slow processing, and a synthetic fulfillment failure end to end.
# The slow scenario checks that a four-item order takes at least 600 ms.
# Usage: .\scripts\smoke.ps1
$ErrorActionPreference = 'Stop'

$api = 'http://127.0.0.1:8080'
$fulfillment = 'http://127.0.0.1:8082'

# Poll until the worker finishes the order, then return its final status.
function Wait-ForOrder([guid]$orderId) {
    for ($attempt = 0; $attempt -lt 1000; $attempt++) {
        $status = Invoke-RestMethod "$api/orders/$orderId"
        if ($status.status -ne 'pending') {
            return $status
        }
    }

    throw "Order $orderId did not finish within the polling limit."
}

# Confirm all three services report the expected identity before creating orders.
$health = @(
    (Invoke-RestMethod "$api/health").service,
    (Invoke-RestMethod 'http://127.0.0.1:8081/health').service,
    (Invoke-RestMethod "$fulfillment/health").service
)
if (($health -join ',') -ne 'order-api,order-worker,fulfillment') {
    throw "Unexpected health services: $($health -join ',')"
}

# A healthy scenario needs an explicit fulfillment request to complete the order.
$null = Invoke-RestMethod "$fulfillment/scenario" -Method Post -ContentType 'application/json' -Body '{"name":"healthy"}'
$healthy = Invoke-RestMethod "$api/orders" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
$null = Invoke-RestMethod "$fulfillment/process" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
$healthyStatus = Wait-ForOrder $healthy.orderId
if ($healthyStatus.status -ne 'completed') {
    throw "Healthy order status was $($healthyStatus.status)"
}

# The slow scenario is processed asynchronously by the worker and should exceed the threshold.
$null = Invoke-RestMethod "$fulfillment/scenario" -Method Post -ContentType 'application/json' -Body '{"name":"slow"}'
$slow = Invoke-RestMethod "$api/orders" -Method Post -ContentType 'application/json' -Body '{"itemCount":4}'
$slowStatus = Wait-ForOrder $slow.orderId
if ($slowStatus.status -ne 'completed') {
    throw "Slow order status was $($slowStatus.status)"
}
if ($slowStatus.elapsedMilliseconds -lt 600) {
    throw "Expected the four-item slow order to exceed 600 ms; observed $($slowStatus.elapsedMilliseconds) ms."
}

# The failing scenario should return HTTP 503 and leave the order failed with error details.
$null = Invoke-RestMethod "$fulfillment/scenario" -Method Post -ContentType 'application/json' -Body '{"name":"failing"}'
$failing = Invoke-RestMethod "$api/orders" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
$failureStatusCode = $null
try {
    $null = Invoke-RestMethod "$fulfillment/process" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
} catch {
    if ($_.Exception.Response) {
        $failureStatusCode = [int]$_.Exception.Response.StatusCode
    }
}
if ($failureStatusCode -ne 503) {
    throw "Failing fulfillment service returned HTTP $failureStatusCode instead of 503."
}

$failingStatus = Wait-ForOrder $failing.orderId
if ($failingStatus.status -ne 'failed' -or -not $failingStatus.error) {
    throw "Expected a failed order with details; received '$($failingStatus.status)'."
}

# Print a concise summary for local runs and CI logs.
[pscustomobject]@{
    Health = $health -join ', '
    HealthyOrder = $healthyStatus.status
    SlowOrder = $slowStatus.status
    SlowElapsedMilliseconds = $slowStatus.elapsedMilliseconds
    SlowItemCount = $slowStatus.itemCount
    FailingOrder = $failingStatus.status
    FailureDetailPresent = [bool]$failingStatus.error
} | Format-List
