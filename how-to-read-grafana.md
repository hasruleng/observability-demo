# How to Read Grafana

This guide follows one synthetic order through the local Grafana stack. Start
the demo stack first; the order UI is at <http://127.0.0.1:5173/> and Grafana
is at <http://127.0.0.1:3000/>.

## Contents

- [Find an order trace](#find-an-order-trace)
- [Read the example trace](#read-the-example-trace)
- [Find the matching log](#find-the-matching-log)
- [Read the example Loki results](#read-the-example-loki-results)
- [Read the Order Flow dashboard](#read-the-order-flow-dashboard)
- [If data is missing](#if-data-is-missing)

## Find an order trace

1. From the repository root, set fulfillment to the healthy scenario:

   ```powershell
   .\scripts\set-scenario.ps1 -Name healthy
   ```

2. Place an order in the UI and copy both its **Trace ID** and **Order ID**.
   The trace ID identifies the submission trace; the order ID identifies the
   order and helps locate its worker log.
3. Open Grafana, select **Explore**, and choose **Tempo** as the data source.
4. Select the **Trace ID** query type, paste the trace ID into its field, and
   run the query with the blue button at the top right.

## Read the example trace

![Tempo trace for a synthetic order, showing the Order API, worker, and fulfillment spans](grafana/images/grafana-tempo-trace.png)

The screenshot shows a trace found successfully by its ID. Its values are an
example, not thresholds your own order must match.

- **Trace summary:** `order-api POST /orders`, `202`, **3 services**, and
  **6 spans**. A trace groups related operations; each span represents one
  operation. HTTP `202 Accepted` means the order was accepted for processing,
  not that fulfillment already completed.
- **Duration 78.64 ms:** the time covered by this trace in the screenshot.
  The root `POST /orders` span took only 2.17 ms; the background work continued
  after the API accepted the order. The UI's final duration can be longer
  because it includes browser communication and status-polling delay.
- **Service & Operation:** read the indented rows as parent/child relationships.
  `order-api POST /orders` receives the request and forwards work;
  `order-worker POST /work` accepts it; `order.process` is the worker's
  background processing; the nested `POST` calls fulfillment, and
  `fulfillment POST /process` handles that call.
- **Timeline bars:** the horizontal position shows when a span started relative
  to the trace; its length shows how long it lasted. The long fulfillment bar
  is about 76 ms here, accounting for most of this trace's elapsed time.
  Nested spans overlap, so do not add their durations together.
- **Expand a row** to inspect its attributes and timing. For example, the
  `order.process` span includes `order.id` and `item.count` attributes.

The screenshot is evidence that the trace contains the API, worker, and
fulfillment spans. It does not by itself prove that the matching log was found.
Later UI status-poll requests are separate requests, not part of this submission
trace.

## Find the matching log

1. Keep the **order ID** from the same UI result. Optionally try **Related logs**
   in the trace view; verify any result matches the order ID. The link's
   presence alone does not prove that trace-to-logs navigation is configured.
2. To search directly, open **Explore** and select **Loki** as the data source.
   Under **Label filters**, select `service_name` = `order-worker`. Under
   **Line contains**, paste the complete order ID into **Text to find**. Set a
   time range that includes the order and run the query.
3. Look for `Order <order ID> completed with <item count> items`. A failing
   order instead produces a worker failure log. Confirm that the ID matches
   the order you traced.

For [D-4 in the test plan](TEST-PLAN.md#d-4-trace-and-log-correlation), record
the trace and order IDs and the log result. If trace-to-logs navigation is not
configured, mark that optional check N/A; a direct Loki search is still needed
for the matching-log check.

## Read the example Loki results

![Loki results showing worker logs filtered by a synthetic order ID](grafana/images/grafana-loki-order-logs.png)

- **Query:** `service_name = order-worker` selects the worker's logs; **Line
  contains** restricts the results to the order ID. The orange highlights show
  where the ID matched. The line filter may match request paths as well as
  completion messages.
- **Logs volume:** the bar shows five matching log lines near the time of this
  order. It counts log entries, not orders.
- **Completion entry:** the middle line says the order completed with **5
  items**, with severity `INFO` and `scope_name=OrderProcessor`. Its order ID
  matches the filter. Its `TraceId` matches the trace ID in the earlier Tempo
  screenshot, connecting this worker completion log to that submission trace.
- **Request starting / Request finished:** the other entries describe `GET
  /work/<order ID>` requests to the worker, made when the Order API checks
  status. Their HTTP `200` means the status request succeeded; it does not by
  itself mean the order completed. These polling requests have separate trace
  IDs from the original submission.
- **Time and ordering:** each line has a timestamp, and the query direction is
  **Backward**, so newer entries appear first. Read bottom to top to follow
  these events forward in time.

The completion entry supports the matching-log check in D-4. It does not by
itself verify that the **Related logs** link navigates to it; test that
separately if the link is configured.

## Read the Order Flow dashboard

Open the **Order Flow** dashboard in Grafana. Its panels summarize multiple
orders over time rather than describing just the trace above:

- **Order duration p95:** an estimated 95th-percentile worker submission-to-
  completion duration, calculated from histogram data over a five-minute
  window. It is not the duration of a particular order.
- **Worker queue depth:** orders waiting for the worker. A rising queue can
  indicate orders are arriving faster than they are processed.
- **Active worker:** orders currently being processed by the single worker;
  it should return to zero when processing finishes.
- **Order outcomes:** completed and failed order rates per second over a
  five-minute window, not raw order counts.

Metrics are exported every 10 seconds. Allow time for export and refresh the
dashboard before judging a panel. See
[D-5 in the test plan](TEST-PLAN.md#d-5-order-flow-dashboard).

## If data is missing

- Check that the stack is running and the order finished. Wait briefly for
  telemetry to arrive and rerun the query.
- Confirm that you selected **Tempo** for a trace ID or **Loki** for an order
  log, and copied the appropriate ID from the same order.
- Expand the Grafana time range to include when the order was placed. The
  screenshot's trace ID is an example and may no longer be present locally.
- In Loki, check the selected labels and line filter; an overly restrictive
  selector can hide the log. If necessary, inspect available labels first.
- Telemetry history is lost after the demo's data volume is reset.
