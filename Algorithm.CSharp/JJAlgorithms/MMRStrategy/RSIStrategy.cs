using QuantConnect.Indicators;
using System;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Mean reversion strategy:
    ///     - Goes short when RSI is greater than 50 plus some threshold
    ///     - Goes longs when RSI is lower than 50 minus some threshold
    /// </summary>
    public class RSIStrategy : BaseStrategy
    {
        private decimal _threshold;

        // Indicator with the prices to be injected from the main algorithm.
        private Indicator _price;

        // You are the start tonight.
        private RelativeStrengthIndex rsi;

        // Rolling window to store the RSI.
        private RollingWindow<decimal> rsiRW = new RollingWindow<decimal>(2);

        private decimal _tolerance = 0.0001m;

        /// <summary>
        /// Initializes a new instance of the <see cref="CrossEMAStrategy"/> class.
        /// </summary>
        /// <param name="Price">The injected price indicator.</param>
        /// <param name="SlowEMAPeriod">The slow EMA period.</param>
        /// <param name="FastEMAPeriod">The fast EMA period.</param>
        public RSIStrategy(Indicator Price, int RSIPeriod = 2, decimal Threshold = 40)
        {
            // Initialize fields.
            _threshold = Threshold;
            _price = Price;
            ActualSignal = OrderSignal.doNothing;
            Position = StockState.noInvested;
            EntryPrice = null;

            rsi = new RelativeStrengthIndex(RSIPeriod).Of(_price);

            // Fill the RSI rolling windows at every new RSI update. Once the
            // rolling windows is ready, at every indicator update the CheckSignal method will be called.
            rsi.Updated += (object sender, IndicatorDataPoint updated) =>
                    {
                        if (rsi.IsReady) rsiRW.Add(rsi);
                        if (rsiRW.IsReady) CheckSignal();
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
            get { return rsiRW.IsReady; }
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            rsi.Reset();
            rsiRW.Reset();
        }

        /// <summary>
        /// Checks the for signals.
        /// </summary>
        public override void CheckSignal()
        {
            OrderSignal actualSignal = OrderSignal.doNothing;

            // Defining the signals.
            bool longSignal = rsiRW[1] > 50 - _threshold &&
                              rsiRW[0] < 50 - _threshold &&
                              Math.Abs(rsiRW[0] - rsiRW[1]) > _tolerance;

            bool shortSignal = rsiRW[1] < 50 + _threshold &&
                               rsiRW[0] > 50 + _threshold &&
                               Math.Abs(rsiRW[0] - rsiRW[1]) > _tolerance;

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