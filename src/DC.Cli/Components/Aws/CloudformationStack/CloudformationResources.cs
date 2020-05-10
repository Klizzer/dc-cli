using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;

namespace DC.Cli.Components.Aws.CloudformationStack
{
    public static class CloudformationResources
    {
        private static readonly IImmutableDictionary<
            string, 
            Func<string, TemplateData, TemplateData.ResourceData, CloudformationStackComponent.CloudformationStackConfiguration, Task>> TypeHandlers =
            new Dictionary<string, Func<string, TemplateData, TemplateData.ResourceData, CloudformationStackComponent.CloudformationStackConfiguration, Task>>
            {
                ["AWS::DynamoDB::Table"] = EnsureTableExists,
                // ["AWS::Cognito::UserPool"] = EnsureCognitoUserPoolExists,
                // ["AWS::Cognito::UserPoolClient"] = EnsureConitoUserPoolClientExists,
                // ["AWS::Cognito::UserPoolDomain"] = EnsureCognitoUserPoolDomainExists
            }.ToImmutableDictionary();
        
        private static readonly IImmutableDictionary<string, Func<KeyValuePair<string, TemplateData.ResourceData>, Func<string, Task<(bool isRunning, int port)>>, Task<string>>>
            GetRefs = new Dictionary<string, Func<KeyValuePair<string, TemplateData.ResourceData>, Func<string, Task<(bool isRunning, int port)>>, Task<string>>>
            {
                ["AWS::DynamoDB::Table"] = GetTableRef
            }.ToImmutableDictionary();
        
        private static readonly IImmutableDictionary<string, Func<KeyValuePair<string, TemplateData.ResourceData>, string, Func<string, Task<(bool isRunning, int port)>>, Task<string>>>
            GetAttributes = new Dictionary<string, Func<KeyValuePair<string, TemplateData.ResourceData>, string, Func<string, Task<(bool isRunning, int port)>>, Task<string>>>
            {
                
            }.ToImmutableDictionary();

        public static async Task EnsureResourcesExist(
            TemplateData template,
            CloudformationStackComponent.CloudformationStackConfiguration configuration)
        {
            foreach (var resource in template.Resources)
            {
                if (TypeHandlers.ContainsKey(resource.Value.Type))
                    await TypeHandlers[resource.Value.Type](resource.Key, template, resource.Value, configuration);
            }
        }

        public static async Task<object> ParseValue(
            object value,
            TemplateData template,
            ProjectSettings settings,
            Func<string, Task<(bool isRunning, int port)>> getServiceInformation)
        {
            switch (value)
            {
                case string variableValue:
                    return variableValue;
                case IDictionary<object, object> objectVariableValue:
                    if (objectVariableValue.ContainsKey("Ref"))
                    {
                        var refKey = objectVariableValue["Ref"].ToString() ?? "";

                        if (template.Parameters.ContainsKey(refKey))
                            return settings.GetConfiguration($"{SettingNamespaces.CloudformationParameters}{refKey}");

                        if (template.Resources.ContainsKey(refKey))
                        {
                            return await GetRef(
                                new KeyValuePair<string, TemplateData.ResourceData>(refKey, template.Resources[refKey]), 
                                getServiceInformation);
                        }
                    }
                    else if (objectVariableValue.ContainsKey("GetAtt"))
                    {
                        var getAttKey = objectVariableValue["GetAtt"].ToString() ?? "";

                        var resourceId = getAttKey.Split('.').First();

                        if (template.Resources.ContainsKey(resourceId))
                        {
                            return await GetAttribute(
                                new KeyValuePair<string, TemplateData.ResourceData>(resourceId, template.Resources[resourceId]), 
                                getAttKey.Substring(resourceId.Length + 1),
                                getServiceInformation);
                        }
                    }
                        
                    break;
            }

            return null;
        }

        private static Task<string> GetRef(
            KeyValuePair<string, TemplateData.ResourceData> resource,
            Func<string, Task<(bool isRunning, int port)>> getServiceInformation)
        {
            return GetRefs.ContainsKey(resource.Value.Type) 
                ? GetRefs[resource.Value.Type](resource, getServiceInformation)
                : Task.FromResult<string>(null);
        }

        private static Task<string> GetAttribute(
            KeyValuePair<string, TemplateData.ResourceData> resource,
            string attribute,
            Func<string, Task<(bool isRunning, int port)>> getServiceInformation)
        {
            return GetAttributes.ContainsKey(resource.Value.Type) 
                ? GetAttributes[resource.Value.Type](resource, attribute, getServiceInformation)
                : Task.FromResult<string>(null);
        }

        private static Task<string> GetTableRef(
            KeyValuePair<string, TemplateData.ResourceData> resource,
            Func<string, Task<(bool isRunning, int port)>> getServiceInformation)
        {
            return Task.FromResult(GetTableName(resource));
        }
        
        private static async Task EnsureTableExists(
            string key,
            TemplateData template,
            TemplateData.ResourceData tableNode,
            CloudformationStackComponent.CloudformationStackConfiguration configuration)
        {
            if (!configuration.GetConfiguredServices().Contains("dynamodb"))
                return;

            var client = new AmazonDynamoDBClient(new BasicAWSCredentials("key", "secret-key"), new AmazonDynamoDBConfig
            {
                ServiceURL = $"http://localhost:{configuration.Settings.ServicesPort}"
            });

            var billingMode = BillingMode.FindValue(tableNode.Properties["BillingMode"].ToString());

            var attributeDefinitions = ((IEnumerable<IDictionary<string, string>>)tableNode.Properties["AttributeDefinitions"])
                .Select(x => new AttributeDefinition(
                    x["AttributeName"],
                    ScalarAttributeType.FindValue(x["AttributeType"])))
                .ToList();

            var name = GetTableName(new KeyValuePair<string, TemplateData.ResourceData>(key, tableNode));
            
            if (await client.TableExists(name))
            {
                await client.UpdateTableAsync(new UpdateTableRequest
                {
                    TableName = name,
                    AttributeDefinitions = attributeDefinitions,
                    BillingMode = billingMode
                });
            }
            else
            {
                await client.CreateTableAsync(new CreateTableRequest
                {
                    TableName = name,
                    KeySchema = ((IEnumerable<IDictionary<string, string>>)tableNode.Properties["KeySchema"])
                        .Select(x => new KeySchemaElement(
                            x["AttributeName"],
                            KeyType.FindValue(x["KeyType"])))
                        .ToList(),
                    AttributeDefinitions = attributeDefinitions,
                    BillingMode = billingMode
                });
            }
        }

        private static string GetTableName(KeyValuePair<string, TemplateData.ResourceData> tableNode)
        {
            //TODO: Check TableName property
            return tableNode.Key;
        }
    }
}