using System.Diagnostics;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.None, AddIndicators = false)]
public class ManuallyTrade : Robot {
    [Parameter("风险1%", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1RiskPct { get; set; }

    [Parameter("空1入场价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1Price { get; set; }

    [Parameter("空1止盈价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1TPPrice { get; set; }

    [Parameter("空1止损价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1SLPrice { get; set; }

    [Parameter("空2风险%", DefaultValue = 0, MinValue = 0, Group = "空2")]
    public double Short2RiskPct { get; set; }

    [Parameter("空2入场价", DefaultValue = 0, MinValue = 0, Group = "空2")]
    public double Short2Price { get; set; }

    [Parameter("空2止盈价", DefaultValue = 0, MinValue = 0, Group = "空2")]
    public double Short2TPPrice { get; set; }

    [Parameter("空2止损价", DefaultValue = 0, MinValue = 0, Group = "空2")]
    public double Short2SLPrice { get; set; }

    [Parameter("空3风险%", DefaultValue = 0, MinValue = 0, Group = "空3")]
    public double Short3RiskPct { get; set; }

    [Parameter("空3入场价", DefaultValue = 0, MinValue = 0, Group = "空3")]
    public double Short3Price { get; set; }

    [Parameter("空3止盈价", DefaultValue = 0, MinValue = 0, Group = "空3")]
    public double Short3TPPrice { get; set; }

    [Parameter("空3止损价", DefaultValue = 0, MinValue = 0, Group = "空3")]
    public double Short3SLPrice { get; set; }

    [Parameter("多1风险%", DefaultValue = 0, MinValue = 0, Group = "多1")]
    public double Long1RiskPct { get; set; }

    [Parameter("多1入场价", DefaultValue = 0, MinValue = 0, Group = "多1")]
    public double Long1Price { get; set; }

    [Parameter("多1止盈价", DefaultValue = 0, MinValue = 0, Group = "多1")]
    public double Long1TPPrice { get; set; }

    [Parameter("多1止损价", DefaultValue = 0, MinValue = 0, Group = "多1")]
    public double Long1SLPrice { get; set; }

    [Parameter("多2风险%", DefaultValue = 0, MinValue = 0, Group = "多2")]
    public double Long2RiskPct { get; set; }

    [Parameter("多2入场价", DefaultValue = 0, MinValue = 0, Group = "多2")]
    public double Long2Price { get; set; }

    [Parameter("多2止盈价", DefaultValue = 0, MinValue = 0, Group = "多2")]
    public double Long2TPPrice { get; set; }

    [Parameter("多2止损价", DefaultValue = 0, MinValue = 0, Group = "多2")]
    public double Long2SLPrice { get; set; }

    [Parameter("多3风险%", DefaultValue = 0, MinValue = 0, Group = "多3")]
    public double Long3RiskPct { get; set; }

    [Parameter("多3入场价", DefaultValue = 0, MinValue = 0, Group = "多3")]
    public double Long3Price { get; set; }

    [Parameter("多3止盈价", DefaultValue = 0, MinValue = 0, Group = "多3")]
    public double Long3TPPrice { get; set; }

    [Parameter("多3止损价", DefaultValue = 0, MinValue = 0, Group = "多3")]
    public double Long3SLPrice { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    protected override void OnStart() {
        LaunchDebug();

        var orderExecutor = new PdhpdlOrderExecutor(this, new PdhpdlOrderPlanner(new CAlgoSymbolModel(Symbol)));

        foreach (PendingOrderRequestModel request in BuildOrderRequests()) {
            // Entry and take-profit both set means the slot is in use. A missing stop-loss or risk
            // percentage is rejected by the planner with a logged reason rather than silently skipped.
            if (request.EntryPrice <= 0.0 || request.TakeProfitPrice <= 0.0)
                continue;

            orderExecutor.PlacePendingOrder(request);
        }

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

    // Each slot has its own label, so the executor's duplicate check only blocks a restart from
    // re-placing that same slot; slots never block each other.
    private PendingOrderRequestModel[] BuildOrderRequests() {
        const PdhpdlTradeDirectionModel Short = PdhpdlTradeDirectionModel.Short;
        const PdhpdlTradeDirectionModel Long = PdhpdlTradeDirectionModel.Long;

        return new[] {
            OrderRequest(Short, "ManuallyTrade_Short1", Short1Price, Short1SLPrice, Short1TPPrice, Short1RiskPct),
            OrderRequest(Short, "ManuallyTrade_Short2", Short2Price, Short2SLPrice, Short2TPPrice, Short2RiskPct),
            OrderRequest(Short, "ManuallyTrade_Short3", Short3Price, Short3SLPrice, Short3TPPrice, Short3RiskPct),
            OrderRequest(Long, "ManuallyTrade_Long1", Long1Price, Long1SLPrice, Long1TPPrice, Long1RiskPct),
            OrderRequest(Long, "ManuallyTrade_Long2", Long2Price, Long2SLPrice, Long2TPPrice, Long2RiskPct),
            OrderRequest(Long, "ManuallyTrade_Long3", Long3Price, Long3SLPrice, Long3TPPrice, Long3RiskPct)
        };
    }

    private static PendingOrderRequestModel OrderRequest(PdhpdlTradeDirectionModel direction, string label, double entryPrice,
        double stopLossPrice, double takeProfitPrice, double riskPct) {
        return new PendingOrderRequestModel {
            Label = label,
            Direction = direction,
            EntryPrice = entryPrice,
            StopLossPrice = stopLossPrice,
            TakeProfitPrice = takeProfitPrice,
            RiskPct = riskPct
        };
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
    }
}
