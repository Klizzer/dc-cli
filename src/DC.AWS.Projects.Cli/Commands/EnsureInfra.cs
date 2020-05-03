using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using CommandLine;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class EnsureInfra
    {
        private static readonly IImmutableDictionary<string, Action<string, TemplateData.ResourceData>> TypeHandlers =
            new Dictionary<string, Action<string, TemplateData.ResourceData>>
            {
                ["AWS::DynamoDB::Table"] = HandleTable
            }.ToImmutableDictionary();
        
        public static void Execute(Options options)
        {
            var settings = ProjectSettings.Read();
            
            var deserializer = new Deserializer();

            var templateData = deserializer.Deserialize<TemplateData>(
                File.ReadAllText(
                    Path.Combine(settings.ProjectRoot, "infrastructure/environment/.generated/project.yml")));

            foreach (var resource in templateData.Resources)
            {
                if (TypeHandlers.ContainsKey(resource.Value.Type))
                    TypeHandlers[resource.Value.Type](resource.Key, resource.Value);
            }
        }

        private static void HandleTable(string name, TemplateData.ResourceData tableNode)
        {
            EnsureLocalstackRunning.Execute(new EnsureLocalstackRunning.Options
            {
                RequiredServices = "dynamodb"
            });

            var client = new AmazonDynamoDBClient(new BasicAWSCredentials("key", "secret-key"), new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost:4569"
            });

            var billingMode = BillingMode.FindValue(tableNode.Properties["BillingMode"].ToString());

            var attributeDefinitions = ((IEnumerable<IDictionary<string, string>>)tableNode.Properties["AttributeDefinitions"])
                .Select(x => new AttributeDefinition(
                    x["AttributeName"],
                    ScalarAttributeType.FindValue(x["AttributeType"])))
                .ToList();

            if (client.TableExists(name).Result)
            {
                client.UpdateTableAsync(new UpdateTableRequest
                {
                    TableName = name,
                    AttributeDefinitions = attributeDefinitions,
                    BillingMode = billingMode
                }).Wait();
            }
            else
            {
                client.CreateTableAsync(new CreateTableRequest
                {
                    TableName = name,
                    KeySchema = ((IEnumerable<IDictionary<string, string>>)tableNode.Properties["KeySchema"])
                        .Select(x => new KeySchemaElement(
                            x["AttributeName"],
                            KeyType.FindValue(x["KeyType"])))
                        .ToList(),
                    AttributeDefinitions = attributeDefinitions,
                    BillingMode = billingMode
                }).Wait();
            }
        }
        
        [Verb("ensure-infra", HelpText = "Ensure infrastructure is up to date.")]
        public class Options
        {
            
        }
        
        private class TemplateData
        {
            public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
            public IDictionary<string, ResourceData> Resources { get; set; } = new Dictionary<string, ResourceData>();
            
            public class ResourceData
            {
                public string Type { get; set; }
                public IDictionary<string, object> Properties { get; set; }
            }
        }
    }
}