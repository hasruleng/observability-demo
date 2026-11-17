$ErrorActionPreference = 'Stop'

$api = 'http://127.0.0.1:8080'
$fulfillment = 'http://127.0.0.1:8082'

function Wait-ForOrder([guid]$orderId) {
    for ($attempt = 0; $attempt -lt 1000; $attempt++) {
        $status = Invoke-RestMethod "$api/orders/$orderId"
        if ($status.status -ne 'pending') {
            return $status
        }
    }

    throw "Order $orderId did not finish within the polling limit."
}

$health = @(
    (Invoke-RestMethod "$api/health").service,
    (Invoke-RestMethod 'http://127.0.0.1:8081/health').service,
    (Invoke-RestMethod "$fulfillment/health").service
)
if (($health -join ',') -ne 'order-api,order-worker,fulfillment') {
    throw "Unexpected health services: $($health -join ',')"
}

$null = Invoke-RestMethod "$fulfillment/scenario" -Method Post -ContentType 'application/json' -Body '{"name":"healthy"}'
$healthy = Invoke-RestMethod "$api/orders" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
$null = Invoke-RestMethod "$fulfillment/process" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
$healthyStatus = Wait-ForOrder $healthy.orderId
if ($healthyStatus.status -ne 'completed') {
    throw "Healthy order status was $($healthyStatus.status)"
}

$null = Invoke-RestMethod "$fulfillment/scenario" -Method Post -ContentType 'application/json' -Body '{"name":"slow"}'
$slow = Invoke-RestMethod "$api/orders" -Method Post -ContentType 'application/json' -Body '{"itemCount":4}'
$slowStatus = Wait-ForOrder $slow.orderId
if ($slowStatus.status -ne 'completed') {
    throw "Slow order status was $($slowStatus.status)"
}
if ($slowStatus.elapsedMilliseconds -lt 600) {
    throw "Expected the four-item slow order to exceed 600 ms; observed $($slowStatus.elapsedMilliseconds) ms."
}

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

[pscustomobject]@{
    Health = $health -join ', '
    HealthyOrder = $healthyStatus.status
    SlowOrder = $slowStatus.status
    SlowElapsedMilliseconds = $slowStatus.elapsedMilliseconds
    SlowItemCount = $slowStatus.itemCount
    FailingOrder = $failingStatus.status
    FailureDetailPresent = [bool]$failingStatus.error
} | Format-List
