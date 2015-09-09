using System;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// From Ehlers Cybernetics page 27 on Trading the trend
    /// </summary>
    public class InstantTrendStrategy
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
        private RollingWindow<IndicatorDataPoint> trendHistory;

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

            if (!SellOutEndOfDay(data))
            {
                #region "Strategy Execution"


                bReverseTrade = false;
                try
                {
                    var nTrig = 2 * trendHistory[0].Value - trendHistory[2].Value;
                    if (nStatus == 1 && nTrig < (nEntryPrice / RevPct))
                    {
                        comment = string.Format("Long Reverse to short. Close < {0} / {1}", nEntryPrice, RevPct);
                        ticket = ReverseToShort();
                        orderFilled = ticket.OrderId > 0;
                        bReverseTrade = true;
                        retval = OrderSignal.revertToShort;
                    }
                    else
                    {
                        if (nStatus == -1 && nTrig > (nEntryPrice * RevPct))
                        {
                            comment = string.Format("Short Reverse to Long. Close > {0} * {1}", nEntryPrice, RevPct);
                            ticket = ReverseToLong();
                            orderFilled = ticket.OrderId > 0;
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
                                    try
                                    {
                                        ticket = _algorithm.Buy(_symbol, tradesize);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                    comment = string.Format("Enter Long after cancel trig xover price up");
                                    retval = OrderSignal.goLong;
                                }
                                else
                                {
                                    nLimitPrice = Math.Round(Math.Max(data[_symbol].Low, (data[_symbol].Close - (data[_symbol].High - data[_symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);
                                    try
                                    {
                                        ticket = _algorithm.LimitOrder(_symbol, tradesize, nLimitPrice, "Long Limit");
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine(e);
                                    }
                                    current = string.Format("Enter Long Limit trig xover price up", nLimitPrice);
                                    retval = OrderSignal.goLongLimit;
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
                                            ticket = _algorithm.Sell(_symbol, tradesize);
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(e);
                                        }
                                        comment = string.Format("Enter Short after cancel trig xunder price down");
                                        retval = OrderSignal.goShort;
                                    }
                                    else
                                    {
                                        nLimitPrice = Math.Round(Math.Min(data[_symbol].High, (data[_symbol].Close + (data[_symbol].High - data[_symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);
                                        try
                                        {
                                            ticket = _algorithm.LimitOrder(_symbol, -tradesize, nLimitPrice, "Short Limit");
                                        }
                                        catch (Exception e)
                                        {
                                            Console.WriteLine(e);
                                        }
                                        //ticket = _algorithm.Sell(_symbol, tradesize);
                                        comment = string.Format("Enter Short at market trig xover price down");
                                        retval = OrderSignal.goShortLimit;
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
            }
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
        /// <summary>
        /// Used to warm up the trend history
        /// </summary>
        /// <param name="trendCurrent"></param>
        public void WarmUpTrendHistory(IndicatorDataPoint trendCurrent)
        {
            trendHistory.Add(trendCurrent);
        }
        private bool SellOutEndOfDay(TradeBars data)
        {
            if (ShouldSellOutAtEod)
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

                    return true;
                }
            }
            return false;
        }
    }
}