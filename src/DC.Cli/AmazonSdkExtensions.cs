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
    }
}