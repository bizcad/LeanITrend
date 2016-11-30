using System;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Computes Ehler's Cyber Cycle indicator based on his EasyLanguage code 
    /// in Cybernetic Analysis for Stocks and Futures.  Figure 4.2 Page 34.
    /// </summary>
    public class CyberCycle : WindowIndicator<IndicatorDataPoint>
    {
        // the alpha for the formula
        private readonly double a = 0.07;
        private readonly RollingWindow<double> _smooth;
        private readonly RollingWindow<double> _cycle;
        private readonly int _period;
        private int barcount;

        /// <summary>
        /// Instanciates the indicator and the two Rolling Windows
        /// </summary>
        /// <param name="name">string - a custom name for your indicator</param>
        /// <param name="period">int - the number of bar in the RollingWindow histories</param>
        /// <remarks>Ehlers only uses the last 4 bars of the history, but he maintains a list
        /// of bars for 7 bars on both indicators.  I recommend a period of 7 and use IsReady to warm up your algo</remarks>
        public CyberCycle(string name, int period)
            : base(name, period)
        {
            // Creates the smoother data set to which the resulting cybercycle is applied
            _smooth = new RollingWindow<double>(period);
            // CyberCycle history
            _cycle = new RollingWindow<double>(period);
            _period = period;
            barcount = 0;
        }
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>
        public CyberCycle(int period)
            : this("CCy" + period, period)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        public override bool IsReady
        {
            get { return _smooth.IsReady; }
        }
        /// <summary>
        /// Computes the next value for the indicator
        /// 
        /// 
        /// </summary>
        /// <param name="window"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        /// <remarks>
        /// From Ehlers page 15 equation 2.7 and Easy Language code on page 34 figure 4.2
        /// His formula is changed slightly because he uses _cycle[1] in the second line of the high
        /// pass filter.  Easy Language creates the newest reading in the indicator before the formula is evaluated,
        ///    so that _cycle[1] refers to "yesterday" (in daily parlance) and _cycle is the newly created
        ///    reading with an implicit index of [0].  
        /// On the other had, Lean's Add method adds the result of the formula to the underlying list
        ///    after it has been evaluated, so that "yesterday's" value in the formula is _cycle[0] not _cycle[1] as Ehlers says.  
        ///After the Add _cycle[0] is the latest value and yesterday's value is _cycle[1]
        /// 
        /// _smooth, on the other hand, has already been added, so I can use _smooth[1] to refer to "yesterday's" value
        ///Following is the high pass filter used for the instanteous trend
        /// I create the intermediate hfp variable for clarity in the code.
        ///</remarks>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // for convenience
            var time = input.Time;

            if (barcount > 2)
            {
                _smooth.Add((double)(window[0].Value + 2 * window[1].Value + 2 * window[2].Value + window[3].Value / 6));

                if (barcount < _period)
                {
                    _cycle.Add((double)(window[0].Value - 2 * window[1].Value + window[2].Value) / 4);
                }
                else
                {
                    // Calc the high pass filter _cycle value
                    var hfp = (1 - 0.5 * a) * (1 - 0.5 * a) * (_smooth[0] - 2 * _smooth[1] + _smooth[2])
                             + 2 * (1 - a) * _cycle[0] - (1 - a) * (1 - a) * _cycle[1];
                    _cycle.Add(hfp);
                }
            }
            else
            {
                _smooth.Add((double)window[0].Value);
                _cycle.Add(0);
            }
            barcount++;
            return (decimal)_cycle[0];
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
