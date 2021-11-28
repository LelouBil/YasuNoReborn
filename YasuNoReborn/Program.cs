using System;
using System.Threading;
using System.Threading.Tasks;
using LCUSharp;
using LCUSharp.Websocket;
using NLog;

namespace NoAudric
{
    class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static LeagueClientApi LeagueClientApi { get; private set; }

        private static readonly TaskCompletionSource ExitCompletionSource = new();

        private static async Task Main(string[] args)
        {
            ConfigLogging();
            CancellationTokenSource lookForClient = new CancellationTokenSource();
            Console.CancelKeyPress += (e, d) =>
            {
                Logger.Info("Received console interrupt");
                lookForClient.Cancel();
                d.Cancel = true;
                ExitCompletionSource.SetResult();
            };
            
            if (await MainLoop(lookForClient)) return;
            
            Logger.Info("Closing app");

        }

        private static async Task<bool> MainLoop(CancellationTokenSource lookForClient)
        {
            while (true)
            {
                Logger.Info("Trying to get Api");

                try
                {
                    LeagueClientApi = await LeagueClientApi.ConnectAsync(lookForClient.Token);
                }
                catch (TaskCanceledException e)
                {
                    break;
                }

                if (LeagueClientApi == null)
                {
                    Logger.Error("Failed to get API");
                    return true;
                }

                Logger.Info("Got API");


                LeagueClientApi.Disconnected += (e, d) =>
                {
                    Logger.Info("Client exited");
                    // lookForClient.Cancel();
                    ExitCompletionSource.SetResult();
                };

                using (var mgr = new GameStateManager())
                {
                    Logger.Info("Done!");
                    Logger.Info("Waiting for input to close");
                    await ExitCompletionSource.Task;
                    Logger.Info("Game exited");
                }
            }

            return false;
        }

        private static void ConfigLogging()
        {
            var config = new NLog.Config.LoggingConfiguration();
            
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);

            NLog.LogManager.Configuration = config;
        }
    }
}