using QuantConnect.Indicators;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Classic moving average cross strategy.
    /// </summary>
    public class CrossEMAStrategy : BaseStrategy
    {
        // Indicator with the prices to be injected from the main algorithm.
        private Indicator _price;

        private ExponentialMovingAverage fastEMA;
        private ExponentialMovingAverage slowEMA;

        // Rolling window to store the difference between fast and slow EMA.
        private RollingWindow<decimal> EMADiffRW = new RollingWindow<decimal>(2);

        private decimal _tolerance = 0.0001m;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrossEMAStrategy"/> class.
        /// </summary>
        /// <param name="Price">The injected price indicator.</param>
        /// <param name="SlowEMAPeriod">The slow EMA period.</param>
        /// <param name="FastEMAPeriod">The fast EMA period.</param>
        public CrossEMAStrategy(Indicator Price, int SlowEMAPeriod = 45, int FastEMAPeriod = 120)
        {
            // Initialize fields.
            _price = Price;
            fastEMA = new ExponentialMovingAverage(FastEMAPeriod).Of(_price);
            slowEMA = new ExponentialMovingAverage(SlowEMAPeriod).Of(_price);

            ActualSignal = OrderSignal.doNothing;
            Position = StockState.noInvested;
            EntryPrice = null;

            // Fill the EMA difference rolling windows at every new slowEMA observation. Once the
            // rolling windows is ready, at every indicator update the CheckSignal method will be called.
            slowEMA.Updated += (object sender, IndicatorDataPoint updated) =>
                    {
                        if (slowEMA.IsReady) EMADiffRW.Add(fastEMA - slowEMA);
                        if (EMADiffRW.IsReady) CheckSignal();
                    };
        }

        /// <summary>
        /// Gets a value indicating whether this instance is ready.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is ready; otherwise, <c>false</c>.
        /// </value>
        public bool IsReady
        {
            get { return EMADiffRW.IsReady; }
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            fastEMA.Reset();
            slowEMA.Reset();
            EMADiffRW.Reset();
        }

        /// <summary>
        /// Checks the for signals.
        /// </summary>
        public override void CheckSignal()
        {
            OrderSignal actualSignal = OrderSignal.doNothing;

            // Defining the signals.
            bool longSignal = EMADiffRW[1] < 0 &&
                              EMADiffRW[0] > 0 &&
                              Math.Abs(EMADiffRW[0] - EMADiffRW[1]) > _tolerance;

            bool shortSignal = EMADiffRW[1] > 0 &&
                               EMADiffRW[0] < 0 &&
                               Math.Abs(EMADiffRW[0] - EMADiffRW[1]) > _tolerance;

            // Depending on the actual strategy position, define the signal.
            switch (Position)
            {
                case StockState.shortPosition:
                    if (longSignal) actualSignal = OrderSignal.closeShort;
                    break;

                case StockState.longPosition:
                    if (shortSignal) actualSignal = OrderSignal.closeLong;
                    break;

                case StockState.noInvested:
                    if (longSignal) actualSignal = OrderSignal.goLong;
                    else if (shortSignal) actualSignal = OrderSignal.goShort;
                    break;

                default:
                    break;
            }
            // Update the ActualSignal field.
            ActualSignal = actualSignal;
        }
    }
}