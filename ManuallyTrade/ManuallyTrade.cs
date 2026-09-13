using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.None, AddIndicators = false)]
public class ManuallyTrade : Robot {
    [Parameter("风险1%", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1RiskPct { get; set; }

    [Parameter("空1止盈价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1TakeProfit { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    private PdhpdlOrderExecutor _orderExecutor;

    // Optimisation only: GetFitness checks the whole run year by year, and GetFitnessArgs does not
    // carry the window, so the robot has to remember where it started.
    private DateTime _optimisationWindowStart;

    protected override void OnStart() {
        _optimisationWindowStart = Server.Time;
        LaunchDebug();

        var riskGuard = new PdhpdlRiskGuard();
        var planner = new PdhpdlOrderPlanner(new CAlgoSymbolModel(Symbol), riskGuard, Short1TakeProfit, Short1RiskPct);
        _orderExecutor = new PdhpdlOrderExecutor(this, SymbolName, planner, riskGuard);

        Print("*****MovingAverageV1 started.");
    }

    private void LaunchDebug() {
        if (IsDebug) {
            bool result = Debugger.Launch();
            if (!result) {
                Print("Debugger launch failed");
            }
        }
    }

    protected override void OnBar() {
        _orderExecutor?.CancelExpiredPendingOrders(Bars.Count - 2);
    }

    // Called once per pass by the desktop Optimisation tab only — a plain backtest, CLI or GUI,
    // never calls it. Passes with a losing (or idle) calendar year sink below every survivor;
    // survivors keep cTrader's own score. See AnnualFitness.
    protected override double GetFitness(GetFitnessArgs args) {
        List<ClosedTradeModel> closedTrades = args.History
            .Select(trade => new ClosedTradeModel(trade.ClosingTime, trade.NetProfit)).ToList();

        var stats = new FitnessStatsModel {
            NetProfit = args.NetProfit, WinningTrades = args.WinningTrades, MaxEquityDrawdownPercent = args.MaxEquityDrawdownPercentages
        };

        return new AnnualFitness(_optimisationWindowStart, Server.Time).Calculate(closedTrades, stats);
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
    }
}
