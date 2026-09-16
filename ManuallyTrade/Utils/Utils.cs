namespace cAlgo.Robots;

public class Utils {
    public static bool AnyBarTouchesLevel(double level, params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (TouchesLevel(candle, level))
                return true;
        }

        return false;
    }

    private static bool TouchesLevel(CandleModel candle, double level) {
        return candle.Low <= level && candle.High >= level;
    }

    // 上方一组关键价位：日线 PDH。
    public static PdhpdlKeyLevelModel[] PdhLevels(PdhpdlSignalModel signalModel) {
        return new[] { new PdhpdlKeyLevelModel("Pdl1", signalModel.Pdl1), new PdhpdlKeyLevelModel("Pdh1", signalModel.Pdh1), };
    }

    // 下方一组关键价位：日线 PDL。
    public static PdhpdlKeyLevelModel[] PdlLevels(PdhpdlSignalModel signalModel) {
        return new[] { new PdhpdlKeyLevelModel("Pdl1", signalModel.Pdl1), new PdhpdlKeyLevelModel("Pdh1", signalModel.Pdh1), };
    }

    // 看跌确认：K线接触到该价位，且收盘价低于该价位。返回第一个命中的价位名。
    public static bool TryFindSellKeyLevel(PdhpdlKeyLevelModel[] levels, double closePrice, CandleModel[] touchCandles,
        out string keyLevel) {
        foreach (PdhpdlKeyLevelModel level in levels) {
            if (level.IsConfigured && AnyBarTouchesLevel(level.Price, touchCandles) && closePrice < level.Price) {
                keyLevel = level.Name;
                return true;
            }
        }

        keyLevel = "";
        return false;
    }

    // 看涨确认：K线接触到该价位，且收盘价高于该价位。返回第一个命中的价位名。
    public static bool TryFindBuyKeyLevel(PdhpdlKeyLevelModel[] levels, double closePrice, CandleModel[] touchCandles,
        out string keyLevel) {
        foreach (PdhpdlKeyLevelModel level in levels) {
            if (level.IsConfigured && AnyBarTouchesLevel(level.Price, touchCandles) && closePrice > level.Price) {
                keyLevel = level.Name;
                return true;
            }
        }

        keyLevel = "";
        return false;
    }

    public static bool AnyBarIsLong(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (!candle.IsBullish) {
                return false;
            }
        }

        return true;
    }

    public static bool AnyBarIsShort(params CandleModel[] candles) {
        foreach (CandleModel candle in candles) {
            if (!candle.IsBearish) {
                return false;
            }
        }

        return true;
    }

}
