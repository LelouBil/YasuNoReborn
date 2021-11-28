using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LCUSharp.Websocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NLog.Fluent;

namespace NoAudric
{
    public class No : IDisposable
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Semaphore _actionSemaphore = new(1,1);

        private readonly TaskCompletionSource _completionSource = new();

        private const string EventUri = "/lol-champ-select/v1/session";
        private const int Cancer = 157;

        public No()
        {
            Logger.Info("Creating champ select session event handler");
            Program.LeagueClientApi.EventHandler.Subscribe(EventUri, SelectEventHandler);
            Logger.Info("Event handler created");
        }


        private async void SelectEventHandler(object sender, LeagueEvent e)
        {
            var data = e.Data;
            var jArray = data["actions"]?.ToObject<JArray>();
            if (jArray == null || jArray.Count < 1) return;
            var actions = jArray[0].ToObject<JArray>();

            var localActions =
                actions?.Where(s => s["actorCellId"].ToString() == data["localPlayerCellId"]?.ToString());

            if (localActions == null) return;

            var isCancer = false;
            var actionId = 0;
            foreach (var localAction in localActions)
            {
                if (localAction["type"]?.ToString() == "pick" && localAction["championId"]?.ToObject<int>() == Cancer)
                {
                    isCancer = true;
                    var id = localAction["id"]?.ToObject<int>();
                    if (id != null) actionId = id.Value;
                }
            }

            if (!isCancer) return;
            
            _actionSemaphore.WaitOne();
            if (_completionSource.Task.IsCompleted)
            {
                _actionSemaphore.Release();
                return;
            }
            
            Logger.Info("Taking sem");

            Logger.Info("TRIED TO PICK YASUO!!!!!!!!!!!!!");
            Logger.Info("Saving ELO");

            try
            {
                var pickable = await GetPickableChampions();
                pickable.Remove(Cancer);

                var betterChamp = pickable[new Random().Next(pickable.Count)];
                Logger.Info($"replacing with : {betterChamp}");

                await HoverChamp(actionId, betterChamp);

                await LockChamp(actionId);
                Logger.Info("Elo saved");
                _completionSource.SetResult();
            }
            catch (HttpRequestException exception)
            {
                Logger.Error($"Failed to replace for action {actionId}");
                
            }
            Logger.Info("giving sem");
            _actionSemaphore.Release();
        }

        public Task GetReplaceTask()
        {
            return _completionSource.Task;
        }

        private static async Task LockChamp(int actionId)
        {
            await Program.LeagueClientApi.RequestHandler.GetJsonResponseAsync(HttpMethod.Post,
                $"lol-champ-select/v1/session/actions/{actionId}/complete");
        }

        private static async Task HoverChamp(int actionId, int replacingCancer)
        {
            var serialized = new JObject
            {
                ["championId"] = replacingCancer
            };
            await Program.LeagueClientApi.RequestHandler.GetJsonResponseAsync(HttpMethod.Patch,
                $"lol-champ-select/v1/session/actions/{actionId}",
                null,
                serialized);
        }

        private static async Task<List<int>> GetPickableChampions()
        {
            return await Program.LeagueClientApi.RequestHandler.GetResponseAsync<List<int>>(HttpMethod.Get,
                "lol-champ-select/v1/pickable-champion-ids");
        }


        public void Dispose()
        {
            Program.LeagueClientApi.EventHandler.Unsubscribe(EventUri, SelectEventHandler);
        }
    }
}