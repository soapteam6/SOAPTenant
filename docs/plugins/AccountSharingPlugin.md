# Account Sharing Plugin

## Business Purpose
Keeps account access rights in sync whenever an account changes owner. When a salesperson takes over (or gives up) an account, this automation makes sure they immediately gain (or lose) the ability to view and work with that account — without anyone having to manually update security/sharing settings.

## Trigger
Runs automatically **after** an Account record is:
- **Created** — when a new account is saved with an owner assigned.
- **Updated** — but only when the account's **Owner** field actually changes.

## Business Logic
1. **New account created** → the person set as the owner is automatically given access (Read, Write, and the ability to add related records) to that account.
2. **Account reassigned to a new owner** → 
   - The **new owner** is granted access to the account.
   - The **previous owner** has their access removed, so they no longer see or edit an account they no longer manage.
3. If the owner hasn't actually changed, nothing happens.
4. If the account is owned by a **team** rather than an individual user, sharing is skipped for that owner (teams already have access through their own security setup).
5. If granting or revoking access fails for any reason (e.g., access already exists or never existed), the process continues without stopping the overall save — it simply logs the issue for troubleshooting.

## Why It Matters
Prevents former account owners from retaining visibility into accounts they no longer manage, and ensures new owners can immediately start working with their accounts — supporting data security and smooth account handoffs.
