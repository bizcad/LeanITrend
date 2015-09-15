using System;

namespace QuantConnect.Indicators
{

    /// <summary>
    /// Fast Trend.
    /// A low-pass filter with almost flat frequency response in the  passband. This filter has a 
    /// similar SMA smoothing with less lag.
    /// --> Ref: Cycle Analytics, eq 3-3
    /// </summary>
    public class SuperSmoother : WindowIndicator<IndicatorDataPoint>
    {
        # region Fields
        private decimal _a;
        private decimal _b;
        private decimal _c1;
        private decimal _c2;
        private decimal _c3;
        private int _period;

        private readonly RollingWindow<IndicatorDataPoint> _sSmootherdWindow;

        public int AdaptativePeriod
        {
            get { return _period; }
            set
            {
                if (value < 3)
                {
                    throw new ArgumentException("SuperSmoother must have _period of at least 3.", "_period");
                }
                _period = value;
                _a = (decimal)Math.Exp(-1.414 * Math.PI / (double)_period);
                _b = 2m * _a * (decimal)Math.Cos(1.414 * Math.PI / (double)_period);
                _c2 = _b;
                _c3 = -(_a * _a);
                _c1 = 1m - _c2 - _c3;
            }
        }
        # endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="period"></param>
        public SuperSmoother(string name, int period)
            : base(name, period)
        {
            this.AdaptativePeriod = period;
            // SuperSmoother history
            _sSmootherdWindow = new RollingWindow<IndicatorDataPoint>(2);
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>

        public SuperSmoother(int period)
            : this("SSmoother" + period, period)
        {
        }


        /// <summary>
        ///     Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _sSmootherdWindow.Reset();
        }

        /// <summary>
        /// Calculates the next value for the SuperSmoother.
        /// </summary>
        /// <param name="window">the window for this indicator</param>
        /// <param name="input">the latest price to input into the trend</param>
        /// <returns>the computed value</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            decimal sSmoother;
            // for convenience
            DateTime time = input.Time;

            if (window.Count < 3)
            {
                sSmoother = input.Value;
            }
            else
            {
                sSmoother = _c1 / 2m * (window[0] + window[1]) + _c2 * _sSmootherdWindow[0] + _c3 * _sSmootherdWindow[1];
            }

            _sSmootherdWindow.Add(idp(time, sSmoother));
            return _sSmootherdWindow[0].Value;
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
