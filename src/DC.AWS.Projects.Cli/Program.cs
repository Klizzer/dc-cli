using System.Threading.Tasks;
using CommandLine;
using DC.AWS.Projects.Cli.Commands;

namespace DC.AWS.Projects.Cli
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var parsedArguments = Parser.Default.ParseArguments<
                New.Options,
                Init.Options,
                Func.Options,
                Build.Options,
                Api.Options,
                ApiFunc.Options,
                Configure.Options,
                EnsureLocalstackRunning.Options,
                EnsureInfra.Options,
                Client.Options,
                Test.Options,
                AddProxy.Options,
                AddProxyPath.Options,
                AutoProxy.Options>(args);

            await parsedArguments.WithParsedAsync<New.Options>(New.Execute);
            await parsedArguments.WithParsedAsync<Init.Options>(Init.Execute);
            await parsedArguments.WithParsedAsync<Func.Options>(Func.Execute);
            await parsedArguments.WithParsedAsync<Build.Options>(Build.Execute);
            await parsedArguments.WithParsedAsync<Api.Options>(Api.Execute);
            await parsedArguments.WithParsedAsync<ApiFunc.Options>(ApiFunc.Execute);
            await parsedArguments.WithParsedAsync<Configure.Options>(Configure.Execute);
            await parsedArguments.WithParsedAsync<EnsureLocalstackRunning.Options>(EnsureLocalstackRunning.Execute);
            await parsedArguments.WithParsedAsync<EnsureInfra.Options>(EnsureInfra.Execute);
            await parsedArguments.WithParsedAsync<Client.Options>(Client.Execute);
            await parsedArguments.WithParsedAsync<Test.Options>(Test.Execute);
            await parsedArguments.WithParsedAsync<New.Options>(New.Execute);
            await parsedArguments.WithParsedAsync<AddProxy.Options>(AddProxy.Execute);
            await parsedArguments.WithParsedAsync<AddProxyPath.Options>(AddProxyPath.Execute);
            await parsedArguments.WithParsedAsync<AutoProxy.Options>(AutoProxy.Execute);
        }
    }
}