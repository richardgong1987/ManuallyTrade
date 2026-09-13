namespace cAlgo.Robots;

// The subset of cTrader symbol facts the planner needs, expressed without any
// cAlgo.API type. CAlgoSymbolModel adapts the real Symbol; tests supply a fake.
public interface IPdhpdlSymbolModel {
    double PipSize { get; }
    double LotSize { get; }
    double VolumeInUnitsMin { get; }
    double VolumeInUnitsMax { get; }

    // Monetary value of one pip for one unit, in the account's deposit currency. This is
    // what makes sizing currency-correct: for a EUR account trading USD-quoted XAUUSD it
    // already folds in the USD->EUR conversion, which a raw price distance does not.
    double PipValue { get; }

    // Snap a raw volume down to a tradable step, so the loss at the stop never exceeds the
    // risk budget.
    double NormalizeVolumeInUnits(double volumeInUnits);
    double AmountRisked(double volumeInUnits, double stopLossPips);
}
