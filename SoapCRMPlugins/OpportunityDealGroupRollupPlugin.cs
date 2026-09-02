using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SoapCRMPlugins
{
    /// <summary>
    /// Plugin that recalculates parent soap_dealgroup rollup fields when soap_opportunity records
    /// are created, updated, or deleted.
    /// Triggers: Post-operation Create, Update (soap_dealgroupid, soap_totalcontractvalue, soap_probability, soap_estclosedate), Delete of soap_opportunity.
    /// Updates soap_dealgroup:
    ///   soap_membercount        = COUNT(soap_opportunity)
    ///   soap_groupvalue         = SUM(soap_totalcontractvalue)
    ///   soap_weightedvalue      = SUM(soap_totalcontractvalue * soap_probability / 100)
    ///   soap_earliestclosedate  = MIN(soap_estclosedate)
    ///   soap_latestclosedate    = MAX(soap_estclosedate)
    /// </summary>
    public class OpportunityDealGroupRollupPlugin : PluginBase
    {
        public OpportunityDealGroupRollupPlugin() : base(typeof(OpportunityDealGroupRollupPlugin)) { }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
                throw new ArgumentNullException(nameof(localPluginContext));

            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.CurrentUserService;
            string message = context.MessageName.ToLower();

            if (message != "create" && message != "update" && message != "delete")
                return;

            // For update, only proceed if a relevant field is part of the change
            if (message == "update" &&
                context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity updateTarget)
            {
                if (!updateTarget.Contains("soap_dealgroupid") &&
                    !updateTarget.Contains("soap_totalcontractvalue") &&
                    !updateTarget.Contains("soap_probability") &&
                    !updateTarget.Contains("soap_estclosedate"))
                {
                    localPluginContext.Trace("OpportunityDealGroupRollupPlugin: No relevant fields changed, skipping.");
                    return;
                }
            }

            localPluginContext.Trace($"OpportunityDealGroupRollupPlugin: Processing {message} on soap_opportunity");

            // The deal group could have changed (opportunity moved between groups), so recalculate
            // both the previous and the new parent deal group when applicable.
            ResolveDealGroupIds(context, message, localPluginContext, out Guid dealGroupId, out Guid previousDealGroupId);

            if (dealGroupId != Guid.Empty)
            {
                localPluginContext.Trace($"OpportunityDealGroupRollupPlugin: Recalculating rollups for deal group {dealGroupId}");
                RecalculateAndUpdateDealGroupRollups(service, dealGroupId, localPluginContext);
            }
            else
            {
                localPluginContext.Trace("OpportunityDealGroupRollupPlugin: Could not resolve parent soap_dealgroupid, skipping.");
            }

            if (previousDealGroupId != Guid.Empty && previousDealGroupId != dealGroupId)
            {
                localPluginContext.Trace($"OpportunityDealGroupRollupPlugin: Recalculating rollups for previous deal group {previousDealGroupId}");
                RecalculateAndUpdateDealGroupRollups(service, previousDealGroupId, localPluginContext);
            }

        }

        /// <summary>
        /// Resolves the current parent soap_dealgroup ID and, when applicable, the previous
        /// soap_dealgroup ID the opportunity was associated with before the operation.
        /// For Delete, the target is an EntityReference so the PreImage is used for both.
        /// For Create/Update, the current value is read from the Target entity (falling back to
        /// PreImage when the field was not part of the change), and the previous value is read
        /// from the PreImage whenever it differs from the current value — this ensures both the
        /// old and new deal groups get their rollups recalculated when an opportunity is
        /// re-parented to a different deal group.
        /// </summary>
        private void ResolveDealGroupIds(IPluginExecutionContext context, string message, ILocalPluginContext localPluginContext, out Guid dealGroupId, out Guid previousDealGroupId)
        {
            dealGroupId = Guid.Empty;
            previousDealGroupId = Guid.Empty;

            Guid? preImageDealGroupId = null;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                var preImage = (Entity)context.PreEntityImages["PreImage"];
                if (preImage.Contains("soap_dealgroupid"))
                    preImageDealGroupId = preImage.GetAttributeValue<EntityReference>("soap_dealgroupid").Id;
            }

            if (message == "delete")
            {
                // Target is EntityReference on delete — read dealGroupId from PreImage
                if (preImageDealGroupId.HasValue)
                {
                    dealGroupId = preImageDealGroupId.Value;
                }
                else
                {
                    localPluginContext.Trace("OpportunityDealGroupRollupPlugin: PreImage missing or does not contain soap_dealgroupid.");
                }

                return;
            }

            // Create / Update — Target is Entity
            Guid? targetDealGroupId = null;
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)
            {
                if (target.Contains("soap_dealgroupid"))
                    targetDealGroupId = target.GetAttributeValue<EntityReference>("soap_dealgroupid").Id;
            }

            dealGroupId = targetDealGroupId ?? preImageDealGroupId ?? Guid.Empty;

            // Only relevant for Update: if the deal group lookup changed, the previous deal group
            // must also be recalculated since it lost a member.
            if (message == "update" && targetDealGroupId.HasValue && preImageDealGroupId.HasValue &&
                preImageDealGroupId.Value != targetDealGroupId.Value)
            {
                previousDealGroupId = preImageDealGroupId.Value;
            }
        }

        /// <summary>
        /// Queries all soap_opportunity records for the given deal group, computes the rollup
        /// values, then writes the results back to the parent soap_dealgroup record.
        /// Runs post-operation so that the triggering record's state is already committed.
        /// </summary>
        private void RecalculateAndUpdateDealGroupRollups(IOrganizationService service, Guid dealGroupId, ILocalPluginContext localPluginContext)
        {
            var query = new QueryExpression("soap_opportunity")
            {
                ColumnSet = new ColumnSet("soap_totalcontractvalue", "soap_probability", "soap_estclosedate"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("soap_dealgroupid", ConditionOperator.Equal, dealGroupId)
                    }
                }
            };

            EntityCollection opportunities = service.RetrieveMultiple(query);
            localPluginContext.Trace($"OpportunityDealGroupRollupPlugin: Found {opportunities.Entities.Count} opportunity(ies) for deal group {dealGroupId}");

            int memberCount = opportunities.Entities.Count;
            decimal groupValue = 0m;
            decimal weightedValue = 0m;
            DateTime? earliestCloseDate = null;
            DateTime? latestCloseDate = null;

            foreach (Entity opportunity in opportunities.Entities)
            {
                decimal contractValue = 0m;
                if (opportunity.Contains("soap_totalcontractvalue") && opportunity["soap_totalcontractvalue"] is Money contractValueMoney)
                    contractValue = contractValueMoney.Value;

                groupValue += contractValue;

                decimal probability = 0m;
                if (opportunity.Contains("soap_probability"))
                    probability = Convert.ToDecimal(opportunity["soap_probability"]);

                weightedValue += contractValue * (probability / 100m);

                if (opportunity.Contains("soap_estclosedate") && opportunity["soap_estclosedate"] is DateTime closeDate)
                {
                    if (earliestCloseDate == null || closeDate < earliestCloseDate)
                        earliestCloseDate = closeDate;

                    if (latestCloseDate == null || closeDate > latestCloseDate)
                        latestCloseDate = closeDate;
                }
            }

            localPluginContext.Trace($"OpportunityDealGroupRollupPlugin: soap_membercount={memberCount}, soap_groupvalue={groupValue}, soap_weightedvalue={weightedValue}, soap_earliestclosedate={earliestCloseDate}, soap_latestclosedate={latestCloseDate}");

            var dealGroupUpdate = new Entity("soap_dealgroup", dealGroupId)
            {
                ["soap_membercount"] = memberCount,
                ["soap_groupvalue"] = new Money(groupValue),
                ["soap_weightedvalue"] = new Money(weightedValue)
            };

            if (earliestCloseDate.HasValue)
                dealGroupUpdate["soap_earliestclosedate"] = earliestCloseDate.Value;

            if (latestCloseDate.HasValue)
                dealGroupUpdate["soap_latestclosedate"] = latestCloseDate.Value;

            service.Update(dealGroupUpdate);
            localPluginContext.Trace("OpportunityDealGroupRollupPlugin: Deal group rollups updated successfully.");
        }
    }
}
