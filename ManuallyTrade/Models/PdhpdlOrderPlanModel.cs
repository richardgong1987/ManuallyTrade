namespace cAlgo.Robots;

// The sized order the planner produces from a request. Pure data, no cAlgo dependency.
public class PdhpdlOrderPlanModel {
    public bool IsValid { get; set; }
    public string RejectReason { get; set; } = "";

    public PdhpdlTradeDirectionModel DirectionModel { get; set; }
    public string Label { get; set; } = "";

    public double EntryPrice { get; set; }
    public double StopPrice { get; set; }
    public double TakeProfitPrice { get; set; }
    public double StopLossPips { get; set; }

    public double Lots { get; set; }
    public double VolumeInUnits { get; set; }

    public double RiskMoney { get; set; }
    public double EstimatedRiskMoney { get; set; }
}
