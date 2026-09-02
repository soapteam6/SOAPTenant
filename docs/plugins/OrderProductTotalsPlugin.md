# Order Product Totals Plugin

## Business Purpose
Keeps the **Sales Order's** total equipment price and total cost automatically up to date as product lines are added, changed, or removed — so no one has to manually re-add up the order lines.

## Trigger
Runs automatically whenever a **Sales Order product line** is:
- **Created**
- **Updated** — specifically when its Price or Cost changes
- **Deleted**

## Business Logic
1. Whenever a product line on a Sales Order changes, the plugin identifies which Sales Order it belongs to.
2. It then re-adds up **all** product lines currently on that Sales Order:
   - **Total Equipment Value** = sum of every line's price.
   - **Total Cost** = sum of every line's cost.
3. These two totals are written back onto the parent Sales Order record immediately.

## Why It Matters
Ensures the Sales Order always shows accurate, current totals in real time — whether a line item was just added, its price was changed, or it was removed entirely — eliminating manual recalculation and reducing the risk of stale or incorrect order totals.
