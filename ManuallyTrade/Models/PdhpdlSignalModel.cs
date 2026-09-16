using System;

namespace cAlgo.Robots;

public class PdhpdlSignalModel {
    public bool HasData { get; set; }

    public DateTime BarTime { get; set; }

    public int BarIndex { get; set; }

    public double High { get; set; }

    public double Low { get; set; }

    public double Close { get; set; }

    public double Open { get; set; }

    public double Pdh1 { get; set; }

    public double Pdl1 { get; set; }

    public bool HasRmaData { get; set; }

    public DateTime RmaSourceBarTime { get; set; }

    public double FastRma { get; set; }

    public double SlowRma { get; set; }

    public bool IsLongSignal { get; set; }

    public bool IsShortSignal { get; set; }

    public string Label { get; set; }

    public string KeyLevel { get; set; }

    public double SL { get; set; }

    public StrategyModel Strategy { get; set; }

    public bool IsBigK { get; set; }

    // 开口扩大 X（GapX）：快慢线开口在回看窗口里扩大了几个 ATR。多头视角，收窄为负。
    public double GapExpansionX3Bar { get; set; } = double.NaN;

    // 开口扩大闸门的设置，随信号一起传给 MainBiz（见 GapXGate）。
    public bool UseGapX { get; set; }
    public double GapXThreshold { get; set; }
}
