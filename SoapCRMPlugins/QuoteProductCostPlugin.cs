using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SoapCRMPlugins
{
    /// <summary>
    /// Calculates and stamps soap_cost on a soap_quotedetail line.
    ///
    /// Registration (synchronous, Pre-operation, stage 20):
    ///   - Create  of soap_quotedetail
    ///   - Update  of soap_quotedetail  (filtered on soap_productid, soap_amount, soap_quantity)
    ///
    /// Pre-image name "PreImage" (Update step only):
    ///   soap_quoteid, soap_productid, soap_amount, soap_quantity
    ///
    /// Logic:
    ///   1. Resolve soap_quoteid → retrieve parent soap_quote.soap_pricelistid.
    ///   2. Look up soap_pricelistitem by (soap_pricelistid, soap_productid).
    ///   3. Item found   → soap_cost = soap_amount × soap_quantity
    ///   4. Item missing → use soap_pricelist.soap_pricingmethod:
    ///        406670000 Markup → soap_cost = soap_amount / (1 + soap_percent / 100)
    ///        406670001 Margin → soap_cost = soap_amount × (1 − soap_percent / 100)
    ///   5. Set target["soap_cost"] directly (pre-operation — no extra Update call).
    /// </summary>
    public class QuoteProductCostPlugin : PluginBase
    {
        private const int PricingMethodMarkup = 406670000;
        private const int PricingMethodMargin = 406670001;

        public QuoteProductCostPlugin() : base(typeof(QuoteProductCostPlugin)) { }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
                throw new ArgumentNullException(nameof(localPluginContext));

            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.CurrentUserService;
            string message = context.MessageName.ToLower();

            if (!context.InputParameters.Contains("Target") ||
                !(context.InputParameters["Target"] is Entity target))
            {
                localPluginContext.Trace("QuoteProductCostPlugin: Target is missing or invalid, skipping.");
                return;
            }

            // For Update, only proceed when a pricing-relevant field is in the payload
            if (message == "update" &&
                !target.Contains("soap_productid") &&
                !target.Contains("soap_amount") &&
                !target.Contains("soap_quantity"))
            {
                localPluginContext.Trace("QuoteProductCostPlugin: No relevant field changed, skipping.");
                return;
            }

            // Pre-image supplies current values for fields absent from an Update target
            Entity preImage = context.PreEntityImages.Contains("PreImage")
                ? (Entity)context.PreEntityImages["PreImage"]
                : new Entity();

            localPluginContext.Trace($"QuoteProductCostPlugin: Processing {message} on soap_quotedetail.");

            try
            {
                // ── 1. Resolve all field values (target wins over preImage) ──────────
                EntityReference quoteRef = Resolve<EntityReference>(target, preImage, "soap_quoteid");
                EntityReference productRef = Resolve<EntityReference>(target, preImage, "soap_productid");
                Money amountMoney = Resolve<Money>(target, preImage, "soap_amount");
                decimal quantity = Resolve<decimal>(target, preImage, "soap_quantity");

                if (quoteRef == null)
                {
                    localPluginContext.Trace("QuoteProductCostPlugin: soap_quoteid not resolved, skipping.");
                    return;
                }

                decimal amount = amountMoney?.Value ?? 0m;

                // ── 2. Get soap_pricelistid from the parent soap_quote ────────────────
                EntityReference priceListRef = GetPriceListRef(service, quoteRef.Id, localPluginContext);
                if (priceListRef == null)
                {
                    localPluginContext.Trace("QuoteProductCostPlugin: soap_pricelistid not found on quote, skipping.");
                    return;
                }

                // ── 3. Look up soap_pricelistitem ─────────────────────────────────────
                Entity priceListItem = productRef != null
                    ? TryGetPriceListItem(service, priceListRef.Id, productRef.Id, localPluginContext)
                    : null;

                decimal cost;

                if (priceListItem != null)
                {
                    // Use soap_amount from the price list item as the unit cost price
                    Money itemAmountMoney = Resolve<Money>(priceListItem, "soap_amount");
                    if (itemAmountMoney == null)
                        throw new InvalidPluginExecutionException(
                            "soap_amount is not set on the price list item.");

                    cost = itemAmountMoney.Value * quantity;
                    localPluginContext.Trace(
                        $"QuoteProductCostPlugin: List item found → cost = {itemAmountMoney.Value} × {quantity} = {cost}");
                }
                else
                {
                    // ── 4. Derive cost from the pricelist formula ─────────────────────
                    cost = CalculateCostByFormula(service, priceListRef.Id, amount, localPluginContext);
                }

                // ── 5. Stamp soap_cost directly onto the Target (pre-operation) ───────
                target["soap_cost"] = new Money(cost);
                localPluginContext.Trace($"QuoteProductCostPlugin: soap_cost set to {cost}.");

            }
            catch (Exception ex)
            {
                localPluginContext.Trace($"QuoteProductCostPlugin Error: {ex.Message}");
                throw new InvalidPluginExecutionException($"Error in QuoteProductCostPlugin: {ex.Message}", ex);
            }
        }

        // ── Private helpers ──────────────────────────────────────────────────────────

        /// <summary>Retrieves soap_pricelistid from the parent soap_quote.</summary>
        private EntityReference GetPriceListRef(
            IOrganizationService service, Guid quoteId, ILocalPluginContext ctx)
        {
            Entity quote = service.Retrieve("soap_quote", quoteId, new ColumnSet("soap_pricelistid"));
            EntityReference priceListRef = quote.GetAttributeValue<EntityReference>("soap_pricelistid");
            ctx.Trace($"QuoteProductCostPlugin: soap_pricelistid = {priceListRef?.Id.ToString() ?? "null"}");
            return priceListRef;
        }

        /// <summary>
        /// Returns the matching soap_pricelistitem (including soap_amount) or null if not found.
        /// </summary>
        private Entity TryGetPriceListItem(
            IOrganizationService service, Guid priceListId, Guid productId, ILocalPluginContext ctx)
        {
            var query = new QueryExpression("soap_pricelistitem")
            {
                ColumnSet = new ColumnSet("soap_amount"),
                TopCount = 1,
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression("soap_pricelistid", ConditionOperator.Equal, priceListId),
                        new ConditionExpression("soap_productid",   ConditionOperator.Equal, productId)
                    }
                }
            };

            EntityCollection result = service.RetrieveMultiple(query);
            ctx.Trace($"QuoteProductCostPlugin: soap_pricelistitem lookup → {result.Entities.Count} row(s).");
            return result.Entities.Count > 0 ? result.Entities[0] : null;
        }

        /// <summary>
        /// Retrieves soap_pricelist and applies the configured markup or margin formula
        /// to derive cost from the selling <paramref name="amount"/>.
        /// </summary>
        private decimal CalculateCostByFormula(
            IOrganizationService service, Guid priceListId, decimal amount, ILocalPluginContext ctx)
        {
            Entity priceList = service.Retrieve(
                "soap_pricelist", priceListId,
                new ColumnSet("soap_pricingmethod", "soap_percent"));

            if (!priceList.Contains("soap_pricingmethod"))
                throw new InvalidPluginExecutionException(
                    "Pricing method is not set on the price list.");

            int method = priceList.GetAttributeValue<OptionSetValue>("soap_pricingmethod").Value;
            decimal percent = Resolve<decimal>(priceList, "soap_percent");

            ctx.Trace($"QuoteProductCostPlugin: Pricing method = {method}, percent = {percent}");

            if (method == PricingMethodMarkup)
            {
                decimal divisor = 1m + (percent / 100m);
                decimal cost = divisor == 0m ? 0m : amount / divisor;
                ctx.Trace($"QuoteProductCostPlugin: Markup → {amount} / (1 + {percent}/100) = {cost}");
                return cost;
            }

            if (method == PricingMethodMargin)
            {
                decimal cost = amount * (1m - (percent / 100m));
                ctx.Trace($"QuoteProductCostPlugin: Margin → {amount} × (1 - {percent}/100) = {cost}");
                return cost;
            }

            throw new InvalidPluginExecutionException(
                $"Unsupported soap_pricingmethod value: {method}.");
        }

        // ── Field-resolution utilities ───────────────────────────────────────────────

        /// <summary>
        /// Returns the attribute value from <paramref name="primary"/>, falling back to
        /// <paramref name="fallback"/>. Handles numeric coercions (e.g. int → decimal).
        /// Returns <c>default(T)</c> when the attribute is absent in both entities.
        /// </summary>
        private static T Resolve<T>(Entity primary, Entity fallback, string attr)
        {
            object raw = primary != null && primary.Contains(attr) ? primary[attr]
                       : fallback != null && fallback.Contains(attr) ? fallback[attr]
                       : null;

            if (raw == null) return default(T);
            if (raw is T direct) return direct;

            try { return (T)Convert.ChangeType(raw, typeof(T)); }
            catch { return default(T); }
        }

        /// <summary>Overload for reading from a single entity with no fallback.</summary>
        private static T Resolve<T>(Entity entity, string attr)
            => Resolve<T>(entity, null, attr);
    }
}