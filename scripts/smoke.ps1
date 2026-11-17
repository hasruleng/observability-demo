$ErrorActionPreference = 'Stop'

$api = 'http://127.0.0.1:8080'
$dependency = 'http://127.0.0.1:8082'

function Wait-ForJourney([guid]$journeyId) {
    for ($attempt = 0; $attempt -lt 1000; $attempt++) {
        $status = Invoke-RestMethod "$api/journeys/$journeyId"
        if ($status.status -ne 'pending') {
            return $status
        }
    }

    throw "Journey $journeyId did not finish within the polling limit."
}

$health = @(
    (Invoke-RestMethod "$api/health").service,
    (Invoke-RestMethod 'http://127.0.0.1:8081/health').service,
    (Invoke-RestMethod "$dependency/health").service
)
if (($health -join ',') -ne 'journey-api,worker,dependency') {
    throw "Unexpected health services: $($health -join ',')"
}

$null = Invoke-RestMethod "$dependency/scenario" -Method Post -ContentType 'application/json' -Body '{"name":"healthy"}'
$healthy = Invoke-RestMethod "$api/journeys" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
$null = Invoke-RestMethod "$dependency/process" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
$healthyStatus = Wait-ForJourney $healthy.journeyId
if ($healthyStatus.status -ne 'completed') {
    throw "Healthy journey status was $($healthyStatus.status)"
}

$null = Invoke-RestMethod "$dependency/scenario" -Method Post -ContentType 'application/json' -Body '{"name":"slow"}'
$slow = Invoke-RestMethod "$api/journeys" -Method Post -ContentType 'application/json' -Body '{"itemCount":4}'
$slowStatus = Wait-ForJourney $slow.journeyId
if ($slowStatus.status -ne 'completed') {
    throw "Slow journey status was $($slowStatus.status)"
}
if ($slowStatus.elapsedMilliseconds -lt 600) {
    throw "Expected the four-item slow journey to exceed 600 ms; observed $($slowStatus.elapsedMilliseconds) ms."
}

$null = Invoke-RestMethod "$dependency/scenario" -Method Post -ContentType 'application/json' -Body '{"name":"failing"}'
$failing = Invoke-RestMethod "$api/journeys" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
$failureStatusCode = $null
try {
    $null = Invoke-RestMethod "$dependency/process" -Method Post -ContentType 'application/json' -Body '{"itemCount":1}'
} catch {
    if ($_.Exception.Response) {
        $failureStatusCode = [int]$_.Exception.Response.StatusCode
    }
}
if ($failureStatusCode -ne 503) {
    throw "Failing dependency returned HTTP $failureStatusCode instead of 503."
}

$failingStatus = Wait-ForJourney $failing.journeyId
if ($failingStatus.status -ne 'failed' -or -not $failingStatus.error) {
    throw "Expected a failed journey with details; received '$($failingStatus.status)'."
}

[pscustomobject]@{
    Health = $health -join ', '
    Healthy = $healthyStatus.status
    Slow = $slowStatus.status
    SlowElapsedMilliseconds = $slowStatus.elapsedMilliseconds
    SlowItemCount = $slowStatus.itemCount
    Failing = $failingStatus.status
    FailureDetailPresent = [bool]$failingStatus.error
} | Format-List
