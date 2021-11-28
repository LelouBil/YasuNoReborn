using System;
using System.Threading;
using System.Threading.Tasks;
using LCUSharp;
using LCUSharp.Websocket;

namespace NoAudric
{
    public class GameStateManager : IDisposable
    {
        private const string GameStateUri = "/lol-gameflow/v1/gameflow-phase";
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private No _svp;

        private readonly Semaphore _svpSem = new(1,1);

        public GameStateManager()
        {
            Logger.Info("Registering GameState EventHandler");
            Program.LeagueClientApi.EventHandler.Subscribe(GameStateUri, OnGameFlowChanged);
        }
        

        private void OnGameFlowChanged(object sender, LeagueEvent e)
        {
            _svpSem.WaitOne();
            var state = e.Data.ToString();
            if (state == "ChampSelect")
            {
                if (_svp == null)
                {
                    Logger.Info("Starting No");
                    _svp = new No();
                    _svp.GetReplaceTask().ContinueWith(a =>
                    {
                        Logger.Info("Received task done");
                        DisposeSvp();
                        Logger.Info("Disposed early");
                    });
                }
            }
            else
            {
                if (_svp != null)
                {
                    Logger.Info("Stopping No");
                    _svp.Dispose();
                    _svp = null;
                }
            }

            _svpSem.Release();
        }

        public void Dispose()
        {
            Logger.Info("Disposing of GameStateManager");
            Program.LeagueClientApi.EventHandler.Unsubscribe(GameStateUri,OnGameFlowChanged);
            Logger.Info("Events disposed");
            DisposeSvp();
            Logger.Info("SVP disposed");

            _svpSem.Dispose();
        }

        private void DisposeSvp()
        {
            _svpSem.WaitOne();

            if (_svp != null)
            {
                _svp.Dispose();
                _svp = null;
            }
            _svpSem.Release();
        }
    }
}