# Account Type Plugin

## Business Purpose
Automatically classifies each account as **Active**, **Inactive**, or **None**, based on whether the account is a currently registered business (identified by having an EA Number) and whether it is active or deactivated in the system. This keeps the account's classification always accurate without requiring manual updates.

## Trigger
Runs automatically when:
- An Account is **created**.
- An Account is **updated** — specifically when its **Status** or **EA Number** field changes.
- An Account's status is changed via the **Activate/Deactivate** action.

## Business Logic
The account is classified as follows:
- **Active account** — the account is currently active AND has an EA Number on file → classified as **Active**.
- **Inactive account** — the account has been deactivated AND still has an EA Number on file → classified as **Inactive**.
- **None** — in all other cases (e.g., no EA Number present), the account is not classified into either category.

This classification is recalculated every time the account is created, updated, or its active/inactive status changes, so it always reflects the current state.

## Why It Matters
Gives sales and operations teams a reliable, always up-to-date way to filter and report on accounts that are registered businesses (have an EA Number) versus those that aren't, and to distinguish currently active customers from ones that have gone inactive.
