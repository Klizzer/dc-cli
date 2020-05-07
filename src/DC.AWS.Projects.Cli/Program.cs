using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Commands;

namespace DC.AWS.Projects.Cli
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            //TODO: Add "new" command
            var parsedArguments = Parser.Default.ParseArguments<
                Init.Options,
                Func.Options,
                Build.Options,
                Api.Options,
                ApiFunc.Options,
                Configure.Options,
                Client.Options,
                Test.Options,
                AddProxy.Options,
                AddProxyPath.Options,
                AutoProxy.Options,
                Restore.Options,
                CloudformationStack.Options,
                Start.Options,
                Stop.Options,
                Logs.Options>(args);

            await parsedArguments.WithParsedAsync<New.Options>(New.Execute);
            await parsedArguments.WithParsedAsync<Init.Options>(Init.Execute);
            await parsedArguments.WithParsedAsync<Func.Options>(Func.Execute);
            await parsedArguments.WithParsedAsync<Build.Options>(Build.Execute);
            await parsedArguments.WithParsedAsync<Api.Options>(Api.Execute);
            await parsedArguments.WithParsedAsync<ApiFunc.Options>(ApiFunc.Execute);
            await parsedArguments.WithParsedAsync<Configure.Options>(Configure.Execute);
            await parsedArguments.WithParsedAsync<Client.Options>(Client.Execute);
            await parsedArguments.WithParsedAsync<Test.Options>(Test.Execute);
            await parsedArguments.WithParsedAsync<AddProxy.Options>(AddProxy.Execute);
            await parsedArguments.WithParsedAsync<AddProxyPath.Options>(AddProxyPath.Execute);
            await parsedArguments.WithParsedAsync<AutoProxy.Options>(AutoProxy.Execute);
            await parsedArguments.WithParsedAsync<Restore.Options>(Restore.Execute);
            await parsedArguments.WithParsedAsync<CloudformationStack.Options>(CloudformationStack.Execute);
            await parsedArguments.WithParsedAsync<Start.Options>(Start.Execute);
            await parsedArguments.WithParsedAsync<Stop.Options>(Stop.Execute);
            await parsedArguments.WithParsedAsync<Logs.Options>(Logs.Execute);
        }
    }
}