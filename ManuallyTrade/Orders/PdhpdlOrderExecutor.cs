using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// Places and tracks the strategy's cTrader orders. It gates on the risk guard and open
// exposure, asks PdhpdlOrderPlanner to size the order, submits it as a market order, and
// keeps the CSV row ids so opens and closes can be reconciled. All sizing math lives in
// the planner.
public class PdhpdlOrderExecutor {
    private const string EntryComment = "ENTRY";

    private readonly Robot _robot;
    private readonly string _symbolName;
    private readonly string _timeFrame;

    // Orders are labelled "{OrderLabel}_L" / "{OrderLabel}_S". The side suffix keeps long and short
    // orders apart in the per-label CSV maps; the prefix marks this instance's orders, so manual
    // trades and other bots on the same symbol are ignored.
    private readonly string _strategyLabelPrefix;

    private readonly PdhpdlOrderPlanner _planner;
    private readonly PdhpdlRiskGuard _riskGuard;
    private readonly PdhpdlTradeCsvLogger _csvLogger;

    private readonly Dictionary<int, string> _positionCsvIds = new();
    private readonly Dictionary<int, double> _positionEntryEquities = new();

    public PdhpdlOrderExecutor(Robot robot, string symbolName, string timeFrame, string orderLabel, PdhpdlOrderPlanner planner,
        PdhpdlRiskGuard riskGuard, PdhpdlTradeCsvLogger csvLogger) {
        _robot = robot;
        _symbolName = symbolName;
        _timeFrame = timeFrame;
        _strategyLabelPrefix = orderLabel + "_";
        _planner = planner;
        _riskGuard = riskGuard;
        _csvLogger = csvLogger;

        _robot.Positions.Closed += OnPositionClosed;
    }

    public void Stop() {
        _robot.Positions.Closed -= OnPositionClosed;
    }

    public bool ExecuteIfSignal(PdhpdlSignalModel signalModel) {
        if (signalModel == null || !signalModel.HasData)
            return false;

        if (!signalModel.IsLongSignal && !signalModel.IsShortSignal)
            return false;

        if (_riskGuard.ShouldBlockNewOrder(_robot.Server.Time)) {
            _robot.Print("*****Order skipped | Risk guard blocked new order. Time: {0}", _robot.Server.Time);
            return false;
        }

        if (HasStrategyPosition()) {
            _robot.Print("*****Order skipped | Label {0}* already has an open position on symbol: {1}",
                _strategyLabelPrefix, _symbolName);
            return false;
        }

        PdhpdlOrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Reason: {0}", planModel.RejectReason);
            return false;
        }

        planModel.Label = _strategyLabelPrefix + (planModel.DirectionModel == PdhpdlTradeDirectionModel.Long ? "L" : "S");
        planModel.SignalName = signalModel.Label;
        planModel.KeyLevel = signalModel.KeyLevel;

        return ExecutePlan(planModel);
    }

    // Checks live broker state rather than in-memory maps, so a restart does not stack a second order.
    private bool HasStrategyPosition() {
        return _robot.Positions.Any(IsStrategyPosition);
    }

    private bool ExecutePlan(PdhpdlOrderPlanModel planModel) {
        _robot.Print(
            "*****Order plan | Side: {0}, Entry: {1}, Stop: {2}, TakeProfit: {3}, RiskPrice: {4}, StopLossPips: {5}, RiskMoney: {6}, EstimatedRiskMoney: {7}, Lots: {8}, VolumeUnits: {9}",
            planModel.DirectionModel, planModel.EntryPrice, planModel.StopPrice, planModel.TakeProfitPrice,
            planModel.RiskPrice, planModel.StopLossPips, planModel.RiskMoney, planModel.EstimatedRiskMoney, planModel.Lots,
            planModel.VolumeInUnits);

        TradeResult result = SubmitOrder(planModel);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return false;
        }

        _robot.Print("*****Order submitted | Label: {0}", planModel.Label);
        return RecordMarketEntry(planModel, result.Position);
    }

    private TradeResult SubmitOrder(PdhpdlOrderPlanModel planModel) {
        return _robot.ExecuteMarketOrder(ToTradeType(planModel.DirectionModel), _symbolName, planModel.VolumeInUnits, planModel.Label,
            planModel.StopLossPips, planModel.TakeProfitPips, EntryComment);
    }

    private bool RecordMarketEntry(PdhpdlOrderPlanModel planModel, Position position) {
        string csvId = _csvLogger.AppendEntry(planModel, position, _symbolName, _timeFrame);

        if (string.IsNullOrWhiteSpace(csvId))
            return false;

        _positionCsvIds[position.Id] = csvId;
        _positionEntryEquities[position.Id] = planModel.AccountEquity;
        _robot.Print("*****CSV trade record added. Path: {0}", _csvLogger.FilePath);
        return true;
    }

    private void OnPositionClosed(PositionClosedEventArgs args) {
        if (args?.Position == null || !IsStrategyPosition(args.Position))
            return;

        string csvId = GetPositionCsvId(args.Position);
        double entryEquity = GetPositionEntryEquity(args.Position);
        double closePrice = GetClosePrice(args.Position);
        string closeRecordId = _csvLogger.AppendClose(args.Position, args.Reason, csvId, _symbolName, _timeFrame, _robot.Server.Time,
            closePrice, entryEquity, _robot.Account.Equity);

        _positionCsvIds.Remove(args.Position.Id);
        _positionEntryEquities.Remove(args.Position.Id);

        if (!string.IsNullOrWhiteSpace(closeRecordId))
            _robot.Print("*****CSV close record added. Id: {0}, ProfitLoss: {1}", closeRecordId, args.Position.NetProfit);
    }

    private bool IsStrategyPosition(Position position) {
        return position.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(position.Label) &&
               position.Label.StartsWith(_strategyLabelPrefix);
    }

    private string GetPositionCsvId(Position position) {
        return _positionCsvIds.TryGetValue(position.Id, out string csvId) ? csvId : position.Id.ToString();
    }

    private double GetPositionEntryEquity(Position position) {
        return _positionEntryEquities.TryGetValue(position.Id, out double entryEquity) ? entryEquity : 0.0;
    }

    private double GetClosePrice(Position position) {
        HistoricalTrade[] closedTrades = _robot.History.FindByPositionId(position.Id);

        if (closedTrades != null && closedTrades.Length > 0)
            return closedTrades.OrderByDescending(trade => trade.ClosingTime).First().ClosingPrice;

        for (int i = position.Deals.Count - 1; i >= 0; i--) {
            Deal deal = position.Deals[i];

            if (deal.PositionImpact == DealPositionImpact.Closing && deal.ExecutionPrice.HasValue)
                return deal.ExecutionPrice.Value;
        }

        return 0.0;
    }

    private static TradeType ToTradeType(PdhpdlTradeDirectionModel directionModel) {
        return directionModel == PdhpdlTradeDirectionModel.Long ? TradeType.Buy : TradeType.Sell;
    }
}
