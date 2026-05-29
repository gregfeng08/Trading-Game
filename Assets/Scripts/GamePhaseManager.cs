using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Game.API;
using Game.API.DTO;

public enum GamePhase { PreMarket, Day, PostMarket }

[System.Serializable]
public class TradeResult
{
    public string ticker;
    public string side;
    public int quantity;
    public double fillPrice;
    public double closePrice;
    public double pnl;
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

    // Arc (season) state — populated after each day advance
    public string ArcName { get; private set; }
    public int ArcDaysRemaining { get; private set; } = -1;
    public double ArcReturnPct { get; private set; }
    public string ArcProjectedGrade { get; private set; }
    public ArcTransitionDTO LastArcTransition { get; private set; }
    public ArcDefinitionDTO CurrentArcDefinition { get; private set; }
    public bool PendingArcIntro { get; set; }

    // Forced liquidations from last day advance
    public ForcedLiquidationDTO[] LastForcedLiquidations { get; private set; }

    public event Action<GamePhase> OnPhaseChanged;
    public event Action OnDayTimerExpired;
    public event Action<ArcTransitionDTO> OnArcTransition;
    public event Action<ForcedLiquidationDTO[]> OnForcedLiquidations;

    private readonly List<TradeResult> todayResults = new List<TradeResult>();

    public IReadOnlyList<TradeResult> TodayResults => todayResults;

    void Awake()
    {
        if (Inst != null && Inst != this) { Destroy(gameObject); return; }
        Inst = this;
        DontDestroyOnLoad(gameObject);

        if (ArcTransitionOverlay.Inst == null)
        {
            var go = new GameObject("ArcTransitionOverlay");
            go.AddComponent<ArcTransitionOverlay>();
        }
        if (DailySummaryOverlay.Inst == null)
        {
            var go = new GameObject("DailySummaryOverlay");
            go.AddComponent<DailySummaryOverlay>();
        }
        if (DialoguePlayer.Inst == null)
        {
            var go = new GameObject("DialoguePlayer");
            go.AddComponent<DialoguePlayer>();
        }
        if (OnboardingController.Inst == null)
        {
            var go = new GameObject("OnboardingController");
            go.AddComponent<OnboardingController>();
        }
        if (NewspaperUI.Inst == null)
        {
            var go = new GameObject("NewspaperUI");
            go.AddComponent<NewspaperUI>();
        }
        if (NewspaperHUDButton.Inst == null)
        {
            var go = new GameObject("NewspaperHUDButton");
            go.AddComponent<NewspaperHUDButton>();
        }
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
        await RefreshArcStatus();
    }

    // ── Local Order Queue ──

    [System.Serializable]
    public class LocalPendingOrder
    {
        public string ticker;
        public string side;
        public int quantity;
        public double estimatedPrice;
    }

    private readonly List<LocalPendingOrder> localOrders = new List<LocalPendingOrder>();
    public IReadOnlyList<LocalPendingOrder> PendingOrders => localOrders;

    public string LastOrderError { get; private set; }

    private double serverCash;
    public void SetServerCash(double cash) => serverCash = cash;

    public double ReservedBuyCost
    {
        get
        {
            double total = 0;
            foreach (var o in localOrders)
                if (o.side == "buy") total += o.estimatedPrice * o.quantity;
            return total;
        }
    }

    public double EffectiveAvailableCash => serverCash - ReservedBuyCost;

    public int ReservedSellQuantity(string ticker)
    {
        int total = 0;
        foreach (var o in localOrders)
            if (o.side == "sell" && o.ticker == ticker) total += o.quantity;
        return total;
    }

    public bool QueueOrder(string ticker, string side, int quantity, double estimatedPrice, double heldShares)
    {
        LastOrderError = null;

        if (side == "buy")
        {
            double cost = estimatedPrice * quantity;
            double available = EffectiveAvailableCash;
            if (available < cost - 1e-9)
            {
                LastOrderError = $"Insufficient cash. Need ${cost:F2}, available ${available:F2}";
                return false;
            }
        }
        else
        {
            int reserved = ReservedSellQuantity(ticker);
            if (heldShares - reserved < quantity - 1e-9)
            {
                LastOrderError = $"Insufficient shares. Available: {heldShares - reserved:F0}";
                return false;
            }
        }

        localOrders.Add(new LocalPendingOrder
        {
            ticker = ticker,
            side = side,
            quantity = quantity,
            estimatedPrice = estimatedPrice
        });
        return true;
    }

    public bool RemoveOrder(int index)
    {
        if (index < 0 || index >= localOrders.Count) return false;
        localOrders.RemoveAt(index);
        return true;
    }

    public void ClearLocalOrders() => localOrders.Clear();

    // ── Phase Transitions (ACID via server) ──

    public async Task<List<TradeResult>> OpenMarkets()
    {
        if (CurrentPhase != GamePhase.PreMarket || IsTransitioning) return null;
        IsTransitioning = true;

        try
        {
            var inlineOrders = new InlineOrderDTO[localOrders.Count];
            for (int i = 0; i < localOrders.Count; i++)
            {
                inlineOrders[i] = new InlineOrderDTO
                {
                    ticker = localOrders[i].ticker,
                    side = localOrders[i].side,
                    quantity = localOrders[i].quantity,
                    order_type = "market"
                };
            }

            var req = new OpenMarketsRequestDTO
            {
                entity_id = APIBootstrapper.EntityExternalId,
                orders = inlineOrders
            };
            var resp = await OrderAPI.OpenMarkets(req);

            if (resp.status != "ok")
            {
                Debug.LogError($"[GamePhaseManager] OpenMarkets failed: {resp.message}");
                return null;
            }

            localOrders.Clear();
            todayResults.Clear();
            if (resp.trade_results != null)
            {
                foreach (var tr in resp.trade_results)
                {
                    todayResults.Add(new TradeResult
                    {
                        ticker = tr.ticker,
                        side = tr.side,
                        quantity = tr.quantity,
                        fillPrice = tr.fill_price,
                        status = tr.status,
                        message = tr.message
                    });
                }
            }

            CurrentDate = resp.current_date;
            CurrentPhase = GamePhase.Day;
            DayTimeRemaining = dayDurationSeconds;
            DayTimerActive = true;
            PostMarketReady = false;

            OnPhaseChanged?.Invoke(CurrentPhase);
            CheckKnowledgeTriggers();
            return new List<TradeResult>(todayResults);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GamePhaseManager] OpenMarkets exception: {ex.Message}");
            return null;
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

            var req = new CloseMarketsRequestDTO { entity_id = APIBootstrapper.EntityExternalId };
            var resp = await OrderAPI.CloseMarkets(req);

            if (resp.status != "ok")
            {
                Debug.LogError($"[GamePhaseManager] CloseMarkets failed: {resp.message}");
                return;
            }

            todayResults.Clear();
            if (resp.trade_results != null)
            {
                foreach (var tr in resp.trade_results)
                {
                    todayResults.Add(new TradeResult
                    {
                        ticker = tr.ticker,
                        side = tr.side,
                        quantity = tr.quantity,
                        fillPrice = tr.fill_price,
                        closePrice = tr.close_price,
                        pnl = tr.pnl,
                        status = tr.status,
                        message = tr.message
                    });
                }
            }

            CurrentDate = resp.current_date;
            CurrentPhase = GamePhase.PostMarket;
            PostMarketReady = true;
            OnPhaseChanged?.Invoke(CurrentPhase);
            CheckKnowledgeTriggers();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GamePhaseManager] CloseMarkets exception: {ex.Message}");
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
            localOrders.Clear();
            PostMarketReady = false;

            LastForcedLiquidations = resp.forced_liquidations;
            if (LastForcedLiquidations != null && LastForcedLiquidations.Length > 0)
                OnForcedLiquidations?.Invoke(LastForcedLiquidations);

            await CheckArcAdvance();
            await RefreshArcStatus();

            CurrentPhase = GamePhase.PreMarket;
            OnPhaseChanged?.Invoke(CurrentPhase);
            CheckKnowledgeTriggers();

            if (NewspaperHUDButton.Inst != null)
                NewspaperHUDButton.Inst.MarkUnread();

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GamePhaseManager] AdvanceToNextDay exception: {ex.Message}");
            return false;
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

    private void CheckKnowledgeTriggers()
    {
        if (KnowledgeGraphManager.Inst != null)
            _ = KnowledgeGraphManager.Inst.CheckTriggersAsync();
    }

    // ── Arc helpers ──

    private async Task RefreshArcStatus()
    {
        try
        {
            var arcResp = await ArcAPI.GetStatus(APIBootstrapper.EntityExternalId);
            if (arcResp.arc != null)
            {
                ArcName = arcResp.arc.name;
                CurrentArcDefinition = arcResp.arc;
                ArcDaysRemaining = arcResp.trading_days_remaining;
                ArcReturnPct = arcResp.current_return_pct;
                ArcProjectedGrade = arcResp.projected_grade;
            }
            else
            {
                ArcName = null;
                ArcDaysRemaining = -1;
                ArcProjectedGrade = null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GamePhaseManager] Arc status fetch failed: {ex.Message}");
        }
    }

    private async Task CheckArcAdvance()
    {
        try
        {
            var resp = await ArcAPI.Advance(APIBootstrapper.EntityExternalId);
            bool hasTransition = resp.transition != null
                && !string.IsNullOrEmpty(resp.transition.completed_arc?.arc_id);
            LastArcTransition = hasTransition ? resp.transition : null;
            if (hasTransition)
            {
                Debug.Log($"[GamePhaseManager] Arc completed: {resp.transition.completed_arc.arc_name} " +
                          $"Grade={resp.transition.completed_arc.grade} " +
                          $"Return={resp.transition.completed_arc.return_pct}%");
                OnArcTransition?.Invoke(resp.transition);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GamePhaseManager] Arc advance check failed: {ex.Message}");
        }
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
