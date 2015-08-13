using System;
using System.CodeDom;
using System.Runtime;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.Examples
{
    /// <summary>
    /// A strategy based on the Rate of Change and Finding max (sell) and min (buy) prices
    /// </summary>
    public class RateOfChangePercentStrategy
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
        private string comment;
        /// <summary>
        /// Flag to determine if the algo should go flat overnight.
        /// </summary>
        public bool shouldSellOutAtEod = true;
        //public int orderId { get; set; }
        private RollingWindow<IndicatorDataPoint> Price;
        /// <summary>
        /// the Algorithm being run.
        /// </summary>
        public QCAlgorithm _algorithm;
        /// <summary>
        /// The flag as to whether the order has been filled.
        /// </summary>
        public Boolean orderFilled { get; set; }
        /// <summary>
        /// the order ticket
        /// </summary>
        public OrderTicket ticket;
        /// <summary>
        /// 14 day min and max
        /// </summary>
        private IndicatorDataPoint maximum, minimum;

        private SimpleMovingAverage sma20;

        /// <summary>
        /// Empty Consturctor
        /// </summary>
        public RateOfChangePercentStrategy() { }

        /// <summary>
        /// Constructor initializes the symbol and period of the RollingWindow
        /// </summary>
        /// <param name="symbol">string - ticker symbol</param>
        /// <param name="period">int - the period of the Trend History Rolling Window</param>
        /// <param name="tradesize">int - the number of shares to trade</param>
        /// <param name="algorithm"></param>
        public RateOfChangePercentStrategy(string symbol, int period, QCAlgorithm algorithm)
        {
            sma20 = new SimpleMovingAverage(20);
            _symbol = symbol;
            Price = new RollingWindow<IndicatorDataPoint>(period);
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
        public string ExecuteStrategy(TradeBars data, int tradesize, IndicatorDataPoint max, IndicatorDataPoint min, RateOfChangePercent rocp, out int orderId)
        {
            maximum = max;
            minimum = min;
            Price.Add(idp(data[_symbol].EndTime, (data[_symbol].Close + data[_symbol].Open) / 2));
            orderId = 0;
            comment = string.Empty;
            sma20.Update(idp(data.Time, data[_symbol].Close));



            if (_algorithm.Portfolio[_symbol].IsLong) nStatus = 1;
            if (_algorithm.Portfolio[_symbol].IsShort) nStatus = -1;


            #region "Strategy Execution"

            bReverseTrade = false;

            try
            {

                if (!_algorithm.Portfolio.Invested)
                {
                    if (PricePassedAValley() && rocp.Current.Value < 0)
                    {
                        ticket = GetLong(tradesize);
                        orderId = ticket.OrderId;
                        comment = "Bot new position ppMin && rocp < 0";
                    }
                    if (PricePassedAPeak() && rocp.Current.Value > 0)
                    {
                        ticket = GetShort(tradesize);
                        orderId = ticket.OrderId;
                        comment = "Sld new position ppMin && rocp < 0";
                    }
                }
                else
                {
                    if (PricePassedAValley() && _algorithm.Portfolio[_symbol].IsShort && rocp.Current.Value > 0 )
                    {
                        if (Price[0].Value > sma20.Current.Value)
                        {
                            ticket = ReverseToLong();
                            comment = "Rev2Long Passed a Valley";
                        }
                    }

                    if (PricePassedAPeak() && _algorithm.Portfolio[_symbol].IsLong && rocp.Current.Value < 0)
                    {
                        if (Price[0].Value < sma20.Current.Value)
                        {
                            ticket = ReverseToShort();
                            comment = "Rev2Short Passed a Peak";
                        }
                    }
                }


            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            #endregion

            return comment;
        }

        private OrderTicket GetOut()
        {
            return _algorithm.Portfolio[_symbol].IsLong
                ? _algorithm.Sell(_symbol, _algorithm.Portfolio[_symbol].AbsoluteQuantity)
                : _algorithm.Buy(_symbol, _algorithm.Portfolio[_symbol].AbsoluteQuantity);
        }

    
        

        private OrderTicket GetLong(int tradesize)
        {
            return _algorithm.Buy(_symbol, tradesize);
        }
        private OrderTicket GetShort(int tradesize)
        {
            return _algorithm.Sell(_symbol, tradesize);
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
                if (data.Time.Hour == 15 && data.Time.Minute > 49 || data.Time.Hour == 16)
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

                    return false;
                }
            }
            return true;
        }
        private bool PricePassedAPeak()
        {
            try
            {
                if (Price.Count == 1)
                {
                    comment = "Price history not ready";
                    return false;
                }
                if (maximum >= Price[0].Value && maximum == Price[1].Value)
                    return true;
                return false;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        private bool PricePassedAValley()
        {
            if (Price.Count == 1)
            {
                comment = "Price history not ready";
                return false;
            }

            if (minimum <= Price[0].Value && minimum == Price[1].Value)
                return true;
            return false;
        }
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }
    }
}