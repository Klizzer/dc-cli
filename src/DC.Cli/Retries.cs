using System;
using System.Threading.Tasks;

namespace DC.Cli
{
    public static class Retries
    {
        public static Task<TResponse> RetryOnException<TResponse>(
            Func<Task<TResponse>> operation,
            string actionName,
            int times = 5)
        {
            return RetryOnException<Exception, TResponse>(operation, actionName, times);
        }

        public static async Task<TResponse> RetryOnException<TException, TResponse>(
            Func<Task<TResponse>> operation,
            string actionName,
            int times = 5)
            where TException : Exception
        {
            if (times <= 0)
                throw new ArgumentOutOfRangeException(nameof(times));

            var attempts = 0;
            do
            {
                try
                {
                    attempts++;
                    
                    return await operation();
                }
                catch (TException)
                {
                    if (attempts >= times)
                    {
                        Console.WriteLine($"Action \"{actionName}\" has failed maximum number of times ({times}). Throwing exception...");
                        
                        throw;
                    }
                    
                    Console.WriteLine($"Action \"{actionName}\" has failed {attempts} times. Retrying...");
                }
            } while (true);
        }
    }
}