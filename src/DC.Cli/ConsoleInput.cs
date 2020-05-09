using System;

namespace DC.Cli
{
    public static class ConsoleInput
    {
        private static readonly object LockObject = new object();

        public static string Ask(string question)
        {
            lock (LockObject)
            {
                Console.WriteLine(question);
                return Console.ReadLine();
            }
        }
    }
}