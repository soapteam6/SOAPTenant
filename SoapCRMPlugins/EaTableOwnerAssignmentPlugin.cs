using Microsoft.Xrm.Sdk;
using System;

namespace SoapCRMPlugins
{
    /// <summary>
    /// Generic plugin that assigns the owner of a record on create, based on a related
    /// account or customer record. Entity-agnostic — register it against any "ea" table
    /// (soap_ea*) or any other table that carries an accountid/customer relationship.
    ///
    /// Registration (synchronous, Pre-operation, stage 20):
    ///   - Create of the target entity (register once per soap_ea* entity as needed).
    ///
    /// Pre-image: none required — all logic reads only from the create Target.
    ///
    /// Logic:
    ///   1. If the record has an accountid relationship, take the owner from that
    ///      account record (checked first).
    ///   2. Otherwise, resolve the customer id from soap_eacustomerid if present,
    ///      falling back to soap_customerid.
    ///   3. Retrieve the resolved account/customer record and read its owner.
    ///   4. Assign the record by setting target["ownerid"] directly (pre-operation,
    ///      no extra Update call required).
    /// </summary>
    public class EaTableOwnerAssignmentPlugin : PluginBase
    {
        private const string AccountFieldName = "soap_accountid";
        private const string EaCustomerFieldName = "soap_eacustomerid";
        private const string CustomerFieldName = "soap_customerid";
        private const string OwnerFieldName = "ownerid";

        public EaTableOwnerAssignmentPlugin() : base(typeof(EaTableOwnerAssignmentPlugin)) { }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
                throw new ArgumentNullException(nameof(localPluginContext));

            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.CurrentUserService;
            string message = context.MessageName.ToLower();

            if (message != "create")
                return;

            if (!context.InputParameters.Contains("Target") ||
                !(context.InputParameters["Target"] is Entity target))
            {
                localPluginContext.Trace("EaTableOwnerAssignmentPlugin: Target is missing or invalid, skipping.");
                return;
            }

            localPluginContext.Trace($"EaTableOwnerAssignmentPlugin: Processing create on {target.LogicalName}");

            EntityReference ownerId = ResolveOwner(service, target, localPluginContext);

            if (ownerId == null)
            {
                localPluginContext.Trace("EaTableOwnerAssignmentPlugin: Could not resolve an owner, skipping assignment.");
                return;
            }

            target[OwnerFieldName] = ownerId;
            localPluginContext.Trace($"EaTableOwnerAssignmentPlugin: Assigned owner {ownerId.Id} to record.");
        }

        /// <summary>
        /// Resolves the owner to assign: first from a related account (accountid),
        /// then from a related customer (soap_eacustomerid falling back to soap_customerid).
        /// </summary>
        private EntityReference ResolveOwner(IOrganizationService service, Entity target, ILocalPluginContext localPluginContext)
        {
            // Step 1: account relationship takes priority
            if (target.Contains(AccountFieldName))
            {
                var accountRef = target.GetAttributeValue<EntityReference>(AccountFieldName);
                if (accountRef != null)
                {
                    var accountOwner = GetOwnerFromRecord(service, accountRef, localPluginContext);
                    if (accountOwner != null)
                        return accountOwner;
                }
            } 

            else if (string.Equals(target.LogicalName, "soap_customer", StringComparison.OrdinalIgnoreCase))
            {
                localPluginContext.Trace($"EaTableOwnerAssignmentPlugin: Target is not an account, soap_eacustomer, or soap_customer. Skipping owner assignment.");
                return null;
            }
            // Step 2: resolve customer id, preferring soap_eacustomerid over soap_customerid
            EntityReference customerRef = null;
            if (target.Contains(EaCustomerFieldName))
            {
                customerRef = target.GetAttributeValue<EntityReference>(EaCustomerFieldName);
            }

            if (customerRef == null && target.Contains(CustomerFieldName))
            {
                customerRef = target.GetAttributeValue<EntityReference>(CustomerFieldName);
            }

            if (customerRef == null)
            {
                localPluginContext.Trace("EaTableOwnerAssignmentPlugin: No accountid, soap_eacustomerid, or soap_customerid found on target.");
                return null;
            }

            return GetOwnerFromRecord(service, customerRef, localPluginContext);
        }

        /// <summary>
        /// Retrieves the ownerid attribute from the specified record.
        /// </summary>
        private EntityReference GetOwnerFromRecord(IOrganizationService service, EntityReference recordRef, ILocalPluginContext localPluginContext)
        {
            var record = service.Retrieve(recordRef.LogicalName, recordRef.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet(OwnerFieldName));
            return record.GetAttributeValue<EntityReference>(OwnerFieldName);
        }
    }
}
