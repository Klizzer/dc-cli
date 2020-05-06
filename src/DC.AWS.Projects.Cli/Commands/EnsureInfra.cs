using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using CommandLine;
using YamlDotNet.Serialization;

namespace DC.AWS.Projects.Cli.Commands
{
    public static class EnsureInfra
    {
        private static readonly IImmutableDictionary<string, Func<string, TemplateData, TemplateData.ResourceData, ProjectSettings, Task>> TypeHandlers =
            new Dictionary<string, Func<string, TemplateData, TemplateData.ResourceData, ProjectSettings, Task>>
            {
                ["AWS::DynamoDB::Table"] = HandleTable
            }.ToImmutableDictionary();
        
        public static async Task Execute(Options options)
        {
            var settings = await ProjectSettings.Read();
            
            var deserializer = new Deserializer();

            var templateData = deserializer.Deserialize<TemplateData>(
                await File.ReadAllTextAsync(
                    Path.Combine(settings.ProjectRoot, "infrastructure/environment/.generated/project.yml")));

            foreach (var resource in templateData.Resources)
            {
                if (TypeHandlers.ContainsKey(resource.Value.Type))
                    await TypeHandlers[resource.Value.Type](resource.Key, templateData, resource.Value, settings);
            }
        }

        private static async Task HandleTable(
            string name,
            TemplateData template, 
            TemplateData.ResourceData tableNode,
            ProjectSettings settings)
        {
            await EnsureLocalstackRunning.Execute(new EnsureLocalstackRunning.Options
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
        
        [Verb("ensure-infra", HelpText = "Ensure infrastructure is up to date.")]
        public class Options
        {
            
        }
    }
}