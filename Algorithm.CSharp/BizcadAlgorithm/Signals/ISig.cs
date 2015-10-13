using System;
using System.Collections.Generic;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    public interface ISig
    {
        OrderSignal CheckSignal(TradeBars data, IndicatorDataPoint trendCurrent, out string current);
        OrderSignal CheckSignal(TradeBars data, Dictionary<string, string> paramlist, out string current);
        OrderSignal CheckSignal(KeyValuePair<Symbol, TradeBar> data, IndicatorDataPoint trendCurrent, out string current);
        OrderSignal CheckSignal(KeyValuePair<Symbol, TradeBar> data, Dictionary<string, string> paramlist, out string current);
        void Reset();
        int GetId();
        /// <summary>
        /// The symbol being processed
        /// </summary>
        Symbol symbol { get; set; }

        /// <summary>
        /// The unique id assigned in the Constructor
        /// </summary>
        //int Id;
        /// <summary>
        /// The entry price from the last trade
        /// </summary>
        decimal nEntryPrice { get; set; }

        Boolean orderFilled { get; set; }
        /// <summary>
        /// The array used to keep track of the last n trend inputs
        /// </summary>
        decimal[] trendArray { get; set; }
        /// <summary>
        /// The bar count from the algorithm
        /// </summary>
        int Barcount { get; set; }
        /// <summary>
        /// The state of the portfolio.
        /// </summary>
        bool IsShort { get; set; }
        /// <summary>
        /// The state of the portfolio.
        /// </summary>
        bool IsLong { get; set; }
        decimal nTrig { get; set; }
    }
}
