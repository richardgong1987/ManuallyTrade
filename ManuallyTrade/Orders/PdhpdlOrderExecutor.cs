using System.Linq;
using cAlgo.API;

namespace cAlgo.Robots;

// Submits a planned pending order to cTrader. All sizing and price validation lives in
// PdhpdlOrderPlanner; this class only talks to the broker.
public class PdhpdlOrderExecutor {
    private const string EntryComment = "ENTRY";

    private readonly Robot _robot;
    private readonly PdhpdlOrderPlanner _planner;

    public PdhpdlOrderExecutor(Robot robot, PdhpdlOrderPlanner planner) {
        _robot = robot;
        _planner = planner;
    }

    public void PlacePendingOrder(PendingOrderRequestModel request) {
        // A restart must not stack a second copy of an order that is still working or already filled.
        if (HasOrderOrPosition(request.Label)) {
            _robot.Print("*****Order skipped | Label {0} already has a pending order or open position.", request.Label);
            return;
        }

        PdhpdlOrderPlanModel planModel = _planner.CreatePlan(request, _robot.Account.Equity);

        if (!planModel.IsValid) {
            _robot.Print("*****Order rejected | Label: {0}, Reason: {1}", request.Label, planModel.RejectReason);
            return;
        }

        _robot.Print(
            "*****Order plan | Label: {0}, Side: {1}, Entry: {2}, Stop: {3}, TakeProfit: {4}, StopLossPips: {5}, RiskMoney: {6}, EstimatedRiskMoney: {7}, Lots: {8}, VolumeUnits: {9}",
            planModel.Label, planModel.DirectionModel, planModel.EntryPrice, planModel.StopPrice, planModel.TakeProfitPrice,
            planModel.StopLossPips, planModel.RiskMoney, planModel.EstimatedRiskMoney, planModel.Lots, planModel.VolumeInUnits);

        TradeResult result = SubmitOrder(planModel);

        if (!result.IsSuccessful) {
            _robot.Print("*****Order failed | Label: {0}, Error: {1}", planModel.Label, result.Error);
            return;
        }

        _robot.Print("*****Order submitted | Label: {0}, Type: {1}, Id: {2}", planModel.Label, result.PendingOrder.OrderType,
            result.PendingOrder.Id);
    }

    private bool HasOrderOrPosition(string label) {
        return _robot.PendingOrders.Any(order => order.SymbolName == _robot.SymbolName && order.Label == label) ||
               _robot.Positions.Any(position => position.SymbolName == _robot.SymbolName && position.Label == label);
    }

    // cTrader accepts a limit order only on the better side of the market (sell above Bid, buy below
    // Ask) and a stop order only on the worse side, so the entry relative to the quote picks the type.
    private TradeResult SubmitOrder(PdhpdlOrderPlanModel planModel) {
        bool isShort = planModel.DirectionModel == PdhpdlTradeDirectionModel.Short;
        TradeType tradeType = isShort ? TradeType.Sell : TradeType.Buy;
        bool isLimitOrder = isShort ? planModel.EntryPrice > _robot.Symbol.Bid : planModel.EntryPrice < _robot.Symbol.Ask;

        if (isLimitOrder) {
            return _robot.PlaceLimitOrder(tradeType, _robot.SymbolName, planModel.VolumeInUnits, planModel.EntryPrice, planModel.Label,
                planModel.StopPrice, planModel.TakeProfitPrice, ProtectionType.Absolute, null, EntryComment);
        }

        return _robot.PlaceStopOrder(tradeType, _robot.SymbolName, planModel.VolumeInUnits, planModel.EntryPrice, planModel.Label,
            planModel.StopPrice, planModel.TakeProfitPrice, ProtectionType.Absolute, null, EntryComment);
    }
}
