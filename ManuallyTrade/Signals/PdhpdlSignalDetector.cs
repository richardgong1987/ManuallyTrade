using cAlgo.API;

namespace cAlgo.Robots;

// Reads the last fully closed bar and assembles the signal model, then hands the candle-pattern
// decision to MainBiz.
// OnBar fires when a new bar opens, so the closed bar is Count - 2.
public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;

    public PdhpdlSignalDetector(Bars chartBars) {
        _chartBars = chartBars;
    }

    public PdhpdlSignalModel DetectOnClosedBar() {
        PdhpdlSignalModel signalModel = new();
        if (_chartBars.Count < 2)
            return signalModel;

        int closedBarIndex = _chartBars.Count - 2; // last fully closed bar in OnBar()
        CandleModel current = ReadCandle(closedBarIndex);
        CandleModel previous = ReadCandle(closedBarIndex - 1);
        CandleModel earlier = ReadCandle(closedBarIndex - 2);

        signalModel.HasData = true;
        signalModel.BarIndex = closedBarIndex;
        signalModel.BarTime = _chartBars.OpenTimes[closedBarIndex];
        signalModel.Open = current.Open;
        signalModel.Close = current.Close;
        signalModel.High = current.High;
        signalModel.Low = current.Low;

        MainBiz.Evaluate(signalModel, current, previous, earlier);

        return signalModel;
    }


    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }
}
