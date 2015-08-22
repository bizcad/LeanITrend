using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Indicators
{
    public class Decycle : WindowIndicator<IndicatorDataPoint>
    {
        // the alpha for the formula
        private decimal alpha;
        private readonly int _period;
        private readonly RollingWindow<IndicatorDataPoint> _price;
        private readonly RollingWindow<IndicatorDataPoint> _decycle;

        public Decycle(string name, int period)
            : base(name, period)
        {

            // Decycle history
            _price = new RollingWindow<IndicatorDataPoint>(2);
            _decycle = new RollingWindow<IndicatorDataPoint>(1);
            _period = period;
            alpha = (decimal)((Math.Cos(2 * Math.PI / period) + Math.Sin(2 * Math.PI / period) - 1) / Math.Cos(2 * Math.PI / period));
        }
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>
        public Decycle(int period)
            : this("Decycle" + period, period)
        {
        }

        /// <summary>
        ///     Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady
        {
            get { return _decycle.IsReady; }
        }

        /// <summary>
        /// Calculates the next value for the decycle
        /// </summary>
        /// <param name="window">the window for this indicator</param>
        /// <param name="input">the latest price to input into the trend</param>
        /// <returns>the computed value</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // for convenience
            DateTime time = input.Time;

            _price.Add(input);

            if (!_price.IsReady)
            {
                _decycle.Add(input);
            }
            else
            {                
                decimal decycle = alpha / 2 * (_price[0] + _price[1]) + (1 - alpha) * _decycle[1];
                _decycle.Add(idp(time, decycle));
            }

            return _decycle[0];
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
