# Order Flow Demo Test Plan

Use this checklist to verify the synthetic order flow locally. It uses no
company systems, customer records, or production endpoints. Run the scenarios
in order; restore fulfillment to `healthy` and let the queue drain after tests
that use `slow` or `failing`.

Prerequisites: Windows PowerShell, .NET SDK 10.0.403, Node.js/npm for the web
build, and Podman with the Compose provider configured as described in the
README. The first restore/build may require internet access to download packages
and container images.

## Run Record

| Field | Value |
|---|---|
| Test date (include year) | |
| Tester | |
| Demo commit | |
| OS and version | |
| Podman version | |
| Browser and version | |
| Overall result | [ ] PASS [ ] FAIL [ ] INCOMPLETE |

For each scenario, mark the expected-result checks and record useful evidence,
observations, or failure details in its Notes field.

## Scenario Groups

Complete the non-demo checks before the presentation. Use the demo group as the
live walkthrough sequence; it focuses on visible order outcomes and the
observability story.

### Non-Demo Scenarios

- [ND-1. Build and Compose Configuration](#nd-1-build-and-compose-configuration)
- [ND-2. Start the Stack and Check Health](#nd-2-start-the-stack-and-check-health)
- [ND-3. Input Boundaries](#nd-3-input-boundaries)
- [ND-4. Responsive UI](#nd-4-responsive-ui)
- [ND-5. Clean Reset and Dashboard Provisioning](#nd-5-clean-reset-and-dashboard-provisioning)
- [ND-6. Optional: Disconnected Start](#nd-6-optional-disconnected-start)

### Demo Scenarios

- [D-1. Place a Healthy Order in the UI](#d-1-place-a-healthy-order-in-the-ui)
- [D-2. Slow Fulfillment Is Visible](#d-2-slow-fulfillment-is-visible)
- [D-3. Failed Fulfillment Is Reported](#d-3-failed-fulfillment-is-reported)
- [D-4. Trace and Log Correlation](#d-4-trace-and-log-correlation)
- [D-5. Order Flow Dashboard](#d-5-order-flow-dashboard)
- [D-6. Bounded Load and Queue Recovery](#d-6-bounded-load-and-queue-recovery)

## Detailed Test Scenarios

### ND-1. Build and Compose Configuration

**Steps**

1. From the repository root, run:

   ```powershell
   dotnet restore Demo.sln --configfile NuGet.Config
   dotnet build Demo.sln --no-restore
   Push-Location .\web
   npm ci
   npm run build
   Pop-Location
   podman compose config
   ```

**Expected results**

- [ ] PASS [ ] FAIL - .NET restore and build complete without errors.
- [ ] PASS [ ] FAIL - The web TypeScript and production build complete without errors.
- [ ] PASS [ ] FAIL - Compose configuration renders all services without errors.
- [ ] PASS [ ] FAIL - Host-published ports in the rendered Compose configuration bind to `127.0.0.1`.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence:**


### ND-2. Start the Stack and Check Health

**Steps**

1. Follow the README's Podman provider setup if needed, then run `podman compose up --build -d`.
2. Run `podman compose ps` and wait for health checks to complete.
3. Check each endpoint:

   ```powershell
   Invoke-RestMethod http://127.0.0.1:8080/health
   Invoke-RestMethod http://127.0.0.1:8081/health
   Invoke-RestMethod http://127.0.0.1:8082/health
   Invoke-RestMethod http://127.0.0.1:3000/api/health
   ```

4. Open `http://127.0.0.1:5173/` in a browser.

**Expected results**

- [ ] PASS [ ] FAIL - The `otel-lgtm`, `fulfillment`, `worker`, `order-api`, and `web` containers are running; the four application/web containers report healthy.
- [ ] PASS [ ] FAIL - The health endpoints return healthy responses for `order-api`, `order-worker`, and `fulfillment`; Grafana's health endpoint succeeds.
- [ ] PASS [ ] FAIL - The page loads with the heading "Place an order", the `LOCAL / SYNTHETIC` marker, and no browser error page.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence:**


### D-1. Place a Healthy Order in the UI

**Steps**

1. Set fulfillment to the baseline scenario:

   ```powershell
   .\scripts\set-scenario.ps1 -Name healthy
   ```

2. In the UI, enter `1` for the item count and click **Place order**.
3. Wait for the result to finish updating.

**Expected results**

- [ ] PASS [ ] FAIL - The status changes from `Ready` through `In progress` to `Completed`.
- [ ] PASS [ ] FAIL - The result shows `Items: 1`, outcome `completed`, and a positive order duration.
- [ ] PASS [ ] FAIL - An order ID and non-empty trace ID are displayed.
- [ ] PASS [ ] FAIL - The submit button is available again after the order completes.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (order ID / trace ID):**


### D-2. Slow Fulfillment Is Visible

**Steps**

1. Set the slow scenario:

   ```powershell
   .\scripts\set-scenario.ps1 -Name slow
   ```

2. In the UI, enter `4` items, click **Place order**, and wait for completion.
3. Run the scripted healthy, slow, and failing cases:

   ```powershell
   .\scripts\smoke.ps1
   ```

**Expected results**

- [ ] PASS [ ] FAIL - The UI shows the slow order as `Completed`, with four items and a longer duration than a healthy order.
- [ ] PASS [ ] FAIL - While the slow order is pending, the status reads `In progress` and the submit button is disabled.
- [ ] PASS [ ] FAIL - The smoke script reports the slow order completed and its server-side elapsed time is at least 600 ms.
- [ ] PASS [ ] FAIL - The smoke script reports the healthy order completed and the failing order failed with details.
- [ ] PASS [ ] FAIL - The slow scenario remains synthetic; no external fulfillment system is contacted.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (healthy and slow durations):**


### D-3. Failed Fulfillment Is Reported

**Steps**

1. Set the failing scenario:

   ```powershell
   .\scripts\set-scenario.ps1 -Name failing
   ```

2. In the UI, enter `1` item, click **Place order**, and wait for the result.

**Expected results**

- [ ] PASS [ ] FAIL - The UI shows status and outcome `Failed`, plus an explanatory error.
- [ ] PASS [ ] FAIL - The order ID and trace ID remain visible for the failed order.
- [ ] PASS [ ] FAIL - The API remains available after the synthetic failure.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (order ID / trace ID / error):**


### ND-3. Input Boundaries

**Steps**

1. In the UI, try submitting item counts `0` and `51`.
2. To check the API directly, send each value to `POST http://127.0.0.1:8080/orders` with a JSON body such as `{"itemCount":0}` or `{"itemCount":51}`. For example:

   ```powershell
   curl.exe -i -H "Content-Type: application/json" -d '{"itemCount":0}' http://127.0.0.1:8080/orders
   curl.exe -i -H "Content-Type: application/json" -d '{"itemCount":51}' http://127.0.0.1:8080/orders
   ```

**Expected results**

- [ ] PASS [ ] FAIL - Browser validation prevents out-of-range values from being submitted through the form.
- [ ] PASS [ ] FAIL - Direct API requests for `0` or `51` items return HTTP 400.
- [ ] PASS [ ] FAIL - Values `1` and `50` are accepted by input validation (use a healthy scenario if submitting them).

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence:**


### ND-4. Responsive UI

**Steps**

1. Open the order page at a desktop-sized viewport.
2. Repeat at a narrow mobile viewport, for example 390 x 844 CSS pixels.
3. Place a healthy one-item order at each size.

**Expected results**

- [ ] PASS [ ] FAIL - The form, status, order ID, and trace ID remain readable and usable at both sizes.
- [ ] PASS [ ] FAIL - No horizontal page overflow or overlapping content appears at the mobile size.
- [ ] PASS [ ] FAIL - The order can be submitted and its result read at both sizes.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (viewport sizes / browser):**


### D-4. Trace and Log Correlation

**Steps**

1. Place a healthy order and copy its trace ID and order ID from the UI.
2. In Grafana at `http://127.0.0.1:3000/`, open Explore and find the trace in Tempo using its trace ID.
3. Inspect the trace and search Loki logs for the order ID.

**Expected results**

- [ ] PASS [ ] FAIL - The trace is found and includes the Order API, worker processing, and fulfillment request.
- [ ] PASS [ ] FAIL - Loki contains the worker's completion log for the same order ID.
- [ ] PASS [ ] FAIL [ ] N/A - If trace-to-logs navigation is configured, it opens or identifies the matching log for this trace. Mark N/A if Grafana has no such link configured.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (trace ID / order ID / Grafana links):**


### D-5. Order Flow Dashboard

**Steps**

1. Set fulfillment to `healthy` and place several orders.
2. Open the provisioned **Order Flow** dashboard in Grafana.
3. Wait for telemetry to export and refresh the dashboard.

**Expected results**

- [ ] PASS [ ] FAIL - The dashboard loads with panels for order duration p95, worker queue depth, active worker, and order outcomes.
- [ ] PASS [ ] FAIL - Completed orders appear in the outcomes panel and duration data appears in the p95 panel after telemetry is exported.
- [ ] PASS [ ] FAIL - During idle time, queue depth and active worker return to zero.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (observed values / time range):**


### D-6. Bounded Load and Queue Recovery

**Steps**

1. Ensure no other test is submitting orders and open the Order Flow dashboard.
2. Set fulfillment to `slow`:

   ```powershell
   .\scripts\set-scenario.ps1 -Name slow
   ```

3. Run the bounded JMeter profile:

   ```powershell
   podman compose --profile load run --rm jmeter
   ```

4. Observe the queue, active worker, order duration, and outcomes panels during and after the run.
5. Wait for queue depth to return to zero, then restore `healthy`.

**Expected results**

- [ ] PASS [ ] FAIL - JMeter finishes its configured four-thread, 20-iteration-per-thread run (at most 80 synthetic order requests) against the local Order API.
- [ ] PASS [ ] FAIL - The JMeter summary reports the sample count and error count; record both below. Review any request errors rather than treating them as expected.
- [ ] PASS [ ] FAIL - Queue depth rises above zero and the single worker is shown active while work is being processed.
- [ ] PASS [ ] FAIL - Order duration p95 rises relative to the idle/healthy baseline during the slow load.
- [ ] PASS [ ] FAIL - Wait for at least one metric export interval after the load (10 seconds) before judging final queue and active-worker values.
- [ ] PASS [ ] FAIL - The queue drains back to zero and active worker returns to zero after the run.
- [ ] PASS [ ] FAIL - Fulfillment is restored to `healthy`.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (JMeter samples / errors / peak queue / p95 / recovery time):**


### ND-5. Clean Reset and Dashboard Provisioning

**Warning:** `podman compose down --volumes` deletes this demo's local telemetry
volume and its stored telemetry data.

**Steps**

1. Set fulfillment to `healthy` and stop the stack with a full local reset:

   ```powershell
   .\scripts\set-scenario.ps1 -Name healthy
   podman compose down --volumes
   podman compose up -d
   ```

2. Wait for application health checks, then open Grafana and the Order Flow dashboard.

**Expected results**

- [ ] PASS [ ] FAIL - The stack starts again without rebuilding and the application health endpoints succeed.
- [ ] PASS [ ] FAIL - The Order Flow dashboard is provisioned after restart.
- [ ] PASS [ ] FAIL - Telemetry history from before the volume reset is no longer present.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence:**


### ND-6. Optional: Disconnected Start

This check is not yet verified. Run it only when the required images and
dependencies are already cached locally.

**Steps**

1. With the Podman machine running and the images built, stop the stack using `podman compose down` (without `--volumes`).
2. Disconnect the host from external networks.
3. Run `podman compose up -d` without `--build`, then check application health and open the UI and Grafana.

**Expected results**

- [ ] PASS [ ] FAIL - The stack starts using cached images without pulling images or downloading dependencies.
- [ ] PASS [ ] FAIL - Health checks pass and the UI and Grafana load with no external network connection.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED [ ] NOT RUN

**Notes / evidence:**
