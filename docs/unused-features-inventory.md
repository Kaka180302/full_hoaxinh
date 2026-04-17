# Unused Features Inventory (Non-Breaking)

This list is for tracking only. No feature is removed in this pass.

## Candidate: `StoreController.TrackOrderLookup`
- Route: `GET /Store/TrackOrderLookup`
- Observation: main UI links/forms call `TrackOrder` (GET/POST), not `TrackOrderLookup`.
- Safe action applied: keep as-is for backward compatibility.
- Suggested next step: add telemetry/log usage for 1-2 weeks, then deprecate only if no traffic.

## Duplicate checkout business logic (resolved)
- Previous state: checkout rules existed in both `StoreController.Checkout` and `Api/OrdersController.Create`.
- Action applied: both flows now call shared `IOrderCheckoutService`.
- Benefit: lower regression risk, easier future changes for payment/inventory rules.
