using System;
using System.Diagnostics;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// From Ehlers Cybernetics page 27 on Trading the trend
    /// </summary>
    public class InstantTrendStrategyOriginal
    {
        /// <summary>
        /// The entry price for the latest trade
        /// </summary>
        public decimal nEntryPrice { get; set; }
        public int Barcount { get; set; }

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
        public QCAlgorithm _algorithm;

        /// <summary>
        /// The flag as to whether the order has been filled.
        /// </summary>
        public Boolean orderFilled { get; set; }
        public Boolean maketrade { get; set; }


        /// <summary>
        /// Empty Consturctor
        /// </summary>
        //public InstantTrendStrategy() { }

        /// <summary>
        /// Constructor initializes the symbol and period of the RollingWindow
        /// </summary>
        /// <param name="symbol">string - ticker symbol</param>
        /// <param name="period">int - the period of the Trend History Rolling Window</param>
        /// <param name="algorithm"></param>
        public InstantTrendStrategyOriginal(string symbol, int period, QCAlgorithm algorithm)
        {
            _symbol = symbol;
            trendHistory = new RollingWindow<IndicatorDataPoint>(period);
            _algorithm = algorithm;
            orderFilled = true;
            maketrade = true;
        }


        /// <summary>
        /// Executes the Instant Trend strategy
        /// </summary>
        /// <param name="data">TradeBars - the current OnData</param>
        /// <param name="tradesize"></param>
        /// <param name="trendCurrent">IndicatorDataPoint - the current trend value trend</param>
        /// <param name="current"></param>
        public OrderSignal ExecuteStrategy(TradeBars data, int tradesize, IndicatorDataPoint trendCurrent, out string current)
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
                if (nStatus == 1 && nTrig < (Math.Abs(nEntryPrice) / RevPct))
                {
                    if(maketrade)
                    {
                        ticket = ReverseToShort();
                        orderFilled = ticket.OrderId > 0;
                        
                    }
                    bReverseTrade = true;
                    retval = OrderSignal.revertToShort;
                    comment = string.Format("{0} nStatus == {1} && nTrig {2} < (nEntryPrice {3} * RevPct{4} orderFilled {5})", retval, nStatus, nTrig, nEntryPrice, RevPct, orderFilled);

                }
                else
                {
                    if (nStatus == -1 && nTrig > (Math.Abs(nEntryPrice) * RevPct))
                    {
                        if (maketrade)
                        {
                            ticket = ReverseToLong();
                            orderFilled = ticket.OrderId > 0;
                        }
                        bReverseTrade = true;
                        retval = OrderSignal.revertToLong;
                        comment = string.Format("{0} nStatus == {1} && nTrig {2} > (nEntryPrice {3} * RevPct{4}, orderFilled {5})", retval, nStatus, nTrig, nEntryPrice, RevPct, orderFilled);
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
                                try
                                {
                                    if (maketrade) ticket = _algorithm.Buy(_symbol, tradesize);
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }
                                retval = OrderSignal.goLong;
                                comment = string.Format("{0} nStatus {1} nTrig {2} > trendHistory[0].Value {3} xOver {4} orderFilled {5}",
                                    retval, nStatus, nTrig, trendHistory[0].Value, xOver, orderFilled);
                            }
                            else
                            {

                                nLimitPrice = Math.Round(Math.Max(data[_symbol].Low, (data[_symbol].Close - (data[_symbol].High - data[_symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);
                                try
                                {
                                    if (maketrade) ticket = _algorithm.LimitOrder(_symbol, tradesize, nLimitPrice, "Long Limit");
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);
                                }
                                retval = OrderSignal.goLongLimit;
                                comment = string.Format("{0} nStatus {1} nTrig {2} > trendHistory[0].Value {3} xOver {4} Limit Price {5}",
                                    retval, nStatus, nTrig, trendHistory[0].Value, xOver, nLimitPrice);
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
                                    try
                                    {
                                        if (maketrade) ticket = _algorithm.Sell(_symbol, tradesize);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                    retval = OrderSignal.goShort;
                                    comment = string.Format("{0} Enter Short after cancel trig xunder price down", retval);
                                }
                                else
                                {
                                    nLimitPrice = Math.Round(Math.Min(data[_symbol].High, (data[_symbol].Close + (data[_symbol].High - data[_symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);
                                    try
                                    {
                                        if (maketrade) ticket = _algorithm.LimitOrder(_symbol, -tradesize, nLimitPrice, "Short Limit");
                                        //ticket = _algorithm.Sell(_symbol, tradesize);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                    retval = OrderSignal.goShortLimit;
                                    comment = string.Format("{0} nStatus {1} nTrig {2} < trendHistory[0].Value {3} xOver {4} Limit Price {5}",
                                            retval, nStatus, nTrig, trendHistory[0].Value, xOver, nLimitPrice);
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
        private OrderTicket ReverseToLong()
        {
            nLimitPrice = 0;
            nStatus = 1;
            return _algorithm.Buy(_symbol, _algorithm.Portfolio[_symbol].Quantity * 2);
        }

        private OrderTicket ReverseToShort()
        {
            nLimitPrice = 0;
            nStatus = -1;
            return _algorithm.Sell(_symbol, _algorithm.Portfolio[_symbol].Quantity * 2);
        }

        public void Reset()
        {
            trendHistory.Reset();
            Barcount = 0;
        }
    }
}
