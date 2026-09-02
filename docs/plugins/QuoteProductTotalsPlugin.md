# Quote Product Totals Plugin

## Business Purpose
Keeps the **Quote's** total equipment price and total cost automatically up to date as product lines are added, changed, or removed — so no one has to manually re-add up the quote lines.

## Trigger
Runs automatically whenever a **Quote product line** is:
- **Created**
- **Updated** — specifically when its Price or Cost changes
- **Deleted**

## Business Logic
1. Whenever a product line on a Quote changes, the plugin identifies which Quote it belongs to.
2. It then re-adds up **all** product lines currently on that Quote:
   - **Total Equipment Value** = sum of every line's price.
   - **Total Cost** = sum of every line's cost.
3. These two totals are written back onto the parent Quote record immediately.

## Why It Matters
Ensures the Quote always shows accurate, current totals in real time — whether a line item was just added, its price was changed, or it was removed entirely — giving sales reps confidence in the numbers they present to customers, and reducing the risk of stale or incorrect quote totals.
