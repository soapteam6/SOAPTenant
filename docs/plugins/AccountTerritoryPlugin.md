# Account Territory Plugin

## Business Purpose
Automatically assigns the correct sales **territory** (and its owning salesperson) to an account based on the account's postal code. This removes the need for staff to manually figure out and set the right territory every time an account is created or its address changes.

## Trigger
Runs automatically **when an Account is created or updated**, specifically reacting to changes in:
- Postal Code
- The "Named Account" flag (a manual override flag)
- The assigned Territory

## Business Logic
1. **Postal code drives territory assignment.** When an account is saved, the plugin looks up which territory covers that postal code and assigns the account (and its owner) to that territory automatically.
2. **Manual protection ("blocked reassignment").** If a territory has been marked as "locked"/protected against automatic reassignment, the account keeps its current territory rather than being moved automatically.
3. **Fallback search when no exact match exists.** If no territory is found for the exact postal code, the system tries broader matches:
   - First using a rounded version of the postal code (e.g., treating "81234" like "80000").
   - If still nothing is found, it falls back to a generic "Out of Territory" catch-all (00000).
4. **Ownership follows territory.** Once a territory is determined, the account is automatically reassigned to the salesperson who owns that territory.
5. **No match found.** If no territory can be determined at all, the account's territory is cleared rather than left incorrect.

## Why It Matters
Ensures every account is routed to the right regional sales owner automatically and consistently, reduces manual data entry errors, and still allows specific accounts to be protected from automatic reassignment when business rules require it (e.g., a named/strategic account with a dedicated owner).
