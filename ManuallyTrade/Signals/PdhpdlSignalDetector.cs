using System;
using cAlgo.API;

namespace cAlgo.Robots;

public class PdhpdlSignalDetector {
    private readonly Bars _chartBars;
    private readonly Bars _dailyBars;
    private readonly DualRmaSeries _rmaSeries;
    private readonly GapXSeries _gapXSeries;

    public PdhpdlSignalDetector(Bars chartBars, Bars dailyBars, DualRmaSeries rmaSeries, GapXSeries gapXSeries) {
        _chartBars = chartBars;
        _dailyBars = dailyBars;
        _rmaSeries = rmaSeries;
        _gapXSeries = gapXSeries;
    }

    public PdhpdlSignalModel DetectOnClosedBar(StrategyModel strategy) {
        PdhpdlSignalModel signalModel = new();
        signalModel.Strategy = strategy;

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

        FillRmaData(signalModel);

        // GapX 是进场条件之一，必须在 Evaluate 之前就位。
        _gapXSeries.Fill(signalModel);

        MainBiz.Evaluate(signalModel, current, previous, earlier);

        return signalModel;
    }

    private CandleModel ReadCandle(int index) {
        return new CandleModel(open: _chartBars.OpenPrices[index], high: _chartBars.HighPrices[index], low: _chartBars.LowPrices[index],
            close: _chartBars.ClosePrices[index]);
    }

    private void FillRmaData(PdhpdlSignalModel signalModel) {
        signalModel.FastRma = double.NaN;
        signalModel.SlowRma = double.NaN;

        if (!_rmaSeries.TryGetLastConfirmedValues(out DateTime sourceBarTime, out double fastRma, out double slowRma))
            return;

        signalModel.HasRmaData = true;
        signalModel.RmaSourceBarTime = sourceBarTime;
        signalModel.FastRma = fastRma;
        signalModel.SlowRma = slowRma;
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
