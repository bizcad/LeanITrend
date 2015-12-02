using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    public class WilliamsVixFix : WindowIndicator<TradeBar>
    {
        
        private readonly Maximum _wfvMaximum;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">The indicator name</param>
        /// <param name="period">The lookback period</param>
        public WilliamsVixFix(string name, int period = 22) : base(name, period)
        {
            _wfvMaximum = new Maximum(period);
        }
         /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods to look back for the maximum close</param>
        public WilliamsVixFix(int period = 22)
            : this("WVF" + period, period)
        {
        }

        /// <summary>
        ///     Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _wfvMaximum.Reset();
        }

        protected override decimal ComputeNextValue(IReadOnlyWindow<TradeBar> window, TradeBar input)
        {
            _wfvMaximum.Update(new IndicatorDataPoint(input.EndTime, input.Close));
            Current = new IndicatorDataPoint(input.EndTime,
                (_wfvMaximum.Current.Value - input.Low)/_wfvMaximum.Current.Value * 100m);
            return Current.Value;
        }
    }
}
