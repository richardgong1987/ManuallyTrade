namespace cAlgo.Robots;

// One manually configured pending order: where to enter, where to exit, and how much of the
// account to risk. Pure data, no cAlgo dependency.
public class PendingOrderRequestModel {
    public string Label { get; set; } = "";
    public PdhpdlTradeDirectionModel Direction { get; set; }

    public double EntryPrice { get; set; }
    public double StopLossPrice { get; set; }
    public double TakeProfitPrice { get; set; }

    // Percentage of account equity lost if the stop is hit (1 = 1%).
    public double RiskPct { get; set; }
}
