# Remaining Review Findings: Canonical Contracts

This document freezes the shared contracts for the remediation plan. Production code, tests, migrations and UI changes must preserve these rules.

## Background job state machine

```text
Queued --TryStart(token, leaseUntil)--> Running
Running --TryRenew(same token, unexpired ownership)--> Running
Running --TrySucceed(same token)--> Succeeded + release logical lock
Running --TryFail(same token, sanitized error)--> Failed + release logical lock
Expired Running --atomic recovery predicate still true--> Failed + release logical lock
Terminal states never transition back to Running and never mutate results.
```

Every state mutation is conditional on the current status and, for an owned run, the current lease token. Terminal transitions set `completed_at_utc`, clear lease ownership, and replace the logical lock key with `released:{runId}`. Historical rows remain queryable.

## Payment callback contract

The endpoint remains `POST /api/checkout/payment/callback` and keeps the published JSON envelope.

| Condition | Response |
| --- | --- |
| Valid first callback | `202` or `200`; effects committed once |
| Duplicate provider and `externalEventId` | `200`; no repeated effect |
| Missing or malformed required provider fields | `400` |
| Invalid signature or unknown provider | `401` |
| Verified callback references no payment | `404` |
| Verified callback conflicts with method, amount or terminal state | `409` |

Verification order is fixed: structural/malformed checks run first and map to `400`; signature verification (including unknown provider) maps to `401`. A callback is a duplicate only when the verified callback was already committed, and returns `200` with no repeated effect.

Webhook secrets are bound from `Payments:Webhooks:VNPay:Secret` and `Payments:Webhooks:MoMo:Secret`. A missing, empty or unparseable secret is a configuration failure: the verifier fails closed with a safe server-side error and never treats the callback as having a valid signature.

Amounts use exact decimal parsing with `InvariantCulture`. A missing response code is invalid and never means success. VNPay callbacks are accepted only for `PaymentMethod.VNPay`; MoMo callbacks only for `PaymentMethod.MoMo`. Verification happens before database effects. Callback uniqueness is `(provider, external_event_id)` and processing is idempotent under retries and concurrency.

`ErrorMessage`/`ErrorCode` values distinguish malformed (`MALFORMED_CALLBACK`), signature (`INVALID_SIGNATURE`) and configuration failures, and never carry exception details. `SanitizedPayload` is an allow-list of verified values (`externalEventId`, `orderId`, `amount`, status code, provider) — never a serialization of the raw request dictionary. Responses and persisted summaries never expose signatures, secrets, tokens or raw stack traces.

## Payment state machine

```text
COD -> PendingCollection      online -> Pending
Pending|PendingCollection|Processing --MarkCompleted(transactionId)--> Completed
Pending|PendingCollection|Processing --MarkFailed--> Failed
Completed|Failed|Refunded: terminal, no transition out; no re-entry into Completed or Failed
```

`Payment.Create` sets `Pending` for a pay-later provider (`VNPay`, `MoMo`) and `PendingCollection` for `COD`. `PendingCollection` is the valid pre-collection state for cash-on-delivery orders and transitions to `Completed` or `Failed` exactly like `Pending`. `Processing` is a permitted intermediate state with the same transitions. `MarkCompleted` requires a non-empty provider transaction id; an invalid transition preserves status, provider transaction id, provider response and completion timestamp.

## Database schema contract

The canonical `background_job_runs` columns are (snake_case): `id` (primary key), `job_name` (varchar(100)), `lock_key` (varchar(100)), `branch_id` (char(36), nullable), `status` (string enum, varchar(20)), `created_at_utc`, `started_at_utc`, `completed_at_utc`, `error_summary` (varchar(1000)), `lock_token` (varchar(50)), `lease_expires_at_utc`. There is no `run_id` column and no result columns; the primary key is `id`. The table has a unique lock slot on `(job_name, lock_key)` and an index on `branch_id`. Released locks use `released:{runId}` while history rows remain queryable.

Payment callbacks retain the unique `(provider, external_event_id)` constraint. Inventory mutations retain the unique `operation_key` constraint. Schema changes are forward-only through new migrations.

## API and UI behavior

- `GET /api/products/{productId}/review-eligibility?orderItemId={optionalGuid}` verifies the target against product, authenticated user and completed order before returning an order item for review.
- A product view event with an unknown `branchId` returns `404 BRANCH_NOT_FOUND` before insertion. An omitted branch remains valid.
- Review pages count before calculating pagination, clamp the requested page to `[1, totalPages]`, and use page 1 for an empty collection.
- `GET /reviews/{id}` does not publicly return a review whose product is inactive.
- Job polling uses a 600 ms interval and at most 200 attempts, aborts on unmount or branch change, and stops on `Succeeded` or `Failed`.
- Admin UI renders review forms only with the backend-returned `eligibility.orderItemId` and reports rejected deep links with a stable invalid-link message.
- Forecast creation rejects undefined `ForecastDataQuality` enum values before persistence.
