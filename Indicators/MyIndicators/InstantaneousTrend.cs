using System;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Ref: Cybernetics, eq 2.9
    /// </summary>
    public class InstantaneousTrend : WindowIndicator<IndicatorDataPoint>
    {
        # region Fields
        private decimal _alpha;
        private decimal _a;
        private decimal _b;
        private readonly int _period;
        private int barcount;

        private readonly RollingWindow<IndicatorDataPoint> _iTrendWindow;
        # endregion

        /// <summary>
        ///
        /// </summary>
        /// <param name="name"></param>
        /// <param name="period"></param>
        public InstantaneousTrend(string name, int period)
            : base(name, period)
        {
            if (period < 3)
            {
                throw new ArgumentException("InstantaneousTrend must have period of at least 3.", "period");
            }
            _period = period;
            // InstantaneousTrend history
            _iTrendWindow = new RollingWindow<IndicatorDataPoint>(2);
            _alpha = 2.0m / ((decimal)_period + 1.0m);
            _a = (_alpha / 2) * (_alpha / 2);
            _b = (1 - _alpha);
            barcount = 0;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>

        public InstantaneousTrend(int period)
            : this("ITrend" + period, period)
        {
        }


        /// <summary>
        ///     Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _iTrendWindow.Reset();
            barcount = 0;
        }

        /// <summary>
        /// Calculates the next value for the ITrend
        /// </summary>
        /// <param name="window">the window for this indicator</param>
        /// <param name="input">the latest price to input into the trend</param>
        /// <returns>the computed value</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            decimal iTrend;
            // for convenience
            DateTime time = input.Time;

            if (barcount < 2)
            {
                iTrend = input.Value;
            }
            else if (barcount >= 2 && barcount < 7)
            {
                iTrend = (window[0] + 2 * window[1] + window[2]) / 4;
            }
            else
            {
                iTrend = (_alpha - _a) * window[0] + (2 * _a) * window[1] - (_alpha - 3 * _a) * window[2]
                    + (2 * _b) * _iTrendWindow[0] - (_b * _b) * _iTrendWindow[1];
            }
            _iTrendWindow.Add(idp(time, iTrend));
            barcount++;
            return _iTrendWindow[0].Value;
        }

        /// <summary>
        /// Factory function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from
        /// new IndicatorDataPoint(data.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }
    }
}
