using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly Bars _dailyBars;

    public PdhpdlSignalDetector(Bars chartBars, Bars dailyBars) {
        _chartBars = chartBars;
        _dailyBars = dailyBars;
    }

    public PdhpdlSignalModel DetectOnClosedBar() {
        PdhpdlSignalModel signalModel = new();

        if (_chartBars.Count < 2 || !TryGetPreviousDayLevels(out double pdh, out double pdl))
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

        signalModel.Pdh1 = pdh;

        signalModel.Pdl1 = pdl;

        MainBiz.Evaluate(signalModel, current, previous, earlier);

        return signalModel;
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }

    private int PreviousDailyIndex => _dailyBars.Count - 2;

    private bool TryGetPreviousDayLevels(out double pdh, out double pdl) {
        pdh = double.NaN;
        pdl = double.NaN;

        if (_dailyBars == null || _dailyBars.Count < 2)
            return false;

        pdh = _dailyBars.HighPrices[PreviousDailyIndex];
        pdl = _dailyBars.LowPrices[PreviousDailyIndex];
        return true;
    }
}
