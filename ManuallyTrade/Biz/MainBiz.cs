using System;

namespace cAlgo.Robots;

public class MainBiz {
    public static void Evaluate(PdhpdlSignalModel signalModel, CandleModel current, CandleModel previous, CandleModel earlier) {
        HanJinSignalScanModel scanResult = HanJinSignals26.Scan(current, previous, earlier);
        signalModel.IsLongSignal = MatchesLongPattern(signalModel, scanResult, current, previous);
        signalModel.IsShortSignal = MatchesShortPattern(signalModel, scanResult, current, previous);
    }

    private static bool MatchesShortPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (ShortPinBar(signalModel, scanResult, current)) {
            return true;
        }

        if (ShortEngulf(signalModel, scanResult, current)) {
            return true;
        }

        if (ShortTop(signalModel, scanResult, current, previous)) {
            return true;
        }

        if (ShortHarami(signalModel, scanResult, current, previous)) {
            return true;
        }

        return false;
    }

    private static bool MatchesLongPattern(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (LongPinbar(signalModel, scanResult, current)) {
            return true;
        }

        if (LongEngulf(signalModel, scanResult, current)) {
            return true;
        }

        if (LongBottom(signalModel, scanResult, current, previous)) {
            return true;
        }

        if (LongHarami(signalModel, scanResult, current, previous)) {
            return true;
        }

        return false;
    }

    private static bool ShortTop(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current, CandleModel previous) {
        if (scanResult.FractalTop != SignalSideModel.Sell || !Utils.AnyBarIsShort(current))
            return false;

        signalModel.Label = "S_Top";
        signalModel.SL = previous.High;
        return true;
    }

    private static bool ShortHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.HaramiSingle != SignalSideModel.Sell)
            return false;

        signalModel.Label = "S_Harami";
        signalModel.SL = Math.Max(previous.High, current.High);
        return true;
    }

    private static bool ShortEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Engulf != SignalSideModel.Sell)
            return false;

        signalModel.Label = "S_Eng";
        signalModel.SL = current.High;
        return true;
    }

    private static bool ShortPinBar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar != SignalSideModel.Sell)
            return false;

        signalModel.Label = "S_Pin";
        signalModel.SL = current.High;
        return true;
    }

    private static bool LongBottom(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.FractalBottom != SignalSideModel.Buy || !Utils.AnyBarIsLong(current))
            return false;

        signalModel.Label = "L_Bot";
        signalModel.SL = previous.Low;
        return true;
    }

    private static bool LongHarami(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current,
        CandleModel previous) {
        if (scanResult.HaramiSingle != SignalSideModel.Buy)
            return false;

        signalModel.Label = "L_Harami";
        signalModel.SL = Math.Min(previous.Low, current.Low);
        return true;
    }

    private static bool LongEngulf(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Engulf != SignalSideModel.Buy)
            return false;

        signalModel.Label = "L_Eng";
        signalModel.SL = current.Low;
        return true;
    }

    private static bool LongPinbar(PdhpdlSignalModel signalModel, HanJinSignalScanModel scanResult, CandleModel current) {
        if (scanResult.Pinbar != SignalSideModel.Buy)
            return false;

        signalModel.Label = "L_Pin";
        signalModel.SL = current.Low;
        return true;
    }
}
