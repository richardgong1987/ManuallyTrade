using System;

namespace cAlgo.Robots;

// Turns a manual pending-order request into a sized, validated order plan. Pure: it depends only
// on the IPdhpdlSymbolModel port, never on cAlgo, so it can be tested with a fake symbol.
//
// Sizing: riskMoney = equity × RiskPct%, volume = riskMoney / (stop distance in pips × PipValue),
// rounded down to a tradable step so a stop-out never loses more than the budget.
public class PdhpdlOrderPlanner {
    private readonly IPdhpdlSymbolModel _symbolModel;

    public PdhpdlOrderPlanner(IPdhpdlSymbolModel symbolModel) {
        _symbolModel = symbolModel;
    }

    public PdhpdlOrderPlanModel CreatePlan(PendingOrderRequestModel request, double accountEquity) {
        PdhpdlOrderPlanModel planModel = new() {
            DirectionModel = request.Direction,
            Label = request.Label,
            EntryPrice = request.EntryPrice,
            StopPrice = request.StopLossPrice,
            TakeProfitPrice = request.TakeProfitPrice
        };

        if (TryGetPriceRejectReason(request, out string rejectReason) ||
            TryGetRiskRejectReason(request.RiskPct, accountEquity, out rejectReason)) {
            planModel.RejectReason = rejectReason;
            return planModel;
        }

        double stopLossPips = Math.Abs(request.StopLossPrice - request.EntryPrice) / _symbolModel.PipSize;
        double riskMoney = accountEquity * request.RiskPct / 100.0;

        // PipValue is the account-currency value of one pip per unit, so the budget stays in the
        // account currency even when the symbol is quoted in another one (e.g. a EUR account on
        // USD-quoted XAUUSD). Dividing by the raw price distance would skip that conversion.
        double idealVolume = riskMoney / (stopLossPips * _symbolModel.PipValue);
        double volume = _symbolModel.NormalizeVolumeInUnits(idealVolume);

        if (TryGetVolumeRejectReason(volume, idealVolume, out rejectReason)) {
            planModel.RejectReason = rejectReason;
            return planModel;
        }

        planModel.IsValid = true;
        planModel.StopLossPips = stopLossPips;
        planModel.VolumeInUnits = volume;
        planModel.Lots = volume / _symbolModel.LotSize;
        planModel.RiskMoney = riskMoney;
        planModel.EstimatedRiskMoney = _symbolModel.AmountRisked(volume, stopLossPips);
        return planModel;
    }

    // A short needs stop > entry > take-profit and a long the reverse; anything else puts the stop
    // or the target on the wrong side of the entry.
    private static bool TryGetPriceRejectReason(PendingOrderRequestModel request, out string rejectReason) {
        rejectReason = "";

        if (request.EntryPrice <= 0.0 || request.StopLossPrice <= 0.0 || request.TakeProfitPrice <= 0.0) {
            rejectReason = $"Entry, stop-loss and take-profit prices must all be set. Entry={request.EntryPrice}, " +
                           $"StopLoss={request.StopLossPrice}, TakeProfit={request.TakeProfitPrice}";
            return true;
        }

        bool isShort = request.Direction == PdhpdlTradeDirectionModel.Short;
        bool arePricesOrdered = isShort
            ? request.StopLossPrice > request.EntryPrice && request.EntryPrice > request.TakeProfitPrice
            : request.StopLossPrice < request.EntryPrice && request.EntryPrice < request.TakeProfitPrice;

        if (!arePricesOrdered) {
            string expectedOrder = isShort ? "StopLoss > Entry > TakeProfit" : "StopLoss < Entry < TakeProfit";
            rejectReason = $"A {request.Direction} order needs {expectedOrder}. Entry={request.EntryPrice}, " +
                           $"StopLoss={request.StopLossPrice}, TakeProfit={request.TakeProfitPrice}";
            return true;
        }

        return false;
    }

    private static bool TryGetRiskRejectReason(double riskPct, double accountEquity, out string rejectReason) {
        rejectReason = "";

        if (riskPct <= 0.0) {
            rejectReason = $"Risk percentage must be positive. RiskPct={riskPct}";
            return true;
        }

        if (accountEquity <= 0.0) {
            rejectReason = $"Account equity must be positive. Equity={accountEquity}";
            return true;
        }

        return false;
    }

    private bool TryGetVolumeRejectReason(double volume, double idealVolume, out string rejectReason) {
        rejectReason = "";

        if (volume < _symbolModel.VolumeInUnitsMin) {
            rejectReason = $"Risk budget is too small for the broker minimum volume. IdealVolume={idealVolume}, " +
                           $"Min={_symbolModel.VolumeInUnitsMin}";
            return true;
        }

        if (volume > _symbolModel.VolumeInUnitsMax) {
            rejectReason = $"Calculated volume is above broker maximum. Volume={volume}, Max={_symbolModel.VolumeInUnitsMax}";
            return true;
        }

        return false;
    }
}
