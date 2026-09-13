using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.None, AddIndicators = false)]
public class ManuallyTrade : Robot {
    [Parameter("风险1%", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 15.0, Step = 0.1, Group = "风控配置")]
    public double RiskPct { get; set; }

    [Parameter("止盈价", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 30.0, Step = 0.1, Group = "风控配置")]
    public double TakeProfitR { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    private PdhpdlSignalDetector _signalDetector;
    private PdhpdlSignalMarkers _signalMarkers;
    private PdhpdlOrderExecutor _orderExecutor;

    // 图表周期的 ATR，只服务 IsBigK（比较图表 K 线自身的振幅）。
    private Atr14Series _atr14;

    // Optimisation only: GetFitness checks the whole run year by year, and GetFitnessArgs does not
    // carry the window, so the robot has to remember where it started.
    private DateTime _optimisationWindowStart;

    protected override void OnStart() {
        _optimisationWindowStart = Server.Time;
        LaunchDebug();
        _atr14 = new Atr14Series(Indicators, Bars);

        _signalDetector = new PdhpdlSignalDetector(Bars);
        _signalMarkers = new PdhpdlSignalMarkers(Chart, Symbol.TickSize);

        var riskGuard = new PdhpdlRiskGuard();
        var planner = new PdhpdlOrderPlanner(new CAlgoSymbolModel(Symbol), riskGuard, TakeProfitR, RiskPct);
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
        // 先撤过期挂单再看新信号：让作废的挂单不再占住「本品种已有挂单」这个名额。
        _orderExecutor?.CancelExpiredPendingOrders(Bars.Count - 2);
        HandleClosedBarSignal();
    }

    private void HandleClosedBarSignal() {
        PdhpdlSignalModel signalModel = _signalDetector.DetectOnClosedBar();
        if (!signalModel.HasData)
            return;

        if (signalModel.IsLongSignal) {
            Print("*****LONG trigger | Time: {0}, Signal: {1}, Low: {2}, Close: {3}", signalModel.BarTime, signalModel.Label,
                signalModel.Low, signalModel.Close);
        }

        if (signalModel.IsShortSignal) {
            Print("*****SHORT trigger | Time: {0}, Signal: {1}, High: {2}, Close: {3}", signalModel.BarTime, signalModel.Label,
                signalModel.High, signalModel.Close);
        }

        signalModel.IsBigK = _atr14.IsBarRangeTooLarge(signalModel.BarIndex, signalModel.High, signalModel.Low, 3);
        if (_orderExecutor.ExecuteIfSignal(signalModel)) {
            _signalMarkers.Draw(signalModel);
        }
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
