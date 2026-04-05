using System;
using System.Threading.Tasks;
using UnityEngine;
using Game.API.DTO;

namespace Game.API
{
    /// <summary>
    /// Attach to a GameObject in your first scene.
    /// Runs once, brings up local Flask + DB, then signals ready.
    /// </summary>
    public class APIBootstrapper : MonoBehaviour
    {
        public enum BootState { NotStarted, Bootstrapping, Ready, Failed }

        public static BootState State { get; private set; } = BootState.NotStarted;
        public static string LastError { get; private set; } = "";

        /// <summary>Internal DB id assigned during entity registration.</summary>
        public static int EntityDbId { get; set; } = -1;

        /// <summary>External entity id (e.g. "player_001") from BootstrapConfig.</summary>
        public static string EntityExternalId { get; set; } = "";

        public static event Action OnReady;
        public static event Action<string> OnFailed;

        [SerializeField] private BootstrapConfig config;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool verboseLogs = true;

        private void Awake()
        {
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
        }

        public void TryStart()
        {
            if (State == BootState.Bootstrapping || State == BootState.Ready) return;

            if (config == null)
            {
                LastError = "BootstrapConfig is not assigned.";
                State = BootState.Failed;
                OnFailed?.Invoke(LastError);
                return;
            }

            // Apply BaseUrl early (you removed this in the new version)
            APIClient.BaseUrl = (config.baseUrl ?? "").Trim().TrimEnd('/');

            State = BootState.Bootstrapping;
            _ = TryStartAsync(); // fire-and-forget safely
        }

        private async Task TryStartAsync()
        {
            try
            {
                await RunBootstrapAsync();
                State = BootState.Ready;
                OnReady?.Invoke();
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
                State = BootState.Failed;
                OnFailed?.Invoke(LastError);
            }
        }

        public static void ResetForRetry()
        {
            // Only allow resetting from Failed
            if (State == BootState.Failed)
            {
                LastError = "";
                State = BootState.NotStarted;
            }
        }

        private async Task RunBootstrapAsync()
        {
            Log($"Pinging API at {APIClient.BaseUrl} ...");
            var ping = await HealthAPI.PingAsync();
            Log($"Ping OK. status={ping.status} server_time={ping.server_time}");

            Log("Fetching status ...");
            var status = await HealthAPI.StatusAsync();
            Log($"Status OK. db_connected={status.db_connected} version={status.version}");

            if (config.initDb)
            {
                Log("Initializing DB ...");
                var init = await DbAPI.InitializeDB();
                Log($"Init DB: {init.status}");
            }

            if (config.loadTickers)
            {
                Log("Loading tickers ...");
                var load = await DbAPI.LoadTickers();
                Log($"Load tickers: {load.status}");
            }

            if (config.newGame)
            {
                Log("Creating New Game");
                var createGameResp = await GameStateAPI.NewGame("2020-01-01");
            }

            if (config.registerEntity)
            {
                Log("Registering entity ...");
                var entity = new EntityDTO
                {
                    entity_id = config.entity_id,
                    entity_type = config.entity_type,
                    display_name = config.display_name
                };

                var reg = await DbAPI.RegisterEntity(entity);
                EntityDbId = reg.entity_db_id;
                EntityExternalId = config.entity_id;
                Log($"Register entity: {reg.status} entity_db_id={reg.entity_db_id}");
            }
        }

        private void Log(string msg)
        {
            if (verboseLogs)
                Debug.Log($"[ApiBootstrapper] {msg}");
        }

        private void Fail(string msg)
        {
            Debug.LogError($"[ApiBootstrapper] {msg}");
            OnFailed?.Invoke(msg);
        }
    }
}
