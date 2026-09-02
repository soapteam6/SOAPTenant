# EA Table Owner Assignment Plugin

## Business Purpose
Automatically assigns the correct owner to newly created "EA" records (and similar related tables) based on the account or customer they belong to — so records aren't left unassigned or owned by the wrong person, and staff don't have to manually set ownership every time.

## Trigger
Runs automatically when a new record is **created** on any table it's registered against (e.g., any `soap_ea*` table, or another table that is linked to an account or customer).

## Business Logic
1. **Account takes priority.** If the new record is linked to an Account, the owner is copied from that Account.
2. **Otherwise, use the customer link.** If there's no Account link, the plugin looks at the record's customer relationship instead, preferring a dedicated "EA Customer" field but falling back to a general "Customer" field if that's not present.
3. **Owner is copied automatically.** Whichever related record is found, its current owner is looked up and applied to the new record automatically.
4. **No match found.** If neither an Account nor a Customer relationship can be resolved, the record is left as-is (no owner is forced) and the situation is logged for troubleshooting.

## Why It Matters
Ensures new EA-related records are automatically routed to the right salesperson/owner based on their related account or customer, keeping ownership consistent across related records without requiring manual assignment, and reducing the risk of orphaned or misassigned records.
