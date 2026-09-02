using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SoapCRMPlugins
{
    /// <summary>
    /// Plugin that recalculates quote product totals when quote detail records are created, updated, or deleted.
    /// Triggers: Post-operation Create, Update (soap_amount, soap_cost), Delete of soap_quotedetail.
    /// Updates soap_quote:
    ///   soap_equipmenttotal = SUM(soap_amount)
    ///   soap_totalcost      = SUM(soap_cost)
    /// </summary>
    public class QuoteProductTotalsPlugin : PluginBase
    {
        public QuoteProductTotalsPlugin() : base(typeof(QuoteProductTotalsPlugin)) { }

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
                    localPluginContext.Trace("QuoteProductTotalsPlugin: Neither soap_amount nor soap_cost changed, skipping.");
                    return;
                }
            }

            localPluginContext.Trace($"QuoteProductTotalsPlugin: Processing {message} on soap_quotedetail");

            Guid quoteId = ResolveQuoteId(context, message, localPluginContext);

            if (quoteId == Guid.Empty)
            {
                localPluginContext.Trace("QuoteProductTotalsPlugin: Could not resolve parent soap_quoteid, skipping.");
                return;
            }

            localPluginContext.Trace($"QuoteProductTotalsPlugin: Recalculating totals for quote {quoteId}");

            try
            {
                RecalculateAndUpdateQuoteTotals(service, quoteId, localPluginContext);
            }
            catch (Exception ex)
            {
                localPluginContext.Trace($"QuoteProductTotalsPlugin Error: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error in QuoteProductTotalsPlugin: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Resolves the parent soap_quote ID from the execution context.
        /// For Delete, the target is an EntityReference so the PreImage is used.
        /// For Create/Update, the lookup is read from the Target entity or PreImage fallback.
        /// </summary>
        private Guid ResolveQuoteId(IPluginExecutionContext context, string message, ILocalPluginContext localPluginContext)
        {
            if (message == "delete")
            {
                // Target is EntityReference on delete — read quoteId from PreImage
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = (Entity)context.PreEntityImages["PreImage"];
                    if (preImage.Contains("soap_quoteid"))
                        return preImage.GetAttributeValue<EntityReference>("soap_quoteid").Id;
                }

                localPluginContext.Trace("QuoteProductTotalsPlugin: PreImage missing or does not contain soap_quoteid.");
                return Guid.Empty;
            }

            // Create / Update — Target is Entity
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)
            {
                if (target.Contains("soap_quoteid"))
                    return target.GetAttributeValue<EntityReference>("soap_quoteid").Id;

                // soap_quoteid may not be in the update payload; fall back to PreImage
                if (context.PreEntityImages.Contains("PreImage"))
                {
                    var preImage = (Entity)context.PreEntityImages["PreImage"];
                    if (preImage.Contains("soap_quoteid"))
                        return preImage.GetAttributeValue<EntityReference>("soap_quoteid").Id;
                }
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Queries all soap_quotedetail lines for the given quote, sums soap_amount and soap_cost,
        /// then writes the results back to the parent soap_quote record.
        /// Runs post-operation so that the triggering record's state is already committed.
        /// </summary>
        private void RecalculateAndUpdateQuoteTotals(IOrganizationService service, Guid quoteId, ILocalPluginContext localPluginContext)
        {
            var query = new QueryExpression("soap_quotedetail")
            {
                ColumnSet = new ColumnSet("soap_amount", "soap_cost"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("soap_quoteid", ConditionOperator.Equal, quoteId)
                    }
                }
            };

            EntityCollection details = service.RetrieveMultiple(query);
            localPluginContext.Trace($"QuoteProductTotalsPlugin: Found {details.Entities.Count} detail line(s) for quote {quoteId}");

            decimal equipmentTotal = 0m;
            decimal totalCost = 0m;

            foreach (Entity detail in details.Entities)
            {
                if (detail.Contains("soap_amount") && detail["soap_amount"] is Money amount)
                    equipmentTotal += amount.Value;

                if (detail.Contains("soap_cost") && detail["soap_cost"] is Money cost)
                    totalCost += cost.Value;
            }

            localPluginContext.Trace($"QuoteProductTotalsPlugin: soap_equipmenttotal={equipmentTotal}, soap_totalcost={totalCost}");

            var quoteUpdate = new Entity("soap_quote", quoteId)
            {
                ["soap_equipmenttotal"] = new Money(equipmentTotal),
                ["soap_totalcost"] = new Money(totalCost)
            };

            service.Update(quoteUpdate);
            localPluginContext.Trace("QuoteProductTotalsPlugin: Quote totals updated successfully.");
        }
    }
}