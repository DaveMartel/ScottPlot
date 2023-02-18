﻿namespace ScottPlotCookbook.Recipes.PlotTypes;

internal class Finance : RecipePageBase
{
    public override RecipePageDetails PageDetails => new()
    {
        Chapter = Chapter.PlotTypes,
        PageName = "Financial Plot",
        PageDescription = "Finance plots display price data binned into time ranges.",
    };

    internal class OHLC : RecipeTestBase
    {
        public override string Name => "OHLC Chart";
        public override string Description => "OHLC charts use symbols to display price data " +
            "(open, high, low, and close) for specific time ranges.";

        [Test]
        public override void Recipe()
        {
            ScottPlot.OHLC[] ohlcs = ScottPlot.Generate.OHLC.Random(30);
            DateTime[] dates = ScottPlot.Generate.DateTime.Weekdays(ohlcs.Length);
            double[] xs = dates.Select(x => x.ToOADate()).ToArray(); // TODO: there must be a better way
            myPlot.Add.OHLC(xs, ohlcs);
            myPlot.Axes.DateTimeTicks(Edge.Bottom);
        }
    }
}
