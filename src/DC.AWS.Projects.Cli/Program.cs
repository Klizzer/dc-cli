using CommandLine;
using DC.AWS.Projects.Cli.Commands;

namespace DC.AWS.Projects.Cli
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<
                    New.Options,
                    Init.Options,
                    Func.Options,
                    Build.Options,
                    BuildFunction.Options,
                    Api.Options,
                    ApiFunc.Options,
                    Configure.Options,
                    EnsureLocalstackRunning.Options,
                    EnsureInfra.Options,
                    Client.Options,
                    TestFunction.Options,
                    Test.Options>(args)
                .WithParsed<New.Options>(New.Execute)
                .WithParsed<Init.Options>(Init.Execute)
                .WithParsed<Func.Options>(Func.Execute)
                .WithParsed<Build.Options>(Build.Execute)
                .WithParsed<BuildFunction.Options>(BuildFunction.Execute)
                .WithParsed<Api.Options>(Api.Execute)
                .WithParsed<ApiFunc.Options>(ApiFunc.Execute)
                .WithParsed<Configure.Options>(Configure.Execute)
                .WithParsed<EnsureLocalstackRunning.Options>(EnsureLocalstackRunning.Execute)
                .WithParsed<EnsureInfra.Options>(EnsureInfra.Execute)
                .WithParsed<Client.Options>(Client.Execute)
                .WithParsed<TestFunction.Options>(TestFunction.Execute)
                .WithParsed<Test.Options>(Test.Execute);
        }
    }
}
