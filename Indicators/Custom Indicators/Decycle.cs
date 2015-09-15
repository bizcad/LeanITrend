using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Fast trend.
    /// Low-pass filter with cutoff frequency equal to period.
    /// --> Ref: Cycle Analytics, Code Listing 4-1.
    /// </summary>
    public class Decycle : WindowIndicator<IndicatorDataPoint>
    {
        private int _period;
        // The alpha for the formula
        private decimal _alpha;
        // Used as an one-observation RollingWindow.
        private IndicatorDataPoint _decycle;

        /// <summary>
        /// Gets or sets the adaptative period and update the alpha correspondent value
        /// </summary>
        /// <value>
        /// The adaptative period.
        /// </value>
        public int AdaptativePeriod
        {
            get { return _period; }
            set 
            { 
                _period = value;
                _alpha = (decimal)((Math.Cos(2 * Math.PI / (double)_period) + Math.Sin(2 * Math.PI / (double)_period) - 1) /
                    Math.Cos(2 * Math.PI / (double)_period));
            }
        }

        public Decycle(string name, int Period)
            : base(name, Period)
        {
            // Decycle history
            _decycle = new IndicatorDataPoint();
            this.AdaptativePeriod = Period;
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
        /// Calculates the next value for the decycle
        /// </summary>
        /// <param name="window">the window for this indicator</param>
        /// <param name="input">the latest price to input into the trend</param>
        /// <returns>the computed value</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // for convenience
            DateTime time = input.Time;


            if (window.Count < 3)
            {
                _decycle = idp(time, input.Value);
            }
            else
            {                
                decimal decycle = _alpha / 2 * (window[0] + window[1]) + (1 - _alpha) * _decycle.Value;
                _decycle = idp(time, decycle);
            }
            return _decycle;
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
