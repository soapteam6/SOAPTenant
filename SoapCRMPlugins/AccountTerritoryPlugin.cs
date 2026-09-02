using SoapCRMPlugins;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq.Expressions;
using System.Security.Principal;

namespace SoapCRMPlugins
{
    /// <summary>
    /// Plugin that automatically assigns territory to accounts based on the postal code
    /// </summary>
    public class AccountTerritoryPlugin : PluginBase
    {
        public AccountTerritoryPlugin() : base(typeof(AccountTerritoryPlugin)) { }

        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
                throw new ArgumentNullException(nameof(localPluginContext));

            var context = localPluginContext.PluginExecutionContext;
            var service = localPluginContext.CurrentUserService;

            // Only process create or update messages for account entity
            if (context.PrimaryEntityName.ToLower() != "account" ||
                (context.MessageName.ToLower() != "create" && context.MessageName.ToLower() != "update"))
            {
                return;
            }

            localPluginContext.Trace($"AccountTerritoryPlugin: Processing {context.MessageName} message");

            // Get the account being created/updated
            Entity accountEntity = null;
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity)
            {
                accountEntity = (Entity)context.InputParameters["Target"];
            }

            if (accountEntity == null)
            {
                localPluginContext.Trace("AccountTerritoryPlugin: Target entity is null");
                return;
            }

            Entity preImage = new Entity();

            if (context.PreEntityImages.Contains("PreImage"))
            {
                preImage = (Entity)context.PreEntityImages["PreImage"];

            }

            // For update operation, we need to merge with pre-image to get complete entity
            if (context.MessageName.ToLower() == "update" && context.PreEntityImages.Contains("PreImage"))
            {
                MergeEntityFields(accountEntity, preImage, "address1_postalcode", "soap_isnamed", "soap_territoryid");
            }


            // Get relevant values from the account entity
            EntityReference territoryPreRef = preImage.Contains("soap_territoryid") ?
                preImage.GetAttributeValue<EntityReference>("soap_territoryid") : null;

            EntityReference territoryRef = accountEntity.Contains("soap_territoryid") ?
                accountEntity.GetAttributeValue<EntityReference>("soap_territoryid") : null;

            string postalCode = accountEntity.Contains("address1_postalcode") ?
                NormalizePostalCode(accountEntity.GetAttributeValue<string>("address1_postalcode")) : null;

            bool accountBlockReassign = accountEntity.Contains("soap_isnamed") ?
                accountEntity.GetAttributeValue<bool>("soap_isnamed") : false;


            bool blockReassign = false;
            Entity territory = new Entity();


            // If territory is already assigned, retrieve the owner of the territory
            if (territoryRef != null)
            {
                territory = service.Retrieve(territoryRef.LogicalName, territoryRef.Id, new ColumnSet("ownerid"));
            }

            localPluginContext.Trace($"AccountTerritoryPlugin: Territory: {territoryRef?.Id}, PostalCode: {postalCode}");

            // Check if the territory is not null
            if (territoryPreRef != null)
            {
                // If territory is not null, check if soap_blockreassign is true on the territory entity
                Entity territoryPre = service.Retrieve(territoryPreRef.LogicalName, territoryPreRef.Id, new ColumnSet("soap_blockreassign"));

                if (territoryPre != null && territoryPre.Contains("soap_blockreassign") &&
                    territoryPre.GetAttributeValue<bool>("soap_blockreassign"))
                {
                    localPluginContext.Trace("AccountTerritoryPlugin: Territory is already assigned and reassignment is blocked");
                    blockReassign = true;
                }
            }

            if (!blockReassign && accountEntity.Contains("address1_postalcode"))
            {
                Entity newTerritory = new Entity();

                // If territory not found then get territory by postal code
                if (newTerritory == null || newTerritory.Id == Guid.Empty)
                {
                    newTerritory = GetTerritoryByPostalCode(service, postalCode, localPluginContext);
                }

                // If no territory found then use Out of territory
                if (newTerritory == null || newTerritory.Id == Guid.Empty)
                {
                    localPluginContext.Trace("AccountTerritoryPlugin: Search for out of territory");

                    // Try searching with first digit and zeros (e.g., 81234 -> 80000)
                    if (!string.IsNullOrWhiteSpace(postalCode) && postalCode.Length >= 1)
                    {
                        string fallbackPostalCode = postalCode[0] + "0000";
                        localPluginContext.Trace($"AccountTerritoryPlugin: Searching with fallback postal code {fallbackPostalCode}");
                        newTerritory = GetTerritoryByPostalCode(service, fallbackPostalCode, localPluginContext);
                    }

                    // If still not found, try with 00000
                    if ((newTerritory == null || newTerritory.Id == Guid.Empty))
                    {
                        localPluginContext.Trace("AccountTerritoryPlugin: Searching with default postal code 00000");
                        newTerritory = GetTerritoryByPostalCode(service, "00000", localPluginContext);
                    }
                }

                // If no territory found
                if (newTerritory == null || newTerritory.Id == Guid.Empty)
                {
                    localPluginContext.Trace("AccountTerritoryPlugin: Zipcode has null territory reference");
                    accountEntity["soap_territoryid"] = null;
                }
                else
                {
                    localPluginContext.Trace($"AccountTerritoryPlugin: Found territory {newTerritory.Id} for postal code {postalCode}");

                    // Update the account with the territory from the zipcode
                    accountEntity["soap_territoryid"] = newTerritory.ToEntityReference();
                    territory = newTerritory;
                }
            }

            if (accountBlockReassign)
            {
                localPluginContext.Trace("AccountTerritoryPlugin: Account has block reassign flag set. Exiting.");
                return;
            }

            // Query the territory entity to get the owner           

            if (territory != null && territory.Id != Guid.Empty && territory.Contains("ownerid"))
            {
                EntityReference systemUser = territory.GetAttributeValue<EntityReference>("ownerid");
                if (systemUser != null)
                {
                    localPluginContext.Trace($"AccountTerritoryPlugin: Setting owner to user {systemUser.Id}");
                    accountEntity["ownerid"] = systemUser;
                }
                else
                {
                    localPluginContext.Trace("AccountTerritoryPlugin: Territory has null system user");
                }
            }
            else
            {
                localPluginContext.Trace("AccountTerritoryPlugin: No system user found on territory");
            }


        }

        /// <summary>
        /// Helper method to merge attributes from pre-image into target entity if they don't exist
        /// </summary>
        private void MergeEntityFields(Entity target, Entity source, params string[] attributeNames)
        {
            if (target == null || source == null || attributeNames == null)
                return;

            foreach (string attributeName in attributeNames)
            {
                if (!target.Contains(attributeName) && source.Contains(attributeName))
                {
                    target[attributeName] = source[attributeName];
                }
            }
        }

        /// <summary>
        /// Retrieves a territory entity by postal code
        /// </summary>
        /// <param name="service">Organization service</param>
        /// <param name="postalCode">Postal code to search for</param>
        /// <param name="localPluginContext">Plugin context for tracing</param>
        /// <returns>Territory entity if found, otherwise null</returns>
        private Entity GetTerritoryByPostalCode(IOrganizationService service, string postalCode, ILocalPluginContext localPluginContext)
        {
            if (string.IsNullOrWhiteSpace(postalCode))
            {
                localPluginContext.Trace("AccountTerritoryPlugin: No postal code specified");
                return null;
            }

            // Query the ais_zipcode entity where ais_name equals the postal code
            var query = new QueryExpression("soap_territory")
            {
                TopCount = 1,
                // Add all columns to territory
                ColumnSet = new ColumnSet("ownerid", "soap_territoryid"),
                // Add filter to territory 
                LinkEntities =
                {
                    // Add link-entity query_ais_zipcode
                    new LinkEntity("soap_territory", "soap_zipcode", "soap_territoryid", "soap_territoryid", JoinOperator.Inner)
                    {
                        // Add filter to soap_zipcode
                        LinkCriteria =
                        {
                            Conditions =
                            {
                                new ConditionExpression("soap_name", ConditionOperator.Equal, postalCode)
                            }
                        }
                    }
                }
            };

            EntityCollection results = service.RetrieveMultiple(query);
            localPluginContext.Trace($"AccountTerritoryPlugin: Found {results.Entities.Count} territories for postal code {postalCode}");

            if (results.Entities.Count == 0)
            {
                return null;
            }

            // Return the first matching zipcode
            return results.Entities[0];
        }


        /// <summary>
        /// Normalizes postal code by removing spaces and taking only first 5 digits before dash
        /// </summary>
        /// <param name="postalCode">Raw postal code input</param>
        /// <returns>Normalized postal code (5 digits) or null if invalid</returns>
        private string NormalizePostalCode(string postalCode)
        {
            if (string.IsNullOrWhiteSpace(postalCode))
            {
                return null;
            }

            // Remove all spaces
            string normalized = postalCode.Replace(" ", string.Empty);

            // Take only part before dash if exists
            int dashIndex = normalized.IndexOf('-');
            if (dashIndex > 0)
            {
                normalized = normalized.Substring(0, dashIndex);
            }

            // Take only first 5 digits
            if (normalized.Length > 5)
            {
                normalized = normalized.Substring(0, 5);
            }

            return normalized;
        }
    }
}