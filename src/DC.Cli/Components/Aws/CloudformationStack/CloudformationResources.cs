using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.S3;

namespace DC.Cli.Components.Aws.CloudformationStack
{
    public static class CloudformationResources
    {
        private static readonly IImmutableDictionary<
            string, 
            (int priority, Func<
                string,
                TemplateData,
                TemplateData.ResourceData,
                CloudformationStackComponent.CloudformationStackConfiguration,
                ProjectSettings,
                Task> handle)> TypeHandlers =
            new Dictionary<
                string,
                (int priority, Func<
                    string,
                    TemplateData,
                    TemplateData.ResourceData,
                    CloudformationStackComponent.CloudformationStackConfiguration, 
                    ProjectSettings,
                    Task> handle)>
            {
                ["AWS::S3::Bucket"] = (1, EnsureBucketExists),
                ["AWS::DynamoDB::Table"] = (1, EnsureTableExists),
                ["AWS::Cognito::UserPool"] = (2, EnsureCognitoUserPoolExists),
                // ["AWS::Cognito::UserPoolClient"] = EnsureConitoUserPoolClientExists,
                // ["AWS::Cognito::UserPoolDomain"] = EnsureCognitoUserPoolDomainExists
            }.ToImmutableDictionary();
        
        private static readonly IImmutableDictionary<
                string,
                Func<
                    TemplateData,
                    KeyValuePair<string, TemplateData.ResourceData>,
                    ProjectSettings,
                    Func<string, Task<(bool isRunning, int port)>>,
                    Task<string>>>
            GetRefs = new Dictionary<
                string,
                Func<
                    TemplateData,
                    KeyValuePair<string, TemplateData.ResourceData>, 
                    ProjectSettings,
                    Func<string, Task<(bool isRunning, int port)>>,
                    Task<string>>>
            {
                ["AWS::DynamoDB::Table"] = GetTableRef,
                ["AWS::Cognito::UserPool"] = GetUserPoolRef,
                ["AWS::S3::Bucket"] = GetBucketRef
            }.ToImmutableDictionary();
        
        private static readonly IImmutableDictionary<
                string,
                Func<
                    KeyValuePair<string, TemplateData.ResourceData>, 
                    string,
                    Func<string, Task<(bool isRunning, int port)>>,
                    Task<string>>>
            GetAttributes = new Dictionary<
                string,
                Func<
                    KeyValuePair<string, TemplateData.ResourceData>,
                    string,
                    Func<string, Task<(bool isRunning, int port)>>,
                    Task<string>>>
            {
                
            }.ToImmutableDictionary();

        public static async Task EnsureResourcesExist(
            TemplateData template,
            CloudformationStackComponent.CloudformationStackConfiguration configuration,
            ProjectSettings settings)
        {
            var availableResources = template
                .Resources
                .Where(x => TypeHandlers.ContainsKey(x.Value.Type))
                .Select(x => new
                {
                    resource = x,
                    handler = TypeHandlers[x.Value.Type]
                })
                .OrderBy(x => x.handler.priority)
                .ToImmutableList();
            
            foreach (var resource in availableResources)
            {
                await resource.handler.handle(
                    resource.resource.Key,
                    template,
                    resource.resource.Value,
                    configuration,
                    settings);
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
                                template,
                                new KeyValuePair<string, TemplateData.ResourceData>(refKey, template.Resources[refKey]), 
                                settings,
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
            TemplateData template,
            KeyValuePair<string, TemplateData.ResourceData> resource,
            ProjectSettings settings,
            Func<string, Task<(bool isRunning, int port)>> getServiceInformation)
        {
            return GetRefs.ContainsKey(resource.Value.Type) 
                ? GetRefs[resource.Value.Type](template, resource, settings, getServiceInformation)
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
            TemplateData template,
            KeyValuePair<string, TemplateData.ResourceData> resource,
            ProjectSettings settings,
            Func<string, Task<(bool isRunning, int port)>> getServiceInformation)
        {
            return Task.FromResult(GetTableName(resource));
        }
        
        private static async Task<string> GetUserPoolRef(
            TemplateData template,
            KeyValuePair<string, TemplateData.ResourceData> resource,
            ProjectSettings settings,
            Func<string, Task<(bool isRunning, int port)>> getServiceInformation)
        {
            var serviceInformation = await getServiceInformation("cognito-idp");

            if (!serviceInformation.isRunning)
                return null;
            
            var client = new AmazonCognitoIdentityProviderClient(
                new BasicAWSCredentials("key", "secret-key"),
                new AmazonCognitoIdentityProviderConfig
                {
                    ServiceURL = $"http://localhost:{serviceInformation.port}"
                });
            
            var userPoolName = (await ParseValue(
                resource.Value.Properties["UserPoolName"],
                template,
                settings,
                getServiceInformation)).ToString();

            var existsData = await client.CognitoUserPoolExists(userPoolName);

            return existsData.id;
        }

        private static Task<string> GetBucketRef(
            TemplateData template,
            KeyValuePair<string, TemplateData.ResourceData> resource,
            ProjectSettings settings,
            Func<string, Task<(bool isRunning, int port)>> getServiceInformation)
        {
            return Task.FromResult(GetBucketName(resource));
        }

        private static async Task EnsureBucketExists(
            string key,
            TemplateData template,
            TemplateData.ResourceData bucketNode,
            CloudformationStackComponent.CloudformationStackConfiguration configuration,
            ProjectSettings settings)
        {
            if (!configuration.GetConfiguredServices().Contains("s3"))
                return;
            
            var client = new AmazonS3Client(new BasicAWSCredentials("key", "secret-key"), new AmazonS3Config
            {
                ServiceURL = $"http://localhost:{configuration.Settings.ServicesPort}"
            });

            var name = GetBucketName(new KeyValuePair<string, TemplateData.ResourceData>(key, bucketNode));
            
            await client.PutBucketAsync(name);
        }
        
        private static async Task EnsureTableExists(
            string key,
            TemplateData template,
            TemplateData.ResourceData tableNode,
            CloudformationStackComponent.CloudformationStackConfiguration configuration,
            ProjectSettings settings)
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

        private static async Task EnsureCognitoUserPoolExists(
            string key,
            TemplateData template,
            TemplateData.ResourceData userPoolNode,
            CloudformationStackComponent.CloudformationStackConfiguration configuration,
            ProjectSettings settings)
        {
            var client = new AmazonCognitoIdentityProviderClient(
                new BasicAWSCredentials("key", "secret-key"),
                new AmazonCognitoIdentityProviderConfig
                {
                    ServiceURL = $"http://localhost:{configuration.Settings.ServicesPort}"
                });

            var userPoolName = (await ParseValue(
                userPoolNode.Properties["UserPoolName"],
                template,
                settings,
                x => Task.FromResult((true, configuration.Settings.ServicesPort)))).ToString();

            var existing = await client.CognitoUserPoolExists(userPoolName);

            var autoVerifiedAttributes = (List<string>) userPoolNode.Properties["AutoVerifiedAttributes"];

            var schema = ((List<Dictionary<string, string>>) userPoolNode.Properties["Schema"])
                .Select(x => new SchemaAttributeType
                {
                    Mutable = x["Mutable"] == "true",
                    Name = x["Name"],
                    Required = x["Required"] == "true",
                    AttributeDataType = AttributeDataType.FindValue(x["AttributeDataType"])
                })
                .ToList();
            
            if (existing.exists)
            {
                await client.UpdateUserPoolAsync(new UpdateUserPoolRequest
                {
                    UserPoolId = existing.id,
                    AutoVerifiedAttributes = autoVerifiedAttributes
                });
            }
            else
            {
                await client.CreateUserPoolAsync(new CreateUserPoolRequest
                {
                    PoolName = userPoolName,
                    AutoVerifiedAttributes = autoVerifiedAttributes,
                    Schema = schema
                });
            }
        }

        private static string GetTableName(KeyValuePair<string, TemplateData.ResourceData> tableNode)
        {
            //TODO: Check TableName property
            return tableNode.Key;
        }

        private static string GetBucketName(KeyValuePair<string, TemplateData.ResourceData> bucketNode)
        {
            //TODO: Check BucketName property
            return bucketNode.Key;
        }
    }
}