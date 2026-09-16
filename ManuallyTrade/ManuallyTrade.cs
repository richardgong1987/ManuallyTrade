using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.FullAccess, AddIndicators = false)]
public class ManuallyTrade : Robot {
    [Parameter("订单标签", DefaultValue = "ManuallyTrade-label")]
    public string OrderLabel { get; set; }

    [Parameter("每笔交易风险百分比，默认1%", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0, Step = 0.1, Group = "风控配置")]
    public double RiskPct { get; set; }

    [Parameter("风险安全系数", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 1.0, Step = 0.05, Group = "风控配置")]
    public double RiskSafetyFactor { get; set; }

    [Parameter("止损偏移点数", DefaultValue = 50, MinValue = 0, MaxValue = 1000, Group = "风控配置")]
    public int StopOffsetTicks { get; set; }

    [Parameter("最小止损点数 (Pips)", DefaultValue = 5.0, MinValue = 0.0, Step = 0.1, Group = "风控配置")]
    public double MinStopLossPips { get; set; }

    [Parameter("止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "风控配置")]
    public double TakeProfitR { get; set; }

    [Parameter("回撤开仓模式", DefaultValue = PdhpdlEntryModel.Close, Group = "风控配置")]
    public PdhpdlEntryModel EntryModel { get; set; }

    [Parameter("周六强制平仓小时（日本时间）", DefaultValue = 5, MinValue = 0, MaxValue = 23, Group = "基本面设置")]
    public int SaturdayForceCloseHour { get; set; }

    [Parameter("周六强制平仓分钟（日本时间）", DefaultValue = 30, MinValue = 0, MaxValue = 59, Group = "基本面设置")]
    public int SaturdayForceCloseMinute { get; set; }

    [Parameter("五星数据空仓时间段", DefaultValue = "", Group = "基本面设置")]
    public string NewsBlackoutWindows { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = false, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("展示调试日志", DefaultValue = false, Group = "开发调试")]
    public bool ShowDebugLogs { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "pdhpdl-trades.csv", Group = "开发调试")]
    public string FileName { get; set; }

    private PdhpdlLines _pdhpdlLines;
    private PdhpdlSignalDetector _signalDetector;
    private PdhpdlSignalMarkers _signalMarkers;
    private PdhpdlOrderExecutor _orderExecutor;
    private PdhpdlTradeCsvLogger _csvLogger;
    private Atr14Series _atr14;
    private DateTime _optimisationWindowStart;

    protected override void OnStart() {
        // A blank label would make every "_L"/"_S" label on the symbol look like this bot's order.
        if (string.IsNullOrWhiteSpace(OrderLabel)) {
            Print("*****OrderLabel must not be empty. cBot stopped.");
            Stop();
            return;
        }

        _optimisationWindowStart = Server.Time;
        LaunchDebug();
        _atr14 = new Atr14Series(Indicators, Bars);
        Bars dailyBars = MarketData.GetBars(TimeFrame.Daily, SymbolName);

        _signalDetector = new PdhpdlSignalDetector(Bars, dailyBars);
        _signalMarkers = new PdhpdlSignalMarkers(Chart, Symbol.TickSize);

        _csvLogger = new PdhpdlTradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new PdhpdlRiskGuard(BuildRiskGuardConfig());
        var planner = new PdhpdlOrderPlanner(new CAlgoSymbolModel(Symbol), riskGuard, StopOffsetTicks, TakeProfitR, EntryModel, RiskPct);
        _orderExecutor = new PdhpdlOrderExecutor(this, SymbolName, Bars.TimeFrame.ToString(), OrderLabel.Trim(), planner, riskGuard,
            _csvLogger);
        DrawPdhpdlLines();
        Print("*****PDH/PDL Break and Reverse started.");
    }

    private void DrawPdhpdlLines() {
        _pdhpdlLines = new PdhpdlLines(Chart, MarketData, SymbolName, Bars, 3);
        _pdhpdlLines.Draw();
    }

    private void LaunchDebug() {
        if (IsDebug) {
            bool result = Debugger.Launch();
            if (!result) {
                Print("Debugger launch failed");
            }
        }
    }

    // 输出目录按运行模式分开、互不覆盖：回测目录由脚本每次清空重建，模拟/实盘目录只追加、从不删除。
    // 回测经 run_conditions 传入绝对路径 FileName，此目录会被忽略（见 PdhpdlTradeCsvLogger）。
    private string ResolveReportsDirectory() {
        string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, ResolveReportsFolderName());
    }

    private string ResolveReportsFolderName() {
        if (IsBacktesting)
            return "trading_reports";

        return Account.IsLive ? "release_trading_reports" : "simulate_trading_reports";
    }

    private PdhpdlRiskGuardConfigModel BuildRiskGuardConfig() {
        return new PdhpdlRiskGuardConfigModel {
            RiskSafetyFactor = RiskSafetyFactor,
            MinStopLossPips = MinStopLossPips,
            SaturdayForceCloseHour = SaturdayForceCloseHour,
            SaturdayForceCloseMinute = SaturdayForceCloseMinute,
            NewsBlackoutWindows = NewsBlackoutWindows
        };
    }

    protected override void OnBar() {
        _pdhpdlLines?.Draw();
        _orderExecutor?.ManageOpenPositions();
        // 先撤过期挂单再看新信号：让作废的挂单不再占住「本品种已有挂单」这个名额。
        _orderExecutor?.CancelExpiredPendingOrders(Bars.Count - 2);
        HandleClosedBarSignal();
    }

    protected override void OnTick() {
        _orderExecutor?.ManageOpenPositions();
    }

    private void HandleClosedBarSignal() {
        PdhpdlSignalModel signalModel = _signalDetector.DetectOnClosedBar();
        if (!signalModel.HasData)
            return;

        signalModel.IsBigK = _atr14.IsBarRangeTooLarge(signalModel.BarIndex, signalModel.High, signalModel.Low, 3);

        if (_orderExecutor.ExecuteIfSignal(signalModel)) {
            _signalMarkers.Draw(signalModel);
        }
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
    }

    protected override void OnBarClosed() { }

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
}
