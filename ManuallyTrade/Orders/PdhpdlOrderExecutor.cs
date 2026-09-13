using System.Collections.Generic;
using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// Places the strategy's cTrader orders. It gates on the risk guard and open exposure, asks
// PdhpdlOrderPlanner to size the order, submits it, and cancels pending orders that go stale.
// All sizing math lives in the planner.
public class PdhpdlOrderExecutor {
    private const string StrategyLabelPrefix = PdhpdlOrderPlanner.LabelPrefix + "_";
    private const string EntryComment = "ENTRY";

    // 挂单最多等 3 根收盘 K 线；等不到回撤就撤单。
    private const int PendingOrderExpiryBars = 3;

    private readonly Robot _robot;
    private readonly string _symbolName;

    private readonly PdhpdlOrderPlanner _planner;
    private readonly PdhpdlRiskGuard _riskGuard;

    private readonly Dictionary<int, int> _pendingOrderBarIndexById = new();

    public PdhpdlOrderExecutor(Robot robot, string symbolName, PdhpdlOrderPlanner planner, PdhpdlRiskGuard riskGuard) {
        _robot = robot;
        _symbolName = symbolName;
        _planner = planner;
        _riskGuard = riskGuard;
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

        if (HasOpenSymbolPosition() || HasOpenSymbolPendingOrder()) {
            _robot.Print("*****Order skipped | Existing position found on symbol: {0}", _symbolName);
            return false;
        }


        PdhpdlOrderPlanModel planModel = _planner.CreatePlan(signalModel, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Reason: {0}", planModel.RejectReason);
            return false;
        }

        planModel.SignalBarIndex = signalModel.BarIndex;

        return ExecutePlan(planModel);
    }

    private bool HasOpenSymbolPosition() {
        return _robot.Positions.Any(position => position.SymbolName == _symbolName);
    }

    private bool HasOpenSymbolPendingOrder() {
        return _robot.PendingOrders.Any(order => order.SymbolName == _symbolName);
    }

    // 挂单是「等价格回撤」，回撤没来就说明这笔已经作废：只给它 PendingOrderExpiryBars
    // 根收盘 K 线的时间，超时撤单，避免行情早已走远后挂单还在原地等着被扫。
    public void CancelExpiredPendingOrders(int closedBarIndex) {
        ForgetFilledPendingOrders();

        foreach (PendingOrder order in _robot.PendingOrders.Where(IsStrategyPendingOrder).ToArray()) {
            if (!IsPendingOrderExpired(order, closedBarIndex))
                continue;

            CancelPendingOrder(order, $"unfilled after {PendingOrderExpiryBars} bars");
        }
    }

    private bool IsPendingOrderExpired(PendingOrder order, int closedBarIndex) {
        // 本次运行之前就存在的挂单没有下单 K 线记录，不归这条规则管。
        if (!_pendingOrderBarIndexById.TryGetValue(order.Id, out int placedBarIndex))
            return false;

        return closedBarIndex - placedBarIndex >= PendingOrderExpiryBars;
    }

    private void ForgetFilledPendingOrders() {
        HashSet<int> liveOrderIds = new(_robot.PendingOrders.Select(order => order.Id));

        foreach (int orderId in _pendingOrderBarIndexById.Keys.Where(id => !liveOrderIds.Contains(id)).ToArray())
            _pendingOrderBarIndexById.Remove(orderId);
    }

    private void CancelPendingOrder(PendingOrder order, string reason) {
        TradeResult result = _robot.CancelPendingOrder(order);

        if (!result.IsSuccessful) {
            _robot.Print("*****Pending cancel failed | Order: {0}, Reason: {1}, Error: {2}", order.Id, reason, result.Error);
            return;
        }

        _pendingOrderBarIndexById.Remove(order.Id);
        _robot.Print("*****Pending order cancelled | Order: {0}, Reason: {1}", order.Id, reason);
    }

    private bool ExecutePlan(PdhpdlOrderPlanModel planModel) {
        _robot.Print(
            "*****Order plan | Side: {0}, Entry: {1}, Stop: {2}, TakeProfit: {3}, RiskPrice: {4}, StopLossPips: {5}, RiskMoney: {6}, EstimatedRiskMoney: {7}, Lots: {8}, VolumeUnits: {9}",
            planModel.DirectionModel, planModel.EntryPrice, planModel.StopPrice, planModel.TakeProfitPrice, planModel.RiskPrice,
            planModel.StopLossPips, planModel.RiskMoney, planModel.EstimatedRiskMoney, planModel.Lots, planModel.VolumeInUnits);

        TradeResult result = _robot.PlaceLimitOrder(ToTradeType(planModel.DirectionModel), _symbolName, planModel.VolumeInUnits,
            planModel.EntryPrice, planModel.Label, planModel.StopLossPips, planModel.TakeProfitPips, ProtectionType.Relative, null,
            EntryComment);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Error: {0}", result.Error);
            return false;
        }

        _pendingOrderBarIndexById[result.PendingOrder.Id] = planModel.SignalBarIndex;
        _robot.Print("*****Order submitted | Label: {0}", planModel.Label);
        return true;
    }

    private bool IsStrategyPendingOrder(PendingOrder order) {
        return order.SymbolName == _symbolName && !string.IsNullOrWhiteSpace(order.Label) && order.Label.StartsWith(StrategyLabelPrefix);
    }

    private static TradeType ToTradeType(PdhpdlTradeDirectionModel directionModel) {
        return directionModel == PdhpdlTradeDirectionModel.Long ? TradeType.Buy : TradeType.Sell;
    }
}
