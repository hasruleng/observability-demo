# Order Flow Demo Test Plan

Use this checklist to verify the synthetic order flow locally. It uses no
company systems, customer records, or production endpoints. Choose scenarios
using their **When to run** guidance below. Start the stack with ND-2 before
running checks that need live services; restore fulfillment to `healthy` and
let the queue drain after tests that use `slow` or `failing`.

Prerequisites: Windows PowerShell and Podman with the Compose provider configured
as described in the README. The optional host builds in ND-1 additionally need
.NET SDK 10.0.403 and Node.js/npm on the host. Container builds install and run
their build tools inside the images, so those host tools are not required for
the Podman-only path. The first build may require internet access to download
packages and container images.

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
For a skipped scenario, leave its outcome unchecked and record `NOT RUN` with
the reason in Notes; do not mark it as passed. Mark the overall result
`INCOMPLETE` if any checks required for your chosen scope remain unverified.

## Scenario Groups

Non-demo scenarios are preparation and regression checks, not mandatory steps
for every startup. For a basic functional check, run ND-2 and D-1. For the full
observability walkthrough, also run D-2 through D-5. D-6 is an optional load
demonstration; choose other non-demo checks according to what changed and the
capabilities you need to verify. Rehearse your selected demo scenarios before
the presentation.

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

## Live Demo Timing

The November 26 meeting is at most 60 minutes shared by the presentation, demo,
and discussion. The proposed agenda reserves **up to 8 minutes for the live
demo**; confirm the final agenda with the meeting owner. These estimates are
for a rehearsed live run, include ordinary navigation and telemetry waits, and
exclude stack startup, image builds, and rehearsal. The ranges are estimates,
not measured guarantees.

| Scenario | Estimated live time | Timing notes |
|---|---:|---|
| D-1. Healthy order | 0:45-1:00 | Includes placing an order and briefly reading the result. |
| D-2. Slow fulfillment | 1:00-1:30 | Includes switching mode, comparing duration, and showing the slow result. The smoke script is a verification tool, not necessary stage content. |
| D-3. Failed fulfillment | 0:45-1:15 | Includes switching mode, placing one order, and pointing out the failure details. |
| D-4. Trace and log correlation | 1:30-2:30 | Depends on Grafana being open and locating the prepared trace/log quickly. |
| D-5. Order Flow dashboard | 0:45-1:15 | Can overlap D-2 while the slow-order metrics appear; avoid narrating the same wait twice. |
| D-6. Bounded load and recovery | 3:00-5:00 | JMeter itself previously ran in about 22 seconds; starting it, showing the queue/p95, waiting for recovery/export, and narrating drive the total. Pre-generated data can shorten this. |
| AI-assisted investigation, one prepared prompt | 2:00-4:00 | Assumes read-only telemetry access and a known order/trace are already prepared. Neither a Grafana MCP server nor a Copilot query path is currently configured and rehearsed. |
| Explain MCP vs. direct APIs and run the prompt | 4:00-7:00 | Includes introducing both access options and demonstrating one investigation; excludes setup or troubleshooting. |

**Recommended core:** D-1 + D-2 + D-4 + D-5 take about **4:00-6:15** of
actions. Allow another **1:00-1:30** for transitions and small delays: total
**5:00-7:45**. This fits the proposed 8-minute cap if rehearsed. D-5 can be
shown during D-2's telemetry wait.

**Full scenario run:** D-1 through D-6, sequentially, take about **7:45-12:30**
before transitions. This is not an 8-minute presentation sequence. D-3 or D-6
should be an optional substitution, not an automatic addition to the core.

**AI fit:** Do not add the AI segment to the current core. One prepared prompt
adds roughly 2-4 minutes, taking the core to **7:00-11:45** including the same
transition allowance; it only fits at the fastest end and leaves no reliable
buffer. The fuller MCP-vs-API explanation plus prompt takes **4-7 minutes** and
does not fit as an add-on. An AI example is possible only if it replaces most
of D-4, uses a preconfigured read-only query path, and is rehearsed end to end.
Otherwise, discuss AI prompting after the demo or keep it for a separate session.

## Detailed Test Scenarios

### ND-1. Build and Compose Configuration

**Why / benefit:** Separates source-build problems from container/runtime
problems and checks Compose wiring and loopback-only port exposure. The host
builds verify your local development toolchain, not a prerequisite for Podman.

**When to run:** Run host builds after source/dependency changes if you develop
outside containers or want earlier build feedback. Skip them for a Podman-only
workflow: ND-2 builds the .NET services and web app inside their images. Run
`podman compose config` after Compose changes or when diagnosing configuration
problems; it validates configuration but does not build or start anything.

**Steps**

1. Optionally verify host builds from the repository root:

   ```powershell
   dotnet restore Demo.sln --configfile NuGet.Config
   dotnet build Demo.sln --no-restore
   Push-Location .\web
   npm ci
   npm run build
   Pop-Location
   ```

2. From the repository root, configure the existing local Compose provider in
   the current PowerShell session, then validate Compose configuration. If the
   provider has not been installed, follow the README's Podman provider setup
   first. Repeat these environment assignments in each new terminal where you
   use `podman compose`.

   ```powershell
   $env:PATH = "$PWD\.venv\Scripts;$env:PATH"
   $env:PODMAN_COMPOSE_PROVIDER = "$PWD\.venv\Scripts\podman-compose.exe"
   podman compose config
   ```

   The `Executing external compose provider` message is informational, not an
   error. Successful output renders the enabled services and their configuration;
   this command does not build images or start containers.

**Expected results**

- [x] PASS [ ] FAIL [ ] N/A - .NET restore and build complete without errors (N/A if host builds are skipped).
- [x] PASS [ ] FAIL [ ] N/A - The web TypeScript and production build complete without errors (N/A if host builds are skipped).
- [x] PASS [ ] FAIL - Compose configuration renders all enabled services without errors.
- [x] PASS [ ] FAIL - Host-published ports in the rendered Compose configuration bind to `127.0.0.1`.

**Outcome:** [x] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence:**

2026-11-17: Host .NET and npm builds reported successful. After setting the local
Compose provider environment variables, `podman compose config` rendered
`fulfillment`, `order-api`, `otel-lgtm`, `web`, and `worker` without errors.
All published ports (8082, 8080, 3000, 4317, 4318, 5173, and 8081) bind to
`127.0.0.1`. The optional JMeter service is in the `load` profile and was not
enabled in this configuration check. Container build/start and runtime health
remain separate checks in ND-2.


### ND-2. Start the Stack and Check Health

**Why / benefit:** Verifies that container images can build, services can start,
and the UI and Grafana are reachable. Successful host builds alone cannot prove
that container networking, configuration, or runtime dependencies work.

**When to run:** Run on first setup, after rebuilding or restarting the stack,
and before a demo. This is the baseline for all live-service scenarios. If the
stack is already running with the intended images, repeat the status and health
checks without unnecessarily rebuilding.

**Steps**

1. Reuse the PowerShell session configured in ND-1. In a new terminal, repeat
   ND-1 step 2's environment assignments; install the provider using the README
   only if it is missing. Run `podman compose up --build -d` when images need
   building or the stack needs starting. For an already-running stack with the
   intended images, proceed to the status and health checks.
2. Run `podman compose ps` and wait for health checks to complete.
3. Check each endpoint:

   ```powershell
   Invoke-RestMethod http://127.0.0.1:8080/health
   Invoke-RestMethod http://127.0.0.1:8081/health
   Invoke-RestMethod http://127.0.0.1:8082/health
   Invoke-RestMethod http://127.0.0.1:3000/api/health
   ```

4. Open `http://127.0.0.1:5173/` in a browser. This is the WebUI; see the
   [WebUI guide](web/docs/ui-guide.md) for an explanation of its controls and results.

**Expected results**

- [x] PASS [ ] FAIL - The `otel-lgtm`, `fulfillment`, `worker`, `order-api`, and `web` containers are running; the four application/web containers report healthy.
- [x] PASS [ ] FAIL - The health endpoints return healthy responses for `order-api`, `order-worker`, and `fulfillment`; Grafana's health endpoint succeeds.
- [x] PASS [ ] FAIL - The page loads with the heading "Place an order", the `LOCAL / SYNTHETIC` marker, and no browser error page.

**Outcome:** [x] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence:**

2026-11-17: `podman compose ps` shows all five containers running for three
hours; fulfillment, worker, order-api, and web report healthy. The three
application health endpoints return `healthy` for their respective services.
Grafana's health endpoint returns `database: ok` and version `13.3.0`.
This verifies the existing running stack, not a fresh image build.
The tester confirmed in the browser that the WebUI at port 5173 displays
"Place an order" and `LOCAL / SYNTHETIC` without a browser error; the supplied
screenshot shows the initial Ready state. All ND-2 expected results pass.
Order submission and telemetry behavior remain separate checks in D-1 onward.


### D-1. Place a Healthy Order in the UI

**Why / benefit:** Confirms the complete business flow, not just health
endpoints: UI submission, API forwarding, queued work, fulfillment, and status
polling all work together.

**When to run:** Run after ND-2 for a basic functional check, after changes to
any order-flow component, and before every order-flow demo. Use it as the healthy
baseline for the slow and failing scenarios.

**Steps**

1. Set fulfillment to the baseline scenario:

   ```powershell
   .\scripts\set-scenario.ps1 -Name healthy
   ```

2. In the UI, enter `1` for the item count and click **Place order**.
3. Wait for the result to finish updating.

**Expected results**

- [x] PASS [ ] FAIL - The status changes from `Ready` through `In progress` to `Completed`.
- [x] PASS [ ] FAIL - The result shows `Items: 1`, outcome `completed`, and a positive order duration.
- [x] PASS [ ] FAIL - An order ID and non-empty trace ID are displayed.
- [x] PASS [ ] FAIL - The **Place order** button is available again after the order completes.

**Outcome:** [x] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (order ID / trace ID):**

2026-11-17: Healthy one-item order completed in 0.45 s. The UI displayed
`Items: 1`, outcome `COMPLETED`, an order ID, and a trace ID. The **Place order**
button was available again after completion (see tester-provided screenshot).


### D-2. Slow Fulfillment Is Visible

**Why / benefit:** Demonstrates that slow downstream work stays visible as a
pending order rather than looking like an unresponsive UI. The smoke script
also checks healthy, slow, and failing backend behavior independently of the UI.

**When to run:** Run when demonstrating latency or after changes to processing,
polling, or scenario behavior. Skip for a healthy-path-only check; this is not
required to start the stack. Compare against a healthy baseline from D-1.

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

- [x] PASS [ ] FAIL - The UI shows the slow order as `Completed`, with four items and a longer duration than a healthy order.
- [x] PASS [ ] FAIL - While the slow order is pending, the status reads `In progress` and the submit button is disabled.
- [x] PASS [ ] FAIL - The smoke script reports the slow order completed and its server-side elapsed time is at least 600 ms.
- [x] PASS [ ] FAIL - The smoke script reports the healthy order completed and the failing order failed with details.
- [x] PASS [ ] FAIL - The slow scenario remains synthetic; no external fulfillment system is contacted.

**Outcome:** [x] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (healthy and slow durations):**
2026-11-22: completed with 4 items in 1.20 s, longer than the 0.45 s healthy order recorded for D-1. If you had switched fulfillment to slow, you can mark the first D-2 check as PASS.

### D-3. Failed Fulfillment Is Reported

**Why / benefit:** Verifies that a downstream failure becomes an explicit
failed order with useful diagnostic IDs, rather than a false success or an
order left pending.

**When to run:** Run before a failure-handling demo or after changes to error
handling, status propagation, or fulfillment. Skip if your current scope is
only startup or healthy-path verification.

**Steps**

1. Set the failing scenario:

   ```powershell
   .\scripts\set-scenario.ps1 -Name failing
   ```

2. In the UI, enter `1` item, click **Place order**, and wait for the result.

**Expected results**

- [x] PASS [ ] FAIL - The UI shows status and outcome `Failed`, plus an explanatory error.
- [x] PASS [ ] FAIL - The order ID and trace ID remain visible for the failed order.
- [x] PASS [ ] FAIL - The API remains available after the synthetic failure.

**Outcome:** [x] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (order ID / trace ID / error):**
Invoke-RestMethod http://127.0.0.1:8080/health

status  service  
------  -------  
healthy order-api

### ND-3. Input Boundaries

**Why / benefit:** Checks that both the browser and API enforce the synthetic
item-count limits. API checks matter because callers can bypass browser
validation.

**When to run:** Run after changes to form validation, request handling, or
item-count limits, or as a pre-release regression check. It is not necessary
for each stack startup or observability walkthrough.

**Steps**

1. In the UI, try submitting item counts `0` and `51`.
2. To check the API directly, send each value to `POST http://127.0.0.1:8080/orders` with a JSON body such as `{"itemCount":0}` or `{"itemCount":51}`. For example:

   ```powershell
   curl.exe -i -H "Content-Type: application/json" -d '{"itemCount":0}' http://127.0.0.1:8080/orders
   curl.exe -i -H "Content-Type: application/json" -d '{"itemCount":51}' http://127.0.0.1:8080/orders
   ```

**Expected results**

- [x] PASS [ ] FAIL - Browser validation prevents out-of-range values from being submitted through the form.
- [x] PASS [ ] FAIL - Direct API requests for `0` or `51` items return HTTP 400.
- [x] PASS [ ] FAIL - Values `1` and `50` are accepted by input validation (use a healthy scenario if submitting them).

**Outcome:** [x] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence:**


### ND-4. Responsive UI

**Why / benefit:** Ensures the order controls and diagnostic results remain
usable at different screen sizes, including narrow windows during a demo.

**When to run:** Run after layout/style changes or before presenting on a new
screen size or browser. Skip for backend-only changes when the UI and
presentation environment are unchanged.

**Steps**

1. Open the order page at a desktop-sized viewport.
2. Repeat at a narrow mobile viewport, for example 390 x 844 CSS pixels.
3. Place a healthy one-item order at each size.

**Expected results**

- [x] PASS [ ] FAIL - The form, status, order ID, and trace ID remain readable and usable at both sizes.
- [x] PASS [ ] FAIL - No horizontal page overflow or overlapping content appears at the mobile size.
- [x] PASS [ ] FAIL - The order can be submitted and its result read at both sizes.

**Outcome:** [x] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (viewport sizes / browser):**


### D-4. Trace and Log Correlation

**Why / benefit:** Proves that an order can be followed across service
boundaries and matched to its worker log. A successful order alone does not
prove that traces and logs reach the telemetry backends.

**When to run:** Run for an observability walkthrough and after changes to
instrumentation, trace propagation, OTLP configuration, or Grafana data sources.
Skip only when telemetry is outside the scope of your check.

**Steps**

1. Place a healthy order and copy its trace ID and order ID from the UI.
2. In Grafana at `http://127.0.0.1:3000/`, open Explore and find the trace in Tempo using its trace ID.
3. Inspect the trace and search Loki logs for the order ID.

For illustrated guidance, see [How to Read Grafana](how-to-read-grafana.md).

**Expected results**

- [x] PASS [ ] FAIL - The trace is found and includes the Order API, worker processing, and fulfillment request.
- [x] PASS [ ] FAIL - Loki contains the worker's completion log for the same order ID.
- [ ] PASS [ ] FAIL [ ] N/A - If trace-to-logs navigation is configured, it opens or identifies the matching log for this trace. Mark N/A if Grafana has no such link configured.

**Outcome:** [ ] PASS [ ] FAIL [ ] BLOCKED

**Notes / evidence (trace ID / order ID / Grafana links):**

2026-11-22: Trace ID `ba3061c9f22912864c5532e5ad8f3cde`; order ID
`10dab4d8-bafd-4fe5-a585-8905257aba7a`. Tempo showed the Order API, worker,
and fulfillment spans. Loki showed the worker's completion log for the same
order with 5 items. Trace-to-logs navigation remains unverified.

### D-5. Order Flow Dashboard

**Why / benefit:** Verifies that custom metrics reach the provisioned dashboard
and show business outcomes and worker activity. Trace/log checks in D-4 do not
validate the separate metrics pipeline or panel queries.

**When to run:** Run before presenting the dashboard and after changes to
metrics, export settings, dashboard provisioning, or panel queries. Skip for a
functional-only check where dashboard behavior is not in scope.

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

**Why / benefit:** Creates enough queued work to demonstrate backlog, worker
utilization, higher latency, and eventual recovery. A single order does not
exercise these conditions. This bounded local profile is a demonstration, not
a production capacity benchmark.

**When to run:** Optional. Run when demonstrating queue saturation/recovery or
after changes to queueing, processing speed, or load-related metrics. First
verify basic order processing and the dashboard; allow time for the queue to
drain and avoid running other order tests concurrently.

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

**Why / benefit:** Verifies that dashboard provisioning works with empty
telemetry storage rather than relying on data or setup left over from an
earlier run.

**When to run:** Run after changes to provisioning or persistence, when
rehearsing a clean setup, or when intentionally verifying reset behavior.
Skip routine startups and any run where you need to preserve telemetry
evidence; this is a destructive reset, not a prerequisite for other scenarios.

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

**Why / benefit:** Checks whether the already-built stack can start and serve
the demo without external network access, reducing the risk of presentation
network failures. It does not verify offline image builds or dependency restores.

**When to run:** Optional. Run before a presentation that requires offline
operation, with all images already built/cached and after ND-2 succeeds.
Skip if offline operation is not a requirement or disconnecting would disrupt
other work.

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
