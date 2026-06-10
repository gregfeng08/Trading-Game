using System;
using System.Collections.Generic;

public static class ProgressionGates
{
    public static bool ShowNetWorth { get; private set; }
    public static bool ShowCandlesticks { get; private set; }
    public static bool ShowPriceChange { get; private set; }
    public static bool ShowWicks { get; private set; }
    public static bool ShowCostBasis { get; private set; }
    public static bool ShowFullTickerUniverse { get; private set; } // unused, kept for future
    public static bool ShowPortfolioChart { get; private set; }
    // DISABLED: limit/stop order UI commented out
    // public static bool ShowLimitOrders { get; private set; }
    // public static bool ShowStopOrders { get; private set; }
    // public static bool ShowStopLimitOrders { get; private set; }
    public static bool ShowLimitOrders => false;
    public static bool ShowStopOrders => false;
    public static bool ShowStopLimitOrders => false;
    public static bool ShowExtendedArcInfo { get; private set; }
    public static bool ShowCashBreakdown { get; private set; }
    public static bool ShowAllTimeframes { get; private set; }

    public static event Action OnGatesChanged;
    public static event Action<string, string> OnFeatureUnlocked;

    private static readonly Dictionary<string, Action<bool>> GateMap = new()
    {
        { "what_is_a_stock",  v => ShowNetWorth = v },
        { "market_buy_sell",  v => { ShowCandlesticks = v; ShowPriceChange = v; } },
        { "volatility",       v => ShowWicks = v },
        { "cost_basis",       v => ShowCostBasis = v },
        { "diversification",  v => ShowFullTickerUniverse = v },
        { "paper_gains",      v => ShowPortfolioChart = v },
        // DISABLED: limit/stop order gates commented out
        // { "limit_buy_sell",   v => ShowLimitOrders = v },
        // { "stop_orders",      v => ShowStopOrders = v },
        // { "stop_limit_orders", v => ShowStopLimitOrders = v },
        { "market_cycles",    v => ShowExtendedArcInfo = v },
        { "cash_management",  v => ShowCashBreakdown = v },
        { "market_timing",    v => ShowAllTimeframes = v },
    };

    private static bool initialized;

    public static void Initialize()
    {
        if (KnowledgeGraphManager.Inst != null)
            KnowledgeGraphManager.Inst.OnGraphRefreshed += Evaluate;

        Evaluate();
        initialized = true;
    }

    public static void Evaluate()
    {
        var mgr = KnowledgeGraphManager.Inst;
        if (mgr == null) return;

        bool changed = false;

        var newlyUnlocked = new System.Collections.Generic.List<string>();

        foreach (var kvp in GateMap)
        {
            bool completed = mgr.IsNodeCompleted(kvp.Key);
            bool current = GetFlag(kvp.Key);
            if (completed != current)
            {
                kvp.Value(completed);
                changed = true;
                if (completed)
                    newlyUnlocked.Add(kvp.Key);
            }
        }

        if (changed)
            OnGatesChanged?.Invoke();

        foreach (var nodeId in newlyUnlocked)
        {
            var label = GetFeatureLabel(nodeId);
            if (label != null)
                OnFeatureUnlocked?.Invoke(nodeId, label);
        }
    }

    private static readonly Dictionary<string, string> FeatureLabels = new()
    {
        { "what_is_a_stock",  "Net worth display" },
        { "market_buy_sell",  "Candlestick chart & price change indicators" },
        { "volatility",       "Candlestick wicks (high/low price range)" },
        { "cost_basis",       "Portfolio cost basis and gain/loss" },
        { "paper_gains",      "Portfolio value chart" },
        // DISABLED: limit/stop order feature labels commented out
        // { "limit_buy_sell",   "Limit orders" },
        // { "stop_orders",      "Stop orders" },
        // { "stop_limit_orders", "Stop-limit orders" },
        { "market_cycles",    "Arc details (days remaining, grade, return %)" },
        { "cash_management",  "Net worth breakdown (holdings value)" },
        { "market_timing",    "All chart timeframes (1W, 3M, 1Y, 5Y)" },
    };

    public static string GetFeatureLabel(string nodeId)
    {
        return FeatureLabels.TryGetValue(nodeId, out var label) ? label : null;
    }

    private static bool GetFlag(string nodeId)
    {
        return nodeId switch
        {
            "what_is_a_stock"  => ShowNetWorth,
            "market_buy_sell"  => ShowCandlesticks,
            "volatility"       => ShowWicks,
            "cost_basis"       => ShowCostBasis,
            "diversification"  => ShowFullTickerUniverse,
            "paper_gains"      => ShowPortfolioChart,
            // DISABLED: limit/stop order flags commented out
            // "limit_buy_sell"   => ShowLimitOrders,
            // "stop_orders"      => ShowStopOrders,
            // "stop_limit_orders" => ShowStopLimitOrders,
            "market_cycles"    => ShowExtendedArcInfo,
            "cash_management"  => ShowCashBreakdown,
            "market_timing"    => ShowAllTimeframes,
            _ => false
        };
    }
}
