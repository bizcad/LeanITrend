using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp 
{
    public class KellyReturns : WindowIndicator<IndicatorDataPoint>
    {

        public Symbol symbol { get; set; }
        public decimal AveWin { get; set; }
        public decimal AveLoss { get; set; }
        public decimal SdWin { get; set; }
        public decimal SdLoss { get; set; }
        public decimal PWin { get; set; }
        public decimal PLoss { get; set; }
        public RollingWindow<decimal> Returns { get; set; }

        /// <summary>
        ///     Initializes a new instance of the KellyReturns class with the specified name and period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the KellyReturns</param>
        /// <param name="symbol">The symbol</param>
        public KellyReturns(string name, int period, Symbol sy) : base(name, period)
        {
            this.symbol = sy;
        }
        /// <summary>
        /// Initializes a new instance of the KellyReturns class with the defalut name and specified period
        /// </summary>
        /// <param name="period"></param>
        /// <param name="sy"></param>
        public KellyReturns(int period, Symbol sy) : this("KellyReturns" + period, period, sy)
        {
        }

        /// <summary>
        /// Initializes a new instance of the KellyReturns class with the default name and period
        /// </summary>
        /// <param name="sy">The symbol for the KellyReturns</param>
        public KellyReturns(Symbol sy) : this("Kelly50", 50, sy)
        {
        }
        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">the latest return for this Symbol as a result of a days trades</param>
        /// <returns></returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            Returns.Add(input);
            return input.Value;

        }
    }

}
