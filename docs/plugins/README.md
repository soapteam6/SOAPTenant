# SOAP CRM Plugins — Business Documentation

This folder documents each automated business rule (plugin) running in the CRM, written for a non-technical audience. Each document explains **when** the automation runs (trigger) and **what** it does (logic), from a business perspective.

## Plugins

| Plugin | Area | Summary |
|---|---|---|
| [Account Sharing Plugin](AccountSharingPlugin.md) | Accounts | Grants/removes account access automatically when the account owner changes. |
| [Account Territory Plugin](AccountTerritoryPlugin.md) | Accounts | Assigns the correct sales territory and owner to an account based on postal code. |
| [Account Type Plugin](AccountTypePlugin.md) | Accounts | Classifies accounts as Active/Inactive based on status and EA Number. |
| [EA Table Owner Assignment Plugin](EaTableOwnerAssignmentPlugin.md) | EA Tables | Assigns the owner of a new EA-related record based on its linked account or customer. |
| [Order Product Cost Plugin](OrderProductCostPlugin.md) | Sales Orders | Calculates the cost of each product line on a sales order. |
| [Order Product Totals Plugin](OrderProductTotalsPlugin.md) | Sales Orders | Keeps sales order totals (price and cost) in sync with its product lines. |
| [Quote Product Cost Plugin](QuoteProductCostPlugin.md) | Quotes | Calculates the cost of each product line on a quote. |
| [Quote Product Totals Plugin](QuoteProductTotalsPlugin.md) | Quotes | Keeps quote totals (price and cost) in sync with its product lines. |
