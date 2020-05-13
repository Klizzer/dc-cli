using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using ResourceNotFoundException = Amazon.DynamoDBv2.Model.ResourceNotFoundException;

namespace DC.Cli
{
    public static class AmazonSdkExtensions
    {
        public static async Task<bool> TableExists(this AmazonDynamoDBClient client, string name)
        {
            try
            {
                await client.DescribeTableAsync(name);

                return true;
            }
            catch (ResourceNotFoundException)
            {
                return false;
            }
        }

        public static async Task<(bool exists, string id)> CognitoUserPoolExists(
            this AmazonCognitoIdentityProviderClient client,
            string name)
        {
            var userPools = await client.ListUserPoolsAsync(new ListUserPoolsRequest());

            var matchingPool = userPools.UserPools.FirstOrDefault(x => x.Name == name);

            return (matchingPool != null, matchingPool?.Id);
        }

        public static async Task<(bool exists, string id)> CognitoUserPoolClientExists(
            this AmazonCognitoIdentityProviderClient client,
            string userPoolId,
            string name)
        {
            var userPoolClients = await client.ListUserPoolClientsAsync(new ListUserPoolClientsRequest
            {
                MaxResults = 10,
                UserPoolId = userPoolId
            });
            
            var matchingClient = userPoolClients.UserPoolClients.FirstOrDefault(x => x.ClientName == name);

            return (matchingClient != null, matchingClient?.ClientId);
        }
        
        public static async Task<bool> CognitoUserPoolDomainExists(
            this AmazonCognitoIdentityProviderClient client,
            string name)
        {
            try
            {
                var response = await client.DescribeUserPoolDomainAsync(new DescribeUserPoolDomainRequest
                {
                    Domain = name
                });

                return !string.IsNullOrEmpty(response.DomainDescription?.UserPoolId);
            }
            catch (Amazon.CognitoIdentityProvider.Model.ResourceNotFoundException)
            {
                return false;
            }
        }
    }
}