# Order Product Cost Plugin

## Business Purpose
Automatically calculates the internal **cost** of a product line on a Sales Order, so sales staff don't have to manually look up pricing/cost information or do the math themselves. This ensures cost figures are always accurate and consistent with the current price list.

## Trigger
Runs automatically when a **Sales Order product line** is:
- **Created**.
- **Updated** — specifically when the Product, Price, or Quantity changes.

## Business Logic
1. The plugin finds the **price list** attached to the parent Sales Order.
2. It checks whether the specific product has a listed cost on that price list:
   - **If a matching price list entry exists** → cost = (unit price from the price list) × (quantity ordered).
   - **If no matching entry exists** → the cost is estimated using the price list's general pricing method:
	 - **Markup pricing** — works backward from the sale price and the price list's markup percentage to estimate cost.
	 - **Margin pricing** — works backward from the sale price and the price list's margin percentage to estimate cost.
3. The calculated cost is saved directly onto the order line at the same time it's created or updated — there's no separate step or delay.

## Why It Matters
Guarantees that every order line always carries an accurate, consistent cost figure calculated the same way every time, which feeds into profitability reporting and order totals without relying on manual entry.
