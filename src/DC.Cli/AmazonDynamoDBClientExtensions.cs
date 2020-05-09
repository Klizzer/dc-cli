using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace DC.Cli
{
    public static class AmazonDynamoDbClientExtensions
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
    }
}