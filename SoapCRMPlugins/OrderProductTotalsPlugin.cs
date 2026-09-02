using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SoapCRMPlugins
{
    /// <summary>
    /// Plugin that recalculates salesorder product totals when salesorder detail records are created, updated, or deleted.
    /// Triggers: Post-operation Create, Update (soap_amount, soap_cost), Delete of soap_salesorderdetail.
    /// Updates soap_salesorder:
    ///   soap_equipmenttotal = SUM(soap_amount)
    ///   soap_totalcost      = SUM(soap_cost)
    /// </summary>
    public class OrderProductTotalsPlugin : PluginBase
    {
        public OrderProductTotalsPlugin() : base(typeof(OrderProductTotalsPlugin)) { }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
                throw new ArgumentNullException(nameof(localPluginContext));

            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.CurrentUserService;
            string message = context.MessageName.ToLower();

            if (message != "create" && message != "update" && message != "delete")
                return;

            // For update, only proceed if soap_amount or soap_cost is part of the change
            if (message == "update" &&
                context.InputParameters.Contains("Target") &&
                context.InputParameters["Target"] is Entity updateTarget)
            {
                if (!updateTarget.Contains("soap_amount") && !updateTarget.Contains("soap_cost"))
                {
                    localPluginContext.Trace("OrderProductTotalsPlugin: Neither soap_amount nor soap_cost changed, skipping.");
                    return;
                }
            }

            localPluginContext.Trace($"OrderProductTotalsPlugin: Processing {message} on soap_salesorderdetail");

            Guid salesorderId = ResolvesalesorderId(context, message, localPluginContext);

            if (salesorderId == Guid.Empty)
            {
                localPluginContext.Trace("OrderProductTotalsPlugin: Could not resolve parent soap_salesorderid, skipping.");
                return;
            }

            localPluginContext.Trace($"OrderProductTotalsPlugin: Recalculating totals for salesorder {salesorderId}");

            try
            {
                RecalculateAndUpdatesalesorderTotals(service, salesorderId, localPluginContext);
            }
            catch (Exception ex)
            {
                localPluginContext.Trace($"OrderProductTotalsPlugin Error: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error in OrderProductTotalsPlugin: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resolves the parent soap_salesorder ID from the execution context.
        /// For Delete, the target is an EntityReference so the PreImage is used.
        /// For Create/Update, the lookup is read from the Target entity or PreImage fallback.
        /// </summary>
        private Guid ResolvesalesorderId(IPluginExecutionContext context, string message, ILocalPluginContext localPluginContext)
        {
            if (message == "delete")
            {
                // Target is EntityReference on delete — read salesorderId from PreImage
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = (Entity)context.PreEntityImages["PreImage"];
                    if (preImage.Contains("soap_salesorderid"))
                        return preImage.GetAttributeValue<EntityReference>("soap_salesorderid").Id;
                }

                localPluginContext.Trace("OrderProductTotalsPlugin: PreImage missing or does not contain soap_salesorderid.");
                return Guid.Empty;
            }

            // Create / Update — Target is Entity
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)
            {
                if (target.Contains("soap_salesorderid"))
                    return target.GetAttributeValue<EntityReference>("soap_salesorderid").Id;

                // soap_salesorderid may not be in the update payload; fall back to PreImage
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = (Entity)context.PreEntityImages["PreImage"];
                    if (preImage.Contains("soap_salesorderid"))
                        return preImage.GetAttributeValue<EntityReference>("soap_salesorderid").Id;
                }
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Queries all soap_salesorderdetail lines for the given salesorder, sums soap_amount and soap_cost,
        /// then writes the results back to the parent soap_salesorder record.
        /// Runs post-operation so that the triggering record's state is already committed.
        /// </summary>
        private void RecalculateAndUpdatesalesorderTotals(IOrganizationService service, Guid salesorderId, ILocalPluginContext localPluginContext)
        {
            var query = new QueryExpression("soap_salesorderdetail")
            {
                ColumnSet = new ColumnSet("soap_amount", "soap_cost"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("soap_salesorderid", ConditionOperator.Equal, salesorderId)
                    }
                }
            };

            EntityCollection details = service.RetrieveMultiple(query);
            localPluginContext.Trace($"OrderProductTotalsPlugin: Found {details.Entities.Count} detail line(s) for salesorder {salesorderId}");

            decimal equipmentTotal = 0m;
            decimal totalCost = 0m;

            foreach (Entity detail in details.Entities)
            {
                if (detail.Contains("soap_amount") && detail["soap_amount"] is Money amount)
                    equipmentTotal += amount.Value;

                if (detail.Contains("soap_cost") && detail["soap_cost"] is Money cost)
                    totalCost += cost.Value;
            }

            localPluginContext.Trace($"OrderProductTotalsPlugin: soap_equipmenttotal={equipmentTotal}, soap_totalcost={totalCost}");

            var salesorderUpdate = new Entity("soap_salesorder", salesorderId)
            {
                ["soap_equipmenttotal"] = new Money(equipmentTotal),
                ["soap_totalcost"] = new Money(totalCost)
            };

            service.Update(salesorderUpdate);
            localPluginContext.Trace("OrderProductTotalsPlugin: salesorder totals updated successfully.");
        }
    }
}