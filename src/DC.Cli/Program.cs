using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using DC.Cli.Commands;

namespace DC.Cli
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var command = args.Take(1);
            var commandArgs = args.Skip(1);
            
            var parsedArguments = Parser.Default.ParseArguments<
                InfraSubCommand.Options,
                ProjectSubCommand.Options>(command);

            await parsedArguments.WithParsedAsync<InfraSubCommand.Options>(_ => InfraSubCommand.Setup(commandArgs));
            await parsedArguments.WithParsedAsync<ProjectSubCommand.Options>(_ => ProjectSubCommand.Setup(commandArgs));
        }
        
        private static class InfraSubCommand
        {
            public static async Task Setup(IEnumerable<string> args)
            {
                var parsedArguments = Parser.Default.ParseArguments<
                    AddProxy.Options,
                    AddProxyPath.Options,
                    AutoProxy.Options,
                    CloudformationStack.Options,
                    CfChildOverrides.Options,
                    Package.Options>(args);
                
                await parsedArguments.WithParsedAsync<AddProxy.Options>(AddProxy.Execute);
                await parsedArguments.WithParsedAsync<AddProxyPath.Options>(AddProxyPath.Execute);
                await parsedArguments.WithParsedAsync<AutoProxy.Options>(AutoProxy.Execute);
                await parsedArguments.WithParsedAsync<CloudformationStack.Options>(CloudformationStack.Execute);
                await parsedArguments.WithParsedAsync<CfChildOverrides.Options>(CfChildOverrides.Execute);
                await parsedArguments.WithParsedAsync<Package.Options>(Package.Execute);
            }
            
            [Verb("infra")]
            public class Options
            {
                
            }
        }
        
        private static class ProjectSubCommand
        {
            public static async Task Setup(IEnumerable<string> args)
            {
                var parsedArguments = Parser.Default.ParseArguments<
                    New.Options,
                    Init.Options,
                    Func.Options,
                    Build.Options,
                    Api.Options,
                    ApiFunc.Options,
                    Configure.Options,
                    Client.Options,
                    Test.Options,
                    Restore.Options,
                    Clean.Options,
                    Start.Options,
                    Stop.Options,
                    Logs.Options,
                    CfWorker.Options>(args);
                
                await parsedArguments.WithParsedAsync<New.Options>(New.Execute);
                await parsedArguments.WithParsedAsync<Init.Options>(Init.Execute);
                await parsedArguments.WithParsedAsync<Func.Options>(Func.Execute);
                await parsedArguments.WithParsedAsync<Build.Options>(Build.Execute);
                await parsedArguments.WithParsedAsync<Api.Options>(Api.Execute);
                await parsedArguments.WithParsedAsync<ApiFunc.Options>(ApiFunc.Execute);
                await parsedArguments.WithParsedAsync<Configure.Options>(Configure.Execute);
                await parsedArguments.WithParsedAsync<Client.Options>(Client.Execute);
                await parsedArguments.WithParsedAsync<Test.Options>(Test.Execute);
                await parsedArguments.WithParsedAsync<Restore.Options>(Restore.Execute);
                await parsedArguments.WithParsedAsync<Clean.Options>(Clean.Execute);
                await parsedArguments.WithParsedAsync<Start.Options>(Start.Execute);
                await parsedArguments.WithParsedAsync<Stop.Options>(Stop.Execute);
                await parsedArguments.WithParsedAsync<Logs.Options>(Logs.Execute);
                await parsedArguments.WithParsedAsync<CfWorker.Options>(CfWorker.Execute);
            }
            
            [Verb("project")]
            public class Options
            {
                
            }
        }
    }
}