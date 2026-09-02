# Quote Product Cost Plugin

## Business Purpose
Automatically calculates the internal **cost** of a product line on a Quote, so sales staff don't have to manually look up pricing/cost information or do the math themselves. This ensures cost figures on quotes are always accurate and consistent with the current price list before the quote is turned into an order.

## Trigger
Runs automatically when a **Quote product line** is:
- **Created**.
- **Updated** — specifically when the Product, Price, or Quantity changes.

## Business Logic
1. The plugin finds the **price list** attached to the parent Quote.
2. It checks whether the specific product has a listed cost on that price list:
   - **If a matching price list entry exists** → cost = (unit price from the price list) × (quantity quoted).
   - **If no matching entry exists** → the cost is estimated using the price list's general pricing method:
	 - **Markup pricing** — works backward from the sale price and the price list's markup percentage to estimate cost.
	 - **Margin pricing** — works backward from the sale price and the price list's margin percentage to estimate cost.
3. The calculated cost is saved directly onto the quote line at the same time it's created or updated — there's no separate step or delay.

## Why It Matters
Gives sales staff an accurate, consistent cost estimate on every quote line as soon as it's entered, so profitability can be reviewed before the quote is sent to the customer or converted into an order.
