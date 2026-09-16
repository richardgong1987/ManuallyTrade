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

    [Parameter("止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "风控配置")]
    public double TakeProfitR { get; set; }

    [Parameter("启动时清空交易记录CSV", DefaultValue = false, Group = "开发调试")]
    public bool ResetTradeLogOnStart { get; set; }

    [Parameter("展示调试日志", DefaultValue = false, Group = "开发调试")]
    public bool ShowDebugLogs { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    [Parameter("输出文件名", DefaultValue = "ManuallyTrades.csv", Group = "开发调试")]
    public string FileName { get; set; }

    [Parameter("空1风险1%", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1RiskPct { get; set; }

    [Parameter("空1入场价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double PDH1 { get; set; }

    [Parameter("空1止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "空1")]
    public double Short1TPPrice { get; set; }

    [Parameter("空2风险%", DefaultValue = 0, MinValue = 0, Group = "空2")]
    public double Short2RiskPct { get; set; }

    [Parameter("空2入场价", DefaultValue = 0, MinValue = 0, Group = "空2")]
    public double PDH2 { get; set; }

    [Parameter("空2止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "空2")]
    public double Short2TPPrice { get; set; }

    [Parameter("空3风险%", DefaultValue = 0, MinValue = 0, Group = "空3")]
    public double Short3RiskPct { get; set; }

    [Parameter("空3入场价", DefaultValue = 0, MinValue = 0, Group = "空3")]
    public double PDH3 { get; set; }

    [Parameter("空3止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "空3")]
    public double Short3TPPrice { get; set; }


    [Parameter("多1风险%", DefaultValue = 0, MinValue = 0, Group = "多1")]
    public double Long1RiskPct { get; set; }

    [Parameter("多1入场价", DefaultValue = 0, MinValue = 0, Group = "多1")]
    public double PDL1 { get; set; }

    [Parameter("多1止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "多1")]
    public double Long1TPPrice { get; set; }


    [Parameter("多2风险%", DefaultValue = 0, MinValue = 0, Group = "多2")]
    public double Long2RiskPct { get; set; }

    [Parameter("多2入场价", DefaultValue = 0, MinValue = 0, Group = "多2")]
    public double PDL2 { get; set; }

    [Parameter("多2止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "多2")]
    public double Long2TPPrice { get; set; }


    [Parameter("多3风险%", DefaultValue = 0, MinValue = 0, Group = "多3")]
    public double Long3RiskPct { get; set; }

    [Parameter("多3入场价", DefaultValue = 0, MinValue = 0, Group = "多3")]
    public double PDL3 { get; set; }

    [Parameter("多3止盈目标", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0, Step = 0.1, Group = "多3")]
    public double Long3TPPrice { get; set; }

    private PdhpdlLines _pdhpdlLines;
    private PdhpdlSignalDetector _signalDetector;
    private PdhpdlSignalMarkers _signalMarkers;
    private PdhpdlOrderExecutor _orderExecutor;
    private PdhpdlTradeCsvLogger _csvLogger;
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
        Bars dailyBars = MarketData.GetBars(TimeFrame.Daily, SymbolName);

        _signalDetector = new PdhpdlSignalDetector(Bars, dailyBars);
        _signalMarkers = new PdhpdlSignalMarkers(Chart, Symbol.TickSize);

        _csvLogger = new PdhpdlTradeCsvLogger(ResetTradeLogOnStart, ResolveReportsDirectory(), FileName);
        Print("****CSV logger path: {0}", _csvLogger.FilePath);

        var riskGuard = new PdhpdlRiskGuard();
        var planner = new PdhpdlOrderPlanner(new CAlgoSymbolModel(Symbol), riskGuard, TakeProfitR, RiskPct);
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

    protected override void OnBar() {
        _pdhpdlLines?.Draw();
        HandleClosedBarSignal();
    }

    private void HandleClosedBarSignal() {
        PdhpdlSignalModel signalModel = _signalDetector.DetectOnClosedBar();
        if (!signalModel.HasData)
            return;

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
