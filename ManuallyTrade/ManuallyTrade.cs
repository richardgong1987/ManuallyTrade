using System.Diagnostics;
using cAlgo.API;

namespace cAlgo.Robots;

[Robot(TimeZone = TimeZones.TokyoStandardTime, AccessRights = AccessRights.None, AddIndicators = false)]
public class ManuallyTrade : Robot {
    private const string Short1Label = "ManuallyTrade_Short1";

    [Parameter("风险1%", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1RiskPct { get; set; }

    [Parameter("空1入场价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1Price { get; set; }

    [Parameter("空1止盈价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1TPPrice { get; set; }

    [Parameter("空1止损价", DefaultValue = 0, MinValue = 0, Group = "空1")]
    public double Short1SLPrice { get; set; }

    [Parameter("debug调试", DefaultValue = false, Group = "开发调试")]
    public bool IsDebug { get; set; }

    protected override void OnStart() {
        LaunchDebug();

        var orderExecutor = new PdhpdlOrderExecutor(this, new PdhpdlOrderPlanner(new CAlgoSymbolModel(Symbol)));
        PlaceShort1OrderIfConfigured(orderExecutor);

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

    // Entry and take-profit both set means "place Short1". A missing stop-loss or risk percentage
    // is rejected by the planner with a logged reason rather than silently skipped.
    private void PlaceShort1OrderIfConfigured(PdhpdlOrderExecutor orderExecutor) {
        if (Short1Price <= 0.0 || Short1TPPrice <= 0.0)
            return;

        orderExecutor.PlacePendingOrder(new PendingOrderRequestModel {
            Label = Short1Label,
            Direction = PdhpdlTradeDirectionModel.Short,
            EntryPrice = Short1Price,
            StopLossPrice = Short1SLPrice,
            TakeProfitPrice = Short1TPPrice,
            RiskPct = Short1RiskPct
        });
    }

    protected override void OnStop() {
        Print("*****cBot stopped.*******************");
    }
}
