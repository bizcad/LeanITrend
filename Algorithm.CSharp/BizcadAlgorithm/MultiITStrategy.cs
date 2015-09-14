using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// From Ehlers Cybernetics page 27 on Trading the trend
    /// This class is for use in an array of strategies. 
    /// It only returns the OrderSignal (goLong, goShort, reverseToShort, reverseToLong, doNothing)
    /// </summary>
    public class MultiITStrategy
    {
        /// <summary>
        /// The entry price for the latest trade
        /// </summary>
        public decimal nEntryPrice { get; set; }
        public int Barcount { get; set; }
        public string sTrig { get; set; }

        private bool bReverseTrade = false;
        private string _symbol { get; set; }
        private decimal RevPct = 1.0015m;
        private decimal RngFac = .35m;
        private decimal nLimitPrice = 0;
        private int nStatus = 0;
        private int xOver = 0;
        public RollingWindow<IndicatorDataPoint> trendHistory { get; set; }
        

        /// <summary>
        /// Flag to determine if the algo should go flat overnight.
        /// </summary>
        public bool ShouldSellOutAtEod;

        /// <summary>
        /// the Algorithm being run.
        /// </summary>
        private QCAlgorithm _algorithm;

        /// <summary>
        /// The flag as to whether the order has been filled.
        /// </summary>
        public Boolean orderFilled { get; set; }

        public StockState Position { get; set; }

        //public RollingWindow<IndicatorDataPoint> _trendHistory;

        //public InstantTrendStrategy() { }
        /// <summary>
        /// Empty Consturctor
        /// </summary>
        /// <summary>
        /// Constructor initializes the symbol and period of the RollingWindow
        /// </summary>
        /// <param name="symbol">string - ticker symbol</param>
        /// <param name="algorithm"></param>
        /// <param name="trendHistory"></param>
        public MultiITStrategy(string symbol, int period, QCAlgorithm algorithm)
        {
            _symbol = symbol;
            trendHistory = new RollingWindow<IndicatorDataPoint>(period);
            _algorithm = algorithm;
            orderFilled = true;
        }


        //public OrderSignal CheckSignal(TradeBars data, int tradesize, IndicatorDataPoint trendCurrent, out string current)
        //{
        //    return CheckSignal(data, tradesize, out current, TODO);
        //}
        /// <summary>
        /// Executes the Instant Trend strategy
        /// </summary>
        /// <param name="data">TradeBars - the current OnData</param>
        /// <param name="data">TradeBars - the current OnData</param>
        /// <param name="tradesize"></param>
        /// <param name="tradesize"></param>
        /// <param name="current"></param>
        /// <param name="current"></param>
        /// <param name="xOver1"></param>
        /// <param name="trendCurrent">IndicatorDataPoint - the current trend value trend</param>
        /// <summary>
        /// Executes the Instant Trend strategy
        /// </summary>

        public OrderSignal CheckSignal(TradeBars data, int tradesize, IndicatorDataPoint trendCurrent, out string current)
        {
            OrderTicket ticket;
            int orderId = 0;
            string comment = string.Empty;
            OrderSignal retval = OrderSignal.doNothing;


            trendHistory.Add(trendCurrent);
            nStatus = 0;

            if (_algorithm.Portfolio[_symbol].IsLong) nStatus = 1;
            if (_algorithm.Portfolio[_symbol].IsShort) nStatus = -1;
            if (!trendHistory.IsReady)
            {
                current = "Trend Not Ready";
                return OrderSignal.doNothing;
            }

            
            #region "Strategy Execution"


            bReverseTrade = false;
            try
            {
                var nTrig = 2 * trendHistory[0].Value - trendHistory[2].Value;
                if (nStatus == 1 && nTrig < (nEntryPrice / RevPct))
                {
                    comment = string.Format("Long Reverse to short. Close < {0} / {1}", nEntryPrice, RevPct);
                    bReverseTrade = true;
                    retval = OrderSignal.revertToShort;
                }
                else
                {
                    if (nStatus == -1 && nTrig > (nEntryPrice * RevPct))
                    {
                        comment = string.Format("Short Reverse to Long. Close > {0} * {1}", nEntryPrice, RevPct);
                        bReverseTrade = true;
                        retval = OrderSignal.revertToLong;
                    }
                }
                if (!bReverseTrade)
                {
                    if (nTrig > trendHistory[0].Value)
                    {
                        if (xOver == -1 && nStatus != 1)
                        {
                            if (!orderFilled)
                            {
                                retval = OrderSignal.goLong;
                                comment = string.Format("{0} after order not filled", retval);
                            }
                            else
                            {
                                nLimitPrice = Math.Round(Math.Max(data[_symbol].Low, (data[_symbol].Close - (data[_symbol].High - data[_symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);
                                retval = OrderSignal.goLongLimit;
                                comment = string.Format("{0} nTrig > history[0] xOver {1} Limit Price {2}",retval, xOver, nLimitPrice);
                            }
                        }
                        if (comment.Length == 0)
                            comment = "Trigger over Trend";
                        xOver = 1;
                    }
                    else
                    {
                        if (nTrig < trendHistory[0].Value)
                        {
                            if (xOver == 1 && nStatus != -1)
                            {
                                if (!orderFilled)
                                {
                                    retval = OrderSignal.goShort;
                                    comment = string.Format("{0} after order not filled", retval);
                                }
                                else
                                {
                                    nLimitPrice = Math.Round(Math.Min(data[_symbol].High, (data[_symbol].Close + (data[_symbol].High - data[_symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);
                                    retval = OrderSignal.goShortLimit;
                                    comment = string.Format("{0} nTrig < history[0] xOver = {1} Limit Price {2}", retval, xOver, nLimitPrice);
                                }
                            }
                            if (comment.Length == 0)
                                comment = "Trigger under trend";
                            xOver = -1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            #endregion
        
            current = comment;
            return retval;
        }
        public void Reset()
        {
            trendHistory.Reset();
            Barcount = 0;
        }

        public void UpdateTrendHistory(IndicatorDataPoint datapoint)
        {
            trendHistory.Add(datapoint);
        }
    }
}
