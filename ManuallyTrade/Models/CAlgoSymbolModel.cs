using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots;

// Adapts the real cTrader Symbol to IPdhpdlSymbolModel. Volume is rounded down to a tradable
// step so a stop-out never loses more than the risk budget.
public class CAlgoSymbolModel : IPdhpdlSymbolModel {
    private readonly Symbol _symbol;

    public CAlgoSymbolModel(Symbol symbol) {
        _symbol = symbol;
    }

    public double PipSize => _symbol.PipSize;
    public double LotSize => _symbol.LotSize;
    public double VolumeInUnitsMin => _symbol.VolumeInUnitsMin;
    public double VolumeInUnitsMax => _symbol.VolumeInUnitsMax;
    public double PipValue => _symbol.PipValue;

    public double NormalizeVolumeInUnits(double volumeInUnits) {
        return _symbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
    }

    public double AmountRisked(double volumeInUnits, double stopLossPips) {
        return _symbol.AmountRisked(volumeInUnits, stopLossPips);
    }
}
