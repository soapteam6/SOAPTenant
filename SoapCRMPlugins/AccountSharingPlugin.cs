using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SoapCRMPlugins;
using System;

namespace SoapCRMPlugins
{
    /// <summary>
    /// Plugin that manages sharing of account records when ownership changes.
    /// Shares the account with the new owner and removes sharing from the previous owner.
    /// Registered on Post-Operation (Create/Update) of Account entity.
    /// </summary>
    public class AccountSharingPlugin : PluginBase
    {
        public AccountSharingPlugin() : base(typeof(AccountSharingPlugin)) { }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
                throw new ArgumentNullException(nameof(localPluginContext));

            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.SystemUserService; // Use system service for sharing operations

            localPluginContext.Trace($"AccountSharingPlugin: Processing {context.MessageName} message");

            Entity targetAccount = null;
            Guid accountId = Guid.Empty;
            EntityReference newOwner = null;
            EntityReference oldOwner = null;

            // Get the target entity
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                targetAccount = (Entity)context.InputParameters["Target"];
                accountId = targetAccount.Id;
            }

            if (accountId == Guid.Empty)
            {
                localPluginContext.Trace("AccountSharingPlugin: No valid account ID found");
                return;
            }

            // Create: Get new owner from target or retrieve the created account
            if (context.MessageName.ToLower() == "create")
            {
                if (targetAccount.Contains("ownerid"))
                {
                    newOwner = targetAccount.GetAttributeValue<EntityReference>("ownerid");
                }

                localPluginContext.Trace($"AccountSharingPlugin (Create): New Owner: {newOwner?.Id}");
            }
            // Update: Check if owner changed
            else if (context.MessageName.ToLower() == "update")
            {
                // Check if ownerid is being updated
                if (!targetAccount.Contains("ownerid"))
                {
                    localPluginContext.Trace("AccountSharingPlugin: Owner not being updated");
                    return;
                }

                newOwner = targetAccount.GetAttributeValue<EntityReference>("ownerid");

                // Get old owner from PreImage
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    Entity preImage = (Entity)context.PreEntityImages["PreImage"];
                    oldOwner = preImage.GetAttributeValue<EntityReference>("ownerid");
                }

                localPluginContext.Trace($"AccountSharingPlugin (Update): Old Owner: {oldOwner?.Id}, New Owner: {newOwner?.Id}");

                // If owner hasn't changed, nothing to do
                if (oldOwner != null && newOwner != null && oldOwner.Id == newOwner.Id)
                {
                    localPluginContext.Trace("AccountSharingPlugin: Owner unchanged");
                    return;
                }
            }

            // Share with new owner (only if owner is a systemuser, not a team)
            if (newOwner != null && newOwner.LogicalName == "systemuser")
            {
                GrantAccountAccess(service, accountId, newOwner.Id, localPluginContext);
            }
            else if (newOwner != null)
            {
                localPluginContext.Trace($"AccountSharingPlugin: New owner is a {newOwner.LogicalName}, skipping share");
            }

            // Revoke access from old owner (only if it's a systemuser)
            if (oldOwner != null && oldOwner.LogicalName == "systemuser")
            {
                RevokeAccountAccess(service, accountId, oldOwner.Id, localPluginContext);
            }
        }

        /// <summary>
        /// Grants access to the account for the specified user with Read, Write, Append, AppendTo access
        /// </summary>
        private void GrantAccountAccess(IOrganizationService service, Guid accountId, Guid userId, ILocalPluginContext localPluginContext)
        {
            var grantAccessRequest = new GrantAccessRequest
            {
                Target = new EntityReference("account", accountId),
                PrincipalAccess = new PrincipalAccess
                {
                    Principal = new EntityReference("systemuser", userId),
                    AccessMask = AccessRights.ReadAccess |
                               AccessRights.WriteAccess |
                               AccessRights.AppendAccess |
                               AccessRights.AppendToAccess
                }
            };

            service.Execute(grantAccessRequest);
            localPluginContext.Trace($"AccountSharingPlugin: Successfully granted access to user {userId}");

        }

        /// <summary>
        /// Revokes access to the account for the specified user
        /// </summary>
        private void RevokeAccountAccess(IOrganizationService service, Guid accountId, Guid userId, ILocalPluginContext localPluginContext)
        {
            var revokeAccessRequest = new RevokeAccessRequest
            {
                Target = new EntityReference("account", accountId),
                Revokee = new EntityReference("systemuser", userId)
            };

            service.Execute(revokeAccessRequest);
            localPluginContext.Trace($"AccountSharingPlugin: Successfully revoked access from user {userId}");
        }
    }
}