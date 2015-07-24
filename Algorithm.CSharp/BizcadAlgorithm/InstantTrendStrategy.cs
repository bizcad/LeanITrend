using System;
using System.Runtime;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.Examples
{
    /// <summary>
    /// 
    /// </summary>
    public class InstantTrendStrategy
    {
        /// <summary>
        /// The entry price for the latest trade
        /// </summary>
        public decimal nEntryPrice { get; set; }
        public decimal nExitPrice { get; set; }


        private bool bReverseTrade = false;
        private string _symbol { get; set; }
        private decimal RevPct = 1.015m;
        private decimal RngFac = .35m;
        private decimal nLimitPrice = 0;
        private int nStatus = 0;
        private int xOver = 0;
        /// <summary>
        /// Flag to determine if the algo should go flat overnight.
        /// </summary>
        public bool shouldSellOutAtEod;
        //public int orderId { get; set; }
        private RollingWindow<IndicatorDataPoint> trendHistory;
        /// <summary>
        /// the Algorithm being run.
        /// </summary>
        public QCAlgorithm _algorithm;
        /// <summary>
        /// The flag as to whether the order has been filled.
        /// </summary>
        public Boolean orderFilled { get; set; }


        /// <summary>
        /// Empty Consturctor
        /// </summary>
        public InstantTrendStrategy() { }

        /// <summary>
        /// Constructor initializes the symbol and period of the RollingWindow
        /// </summary>
        /// <param name="symbol">string - ticker symbol</param>
        /// <param name="period">int - the period of the Trend History Rolling Window</param>
        /// <param name="tradesize">int - the number of shares to trade</param>
        /// <param name="algorithm"></param>
        public InstantTrendStrategy(string symbol, int period, QCAlgorithm algorithm)
        {
            _symbol = symbol;
            trendHistory = new RollingWindow<IndicatorDataPoint>(period);
            _algorithm = algorithm;
            orderFilled = true;

        }


        /// <summary>
        /// Executes the Instant Trend strategy
        /// </summary>
        /// <param name="data">TradeBars - the current OnData</param>
        /// <param name="tradesize"></param>
        /// <param name="trendCurrent">IndicatorDataPoint - the current trend value trend</param>
        /// <param name="orderId">int - the orderId if one is placed, -1 if order has not filled and 0 if no order was placed</param>
        public string ExecuteStrategy(TradeBars data, int tradesize, IndicatorDataPoint trendCurrent, IndicatorDataPoint triggerCurrent, out int orderId)
        {
            orderId = 0;
            string comment = string.Empty;
            OrderTicket ticket;
            trendHistory.Add(trendCurrent);
            if (_algorithm.Portfolio[_symbol].IsLong) nStatus = 1;
            if (_algorithm.Portfolio[_symbol].IsShort) nStatus = -1;
            if (!trendHistory.IsReady) return "Trend Not Ready";

            if (!SellOutEndOfDay(data))
            {
                #region "Strategy Execution"

                bReverseTrade = false;

                try
                {
                    //if (_algorithm.Portfolio[_symbol].IsLong) nStatus = 1;
                    //if (_algorithm.Portfolio[_symbol].IsShort) nStatus = -1;

                    var nTrig = 2 * trendHistory[0].Value - trendHistory[2].Value;
                    if (orderFilled)
                    {
                        if (nStatus == 1 && data[_symbol].Close < (nEntryPrice/RevPct))
                        {
                            comment = string.Format("Long Reverse to short. Close < {0} / {1}", nEntryPrice,RevPct);
                            ticket = ReverseToShort();
                            orderFilled = ticket.OrderId > 0;
                            orderId = ticket.OrderId;
                            bReverseTrade = true;
                        }
                        else
                        {
                            if (nStatus == -1 && data[_symbol].Close > (nEntryPrice*RevPct))
                            {
                                comment = string.Format("Short Reverse to Long. Close > {0} * {1}", nEntryPrice, RevPct);
                                ticket = ReverseToLong();
                                orderFilled = ticket.OrderId > 0;
                                orderId = ticket.OrderId;
                                bReverseTrade = true;
                            }
                        }
                        if (!bReverseTrade)
                        {
                            if (nTrig > trendHistory[0].Value)
                            {
                                if (xOver == -1 && nStatus != 1)
                                {

                                    nLimitPrice = Math.Max(data[_symbol].Low, (data[_symbol].Close - (data[_symbol].High - data[_symbol].Low)*RngFac));
                                    ticket = _algorithm.LimitOrder(_symbol, tradesize, nLimitPrice, "Long Limit");
                                    //ticket = _algorithm.Buy(_symbol, tradesize);
                                    orderFilled = ticket.OrderId > 0;
                                    orderId = ticket.OrderId;
                                    comment = string.Format("Enter Long Limit at {0} trig xover price up", nLimitPrice);
                                    //comment = "Enter Long Market";
                                }
                                xOver = 1;
                            }
                            else
                            {
                                if (nTrig < trendHistory[0].Value)
                                {
                                    if (xOver == 1 && nStatus != -1)
                                    {
                                        nLimitPrice = Math.Min(data[_symbol].High, (data[_symbol].Close + (data[_symbol].High - data[_symbol].Low)*RngFac));
                                        ticket = _algorithm.LimitOrder(_symbol, -tradesize, nLimitPrice, "Short Limit");
                                        //ticket = _algorithm.Sell(_symbol, tradesize);
                                        orderFilled = ticket.OrderId > 0;
                                        orderId = ticket.OrderId;
                                        comment = string.Format("Enter Short Limit at {0} trig xover price down", nLimitPrice);
                                        //comment = "Enter Short Market";
                                    }
                                    xOver = -1;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }
                #endregion
            }
            return comment;
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
        private bool SellOutEndOfDay(TradeBars data)
        {
            if (shouldSellOutAtEod)
            {
                if (data.Time.Hour == 15 && data.Time.Minute > 55 || data.Time.Hour == 16)
                {
                    if (_algorithm.Portfolio[_symbol].IsLong)
                    {
                        _algorithm.Sell(_symbol, _algorithm.Portfolio[_symbol].AbsoluteQuantity);
                    }
                    if (_algorithm.Portfolio[_symbol].IsShort)
                    {
                        _algorithm.Buy(_symbol, _algorithm.Portfolio[_symbol].AbsoluteQuantity);
                    }

                    System.Threading.Thread.Sleep(100);
                    
                    return true;
                }
            }
            return false;
        }
    }
}