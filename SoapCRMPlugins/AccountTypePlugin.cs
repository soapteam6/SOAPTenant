using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace SoapCRMPlugins
{
    /// <summary>
    /// Sets soap_accounttype on Account based on statecode and soap_eanumber.
    ///
    /// Step 1 – Pre-operation (stage 20), synchronous:
    ///   - Create  of account
    ///   - Update  of account (filtered on statuscode, soap_eanumber)
    ///   Pre-image name "PreImage" (Update step only): statecode, soap_eanumber
    ///
    /// Step 2 – Post-operation (stage 40), synchronous:
    ///   - SetState of account
    ///   Pre-image name "PreImage": soap_eanumber
    ///
    /// Logic:
    ///   statecode = 1 (Inactive) AND soap_eanumber is not empty  → soap_accounttype = 2
    ///   statecode = 0 (Active)   AND soap_eanumber is not empty  → soap_accounttype = 1
    ///   else                                                      → soap_accounttype = 0
    /// </summary>
    public class AccountTypePlugin : PluginBase
    {
        private const int AccountTypeNone     = 0;
        private const int AccountTypeActive   = 1;
        private const int AccountTypeInactive = 2;

        public AccountTypePlugin() : base(typeof(AccountTypePlugin)) { }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
                throw new ArgumentNullException(nameof(localPluginContext));

            var context = localPluginContext.PluginExecutionContext;
            string message = context.MessageName.ToLower();

            if (context.PrimaryEntityName.ToLower() != "account")
                return;

            if (message == "setstate")
                HandleSetState(localPluginContext);
            else if (message == "create" || message == "update")
                HandleCreateUpdate(localPluginContext);
        }

        /// <summary>
        /// Create / Update — runs Pre-operation; sets soap_accounttype directly on the target.
        /// </summary>
        private void HandleCreateUpdate(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            string message = context.MessageName.ToLower();

            if (!context.InputParameters.Contains("Target") ||
                !(context.InputParameters["Target"] is Entity target))
            {
                localPluginContext.Trace("AccountTypePlugin: Target is missing or invalid, skipping.");
                return;
            }

            if (message == "update" &&
                !target.Contains("statuscode") &&
                !target.Contains("soap_eanumber"))
            {
                localPluginContext.Trace("AccountTypePlugin: No relevant field changed, skipping.");
                return;
            }

            Entity preImage = context.PreEntityImages.Contains("PreImage")
                ? context.PreEntityImages["PreImage"]
                : new Entity();

            OptionSetValue stateCodeValue = target.Contains("statecode")
                ? target.GetAttributeValue<OptionSetValue>("statecode")
                : preImage.GetAttributeValue<OptionSetValue>("statecode");

            string eaNumber = target.Contains("soap_eanumber")
                ? target.GetAttributeValue<string>("soap_eanumber")
                : preImage.GetAttributeValue<string>("soap_eanumber");

            int accountType = CalculateAccountType(stateCodeValue?.Value ?? 0, eaNumber);
            target["soap_accounttype"] = new OptionSetValue(accountType);

            localPluginContext.Trace($"AccountTypePlugin ({message}): statecode={stateCodeValue?.Value}, eaNumber={eaNumber} → soap_accounttype={accountType}");
        }

        /// <summary>
        /// SetState — runs Post-operation (statecode is already committed); issues a separate Update.
        /// Input parameters: EntityMoniker (EntityReference), State (OptionSetValue), Status (OptionSetValue).
        /// </summary>
        private void HandleSetState(ILocalPluginContext localPluginContext)
        {
            var context = localPluginContext.PluginExecutionContext;
            var service  = localPluginContext.CurrentUserService;

            if (!context.InputParameters.Contains("EntityMoniker") ||
                !(context.InputParameters["EntityMoniker"] is EntityReference entityMoniker))
            {
                localPluginContext.Trace("AccountTypePlugin: EntityMoniker missing, skipping.");
                return;
            }

            // New statecode is carried directly in the SetState input parameters
            OptionSetValue newState = context.InputParameters.Contains("State")
                ? context.InputParameters["State"] as OptionSetValue
                : null;

            // soap_eanumber is not part of SetState — use pre-image if registered, otherwise retrieve
            string eaNumber = null;
            if (context.PreEntityImages.Contains("PreImage"))
            {
                eaNumber = context.PreEntityImages["PreImage"].GetAttributeValue<string>("soap_eanumber");
            }
            else
            {
                Entity account = service.Retrieve("account", entityMoniker.Id, new ColumnSet("soap_eanumber"));
                eaNumber = account.GetAttributeValue<string>("soap_eanumber");
            }

            int accountType = CalculateAccountType(newState?.Value ?? 0, eaNumber);

            Entity update = new Entity("account", entityMoniker.Id);
            update["soap_accounttype"] = new OptionSetValue(accountType);
            service.Update(update);

            localPluginContext.Trace($"AccountTypePlugin (setstate): statecode={newState?.Value}, eaNumber={eaNumber} → soap_accounttype={accountType}");
        }

        private int CalculateAccountType(int stateCode, string eaNumber)
        {
            bool hasEaNumber = !string.IsNullOrWhiteSpace(eaNumber);

            if (stateCode == 1 && hasEaNumber)
                return AccountTypeInactive;
            if (stateCode == 0 && hasEaNumber)
                return AccountTypeActive;
            return AccountTypeNone;
        }
    }
}
