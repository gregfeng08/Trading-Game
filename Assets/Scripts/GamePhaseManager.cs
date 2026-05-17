using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Game.API;
using Game.API.DTO;

public enum GamePhase { PreMarket, Day, PostMarket }

[System.Serializable]
public class PendingOrder
{
    public string ticker;
    public string side;
    public int quantity;
}

[System.Serializable]
public class TradeResult
{
    public string ticker;
    public string side;
    public int quantity;
    public float fillPrice;
    public float closePrice;
    public float pnl;
    public string status;
    public string message;
}

public class GamePhaseManager : MonoBehaviour
{
    public static GamePhaseManager Inst { get; private set; }

    [Header("Day Timer")]
    [SerializeField] private float dayDurationSeconds = 360f;

    public GamePhase CurrentPhase { get; private set; } = GamePhase.PreMarket;
    public string CurrentDate { get; private set; }
    public float DayTimeRemaining { get; private set; }
    public float DayDuration => dayDurationSeconds;
    public bool DayTimerActive { get; private set; }
    public bool PostMarketReady { get; private set; }
    public bool IsTransitioning { get; private set; }

    public event Action<GamePhase> OnPhaseChanged;
    public event Action OnDayTimerExpired;

    private readonly List<PendingOrder> pendingOrders = new List<PendingOrder>();
    private readonly List<TradeResult> todayResults = new List<TradeResult>();

    public IReadOnlyList<PendingOrder> PendingOrders => pendingOrders;
    public IReadOnlyList<TradeResult> TodayResults => todayResults;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!DayTimerActive) return;

        DayTimeRemaining -= Time.deltaTime;
        if (DayTimeRemaining <= 0f)
        {
            DayTimeRemaining = 0f;
            DayTimerActive = false;
            OnDayTimerExpired?.Invoke();
            HandleDayExpired();
        }
    }

    public async Task SyncWithServer()
    {
        var resp = await GameStateAPI.GetGameDate();
        CurrentDate = resp.current_date;
        CurrentPhase = ParsePhase(resp.game_phase);
    }

    // ── Order Queue ──

    public void QueueOrder(string ticker, string side, int quantity)
    {
        pendingOrders.Add(new PendingOrder { ticker = ticker, side = side, quantity = quantity });
    }

    public void RemoveOrder(int index)
    {
        if (index >= 0 && index < pendingOrders.Count)
            pendingOrders.RemoveAt(index);
    }

    public void ClearOrders() => pendingOrders.Clear();

    // ── Phase Transitions ──

    public async Task<List<TradeResult>> OpenMarkets()
    {
        if (CurrentPhase != GamePhase.PreMarket || IsTransitioning) return null;
        IsTransitioning = true;

        try
        {
            await GameStateAPI.AdvancePhase();

            todayResults.Clear();
            foreach (var order in pendingOrders)
            {
                try
                {
                    var req = new TradeRequestDTO
                    {
                        entity_id = APIBootstrapper.EntityExternalId,
                        ticker = order.ticker,
                        side = order.side,
                        quantity = order.quantity,
                        order_type = "market"
                    };
                    var resp = await TradeAPI.PostTrade(req);
                    todayResults.Add(new TradeResult
                    {
                        ticker = order.ticker,
                        side = order.side,
                        quantity = order.quantity,
                        fillPrice = resp.filled_price,
                        status = resp.status,
                        message = resp.message
                    });
                }
                catch (Exception ex)
                {
                    todayResults.Add(new TradeResult
                    {
                        ticker = order.ticker,
                        side = order.side,
                        quantity = order.quantity,
                        status = "error",
                        message = ex.Message
                    });
                }
            }
            pendingOrders.Clear();

            CurrentPhase = GamePhase.Day;
            DayTimeRemaining = dayDurationSeconds;
            DayTimerActive = true;
            PostMarketReady = false;

            OnPhaseChanged?.Invoke(CurrentPhase);
            return new List<TradeResult>(todayResults);
        }
        finally
        {
            IsTransitioning = false;
        }
    }

    public async Task CloseMarkets()
    {
        if (CurrentPhase != GamePhase.Day || IsTransitioning) return;
        IsTransitioning = true;

        try
        {
            DayTimerActive = false;

            await GameStateAPI.AdvancePhase();

            foreach (var result in todayResults)
            {
                if (result.status != "ok") continue;
                try
                {
                    var prices = await MarketAPI.GetPrices(result.ticker, CurrentDate, CurrentDate);
                    if (prices.rows != null && prices.rows.Length > 0)
                    {
                        result.closePrice = prices.rows[0].close_price;
                        result.pnl = result.side == "buy"
                            ? (result.closePrice - result.fillPrice) * result.quantity
                            : (result.fillPrice - result.closePrice) * result.quantity;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GamePhaseManager] Failed to fetch close price for {result.ticker}: {ex.Message}");
                }
            }

            CurrentPhase = GamePhase.PostMarket;
            PostMarketReady = true;
            OnPhaseChanged?.Invoke(CurrentPhase);
        }
        finally
        {
            IsTransitioning = false;
        }
    }

    public async Task<bool> AdvanceToNextDay()
    {
        if (CurrentPhase != GamePhase.PostMarket || IsTransitioning) return false;
        IsTransitioning = true;

        try
        {
            var resp = await GameStateAPI.AdvanceDay();
            if (resp.game_over) return false;

            CurrentDate = resp.current_date;
            todayResults.Clear();
            pendingOrders.Clear();
            PostMarketReady = false;

            CurrentPhase = GamePhase.PreMarket;
            OnPhaseChanged?.Invoke(CurrentPhase);
            return true;
        }
        finally
        {
            IsTransitioning = false;
        }
    }

    public void EndDayEarly()
    {
        if (CurrentPhase != GamePhase.Day) return;
        DayTimerActive = false;
        _ = CloseMarketsAndGoHome();
    }

    // ── Internals ──

    private async Task CloseMarketsAndGoHome()
    {
        await CloseMarkets();
        if (SceneTransitionManager.Inst != null)
            SceneTransitionManager.Inst.LoadScene("Room", "bed");
    }

    private void HandleDayExpired()
    {
        _ = CloseMarketsAndGoHome();
    }

    private static GamePhase ParsePhase(string phase)
    {
        return phase switch
        {
            "day" => GamePhase.Day,
            "post_market" => GamePhase.PostMarket,
            _ => GamePhase.PreMarket
        };
    }
}
