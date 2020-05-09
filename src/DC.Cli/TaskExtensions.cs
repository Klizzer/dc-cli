using System.Collections.Generic;
using System.Threading.Tasks;

namespace DC.Cli
{
    public static class TaskExtensions
    {
        public static Task<TResult[]> WhenAll<TResult>(this IEnumerable<Task<TResult>> tasks)
        {
            return Task.WhenAll(tasks);
        }
    }
}