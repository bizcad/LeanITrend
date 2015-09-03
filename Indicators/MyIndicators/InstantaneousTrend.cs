using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// 
    /// </summary>
    public class InstantaneousTrend : WindowIndicator<IndicatorDataPoint>
    {
        // the alpha for the formula
        private decimal _alpha;
        private decimal _a;
        private decimal _b;
        private readonly int _period;
        private readonly RollingWindow<IndicatorDataPoint> _trend;
        private readonly RollingWindow<IndicatorDataPoint> _price;
        private int barcount;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="period"></param>
        public InstantaneousTrend(string name, int period)
            : base(name, period)
        {

            // InstantaneousTrend history
            _trend = new RollingWindow<IndicatorDataPoint>(period);
            _price = new RollingWindow<IndicatorDataPoint>(period);
            _period = period;
            _alpha = 2 / (_period + 1);
            _a = (_alpha / 2) * (_alpha / 2);
            _b = (1 - _alpha);
            barcount = 0;
        }
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>
        public InstantaneousTrend(int period)
            : this("CCy" + period, period)
        {
        }

        /// <summary>
        ///     Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady
        {
            get { return _trend.IsReady; }
        }

        /// <summary>
        ///     Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _trend.Reset();
            _price.Reset();
        }

        /// <summary>
        /// Calculates the next value for the ITrend
        /// </summary>
        /// <param name="window">the window for this indicator</param>
        /// <param name="input">the latest price to input into the trend</param>
        /// <returns>the computed value</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // for convenience
            var time = input.Time;
            _price.Add(input);

            if (barcount < _period)
            {
                _trend.Add(input);
            }
            else
            {
                // Calc the low pass filter _trend value and add it to the _trend
                decimal lfp = (_alpha - _a) * _price[0] + 2 * _a * _price[1] - (_alpha - 3 * _a) * _price[2] 
                    + 2 * _b * _trend[1] - _b * _b * _trend[2];
                _trend.Add(idp(time, lfp));
            }

            barcount++;
            return _trend[0].Value;
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
