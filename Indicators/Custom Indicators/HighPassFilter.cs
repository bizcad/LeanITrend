using System;

namespace QuantConnect.Indicators
{

    /// <summary>
    /// Fast Trend.
    /// A low-pass filter with almost flat frequency response in the  passband. This filter has a 
    /// similar SMA smoothing with less lag.
    /// --> Ref: Cycle Analytics, eq 3-3
    /// </summary>
    public class HighPassFilter : WindowIndicator<IndicatorDataPoint>
    {
        # region Fields
        private decimal _alpha;
        private decimal _a;
        private decimal _b;
        private int _period;

        private readonly RollingWindow<IndicatorDataPoint> _hpfWindow;

        public int AdaptativePeriod
        {
            get { return _period; }
            set
            {
                if (value < 3)
                {
                    throw new ArgumentException("HighPassFilter must have _period of at least 3.", "period");
                }
                _period = value;
                double arg = 2d * Math.PI / (double)_period;
                _alpha = (decimal)((Math.Cos(arg) + Math.Sin(arg) - 1d) / Math.Cos(arg));
                _a = (1m - _alpha / 2m) * (1m - _alpha / 2m);
                _b = 1m - _alpha;
            }
        }
        # endregion

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="period"></param>
        public HighPassFilter(string name, int period)
            : base(name, period)
        {
            this.AdaptativePeriod = period;
            
            // HighPassFilter history
             _hpfWindow = new RollingWindow<IndicatorDataPoint>(2);
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>

        public HighPassFilter(int period)
            : this("HPF" + period, period)
        {
        }


        /// <summary>
        ///     Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _hpfWindow.Reset();
        }

        /// <summary>
        /// Calculates the next value for the HighPassFilter.
        /// </summary>
        /// <param name="window">the window for this indicator</param>
        /// <param name="input">the latest price to input into the trend</param>
        /// <returns>the computed value</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            decimal hpf;
            // for convenience
            DateTime time = input.Time;

            if (window.Count < 4)
            {
                hpf = 0m;
            }
            else
            {
                hpf = _a * (window[0] - 2m * window[1] + window[2]) + 2m * _b * _hpfWindow[0] - (_b * _b) * _hpfWindow[1];
            }
            
            _hpfWindow.Add(idp(time, hpf));
            return _hpfWindow[0].Value;
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
