using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using QuantConnect.Statistics;

namespace QuantConnect.Indicators
{
    public class WilliamsVixFixIndicator : IndicatorBase<TradeBar>
    {
        private int _period = 22;
        private Maximum _highest;
        public IndicatorDataPoint Current { get; set; }


        /// <summary>
        ///     Initializes a new instance of the WilliamsVixFixIndicator class with the specified name and period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the WVF</param>
        public WilliamsVixFixIndicator(string name, int period)
            : base(name)
        {
            _period = period;
            _highest = new Maximum(period);
        }
        /// <summary>
        ///     Initializes a new instance of the WilliamsVixFixIndicator class with the default name and period
        /// </summary>
        /// <param name="period">The period of the WVF</param>
        public WilliamsVixFixIndicator(int period)
            : this("WVF" + period, period)
        {
        }
        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady
        {
            get { return Samples >= _period; }
        }

        protected override decimal ComputeNextValue(TradeBar input)
        {
            _highest.Update(new IndicatorDataPoint(input.EndTime, input.Close));
            if (_highest.IsReady)
            {
                Current = new IndicatorDataPoint(input.EndTime,
                    ((_highest.Current.Value - input.Close) / _highest.Current.Value) * 100);
            }
            else
            {
                Current = new IndicatorDataPoint(input.EndTime, _highest.Current.Value);
                //Current = new IndicatorDataPoint(input.EndTime,((_highest.Current.Value - input.Close) / _highest.Current.Value) * 100);
            }
            return Current.Value;
        }
    }
}
