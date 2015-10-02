using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
        public interface ISig
    {
        //bool orderFilled { get; set; }
        //decimal nTrig { get; set; }
        //decimal nEntryPrice { get; set; }
        //int Barcount { get; set; }
        //bool maketrade { get; set; }
        //Decimal[] trendArray { get; set; }
        //int IsShort { get; set; }
        //bool IsLong { get; set; }

        OrderSignal CheckSignal(TradeBars data, IndicatorDataPoint trendCurrent, out string current);
        OrderSignal CheckSignal(TradeBars data, Dictionary<string,string> paramlist, out string current);
        void Reset();
        int GetId();
        //void SetTradesize(int size);

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
        /// <summary>
        /// The entry price from the last trade
        /// </summary>
        //int xOver { get; set; }
        /// <summary>
        /// The trigger use in the decision process
        /// </summary>

        Boolean orderFilled { get; set; }
        /// <summary>
        /// A flag to disable the trading.  True means make the trade.  This is left over from the 
        /// InstantTrendStrategy where the trade was being made in the strategy.  
        /// </summary>
        //Boolean maketrade { get; set; }
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


        //bool BarcountLT4 { get; set; }
        //bool NTrigLTEP { get; set; }
        //bool NTrigGTEP { get; set; }
        //bool NTrigGTTA0 { get; set; }
        //bool NTrigLTTA0 { get; set; }
        //bool ReverseTrade { get; set; }
        //bool xOverIsPositive { get; set; }
        //bool xOverisNegative { get; set; }
        //bool OrderFilled { get; set; }

    }
}
