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
        private readonly decimal a = 0.1m;
        private readonly int _period;
        private readonly RollingWindow<IndicatorDataPoint> _trend;
        private readonly RollingWindow<IndicatorDataPoint> _price;
        private int barcount;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="period"></param>
        public InstantaneousTrend(string name, int period, decimal alpha)
            : base(name, period)
        {

            // InstantaneousTrend history
            _trend = new RollingWindow<IndicatorDataPoint>(period);
            _price = new RollingWindow<IndicatorDataPoint>(period);
            _period = period;
            a = alpha;
            barcount = 0;
        }
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>
        public InstantaneousTrend(int period, decimal alpha = .05m)
            : this("CCy" + period, period, alpha)
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
                var lfp = (a - ((a / 2) * (a / 2))) * input.Value + ((a * a) / 2) * _price[1].Value
                     - (a - (3 * (a * a) / 4)) * _price[2].Value + 2 * (1 - a) * _trend[0].Value
                     - ((1 - a) * (1 - a)) * _trend[1].Value;
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
