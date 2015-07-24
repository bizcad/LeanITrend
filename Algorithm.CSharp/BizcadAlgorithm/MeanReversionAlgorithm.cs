using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Examples
{
    /// <summary>
    /// A aelatively simple mean revesion algorithm using an EMA and SMA
    /// and trading when the difference between the two cross over the standard deviation
    /// 
    /// </summary>
    public class MeanReversionAlgorithm : QCAlgorithm
    {
        private string symbol = "SPY";
        private DateTime _startDate = new DateTime(2015, 5, 15);
        private DateTime _endDate = new DateTime(2015, 5, 15);
        // Custom Logging
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        private string ondataheader = @"Time,CurrentBar,Open,High,Low,Close,Price,Trend, ema10, sma10, difference,std, negstd,,,,Long Sig,Short Sig,long exit,shortexit, orderId , unrealized, shares owned,trade profit, profit, fees, day profit, day fees, day net";
        private string dailyheader = @"Trading Date,Daily Profit, Daily Fees, Daily Net, Cum profit, Cum Fees, Cum Net, Trades/day, Portfolio Value";

        private int barcount = 0;
        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;
        private ExponentialMovingAverage ema10;
        private RollingWindow<IndicatorDataPoint> emaHistory;
        private SimpleMovingAverage sma10;
        private RollingWindow<IndicatorDataPoint> smaHistory;
        private RollingWindow<IndicatorDataPoint> madiff;
        private StandardDeviation stddev;

        // P & L
        private bool openForTrading = true;
        private int sharesOwned = 0;
        private decimal portfolioProfit = 0;
        private decimal fillprice = 0;

        decimal fees = 0m;
        decimal tradeprofit = 0m;
        decimal profit = 0m;
        private decimal dayprofit = 0;
        private decimal dayfees = 0;
        private decimal daynet = 0;
        private decimal lastprofit = 0;
        private decimal lastfees = 0;

        // Strategy
        private decimal nEntryPrice = 0;
        private decimal RevPct = 1.015m;
        private decimal RngFac = .35m;
        private bool bReverseTrade = false;
        private int nDirection = 0;
        private decimal nLimitPrice = 0;
        private int xOver = 0;
        private int nStatus = 0;

        private int orderId = 0;
        private string reversed;
        private int tradesize;
        private decimal openprice = 0;

        private int tradecount;
        private int lasttradecount;
        private DateTime tradingDate;
        private bool shouldSellOutAtEod = true;
        private decimal factor = 1.0m;
        private int outsideStdDev = 0;
        private bool SeekingTop = true;
        private bool SeekingBottom = true;
        private DateTime tradeTime = DateTime.MinValue;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {
            //mylog.Debug(transheader);
            mylog.Debug("Mean Reversion Algorithm");
            mylog.Debug(ondataheader);
            dailylog.Debug("Mean Reversion Algorithm");
            dailylog.Debug(dailyheader);

            //Initialize dates
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(22000);

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
            Price = new RollingWindow<IndicatorDataPoint>(14);
            trendHistory = new RollingWindow<IndicatorDataPoint>(14);
            trend = new InstantaneousTrend(10);
            ema10 = new ExponentialMovingAverage(10);
            sma10 = new SimpleMovingAverage(10);
            madiff = new RollingWindow<IndicatorDataPoint>(390);
            stddev = new StandardDeviation(390);
            emaHistory = new RollingWindow<IndicatorDataPoint>(10);
            smaHistory = new RollingWindow<IndicatorDataPoint>(10);
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            decimal close = data[symbol].Close;
            barcount++;
            tradingDate = data.Time;
            var time = data.Time;
            Price.Add(idp(time, close));
            trend.Update(idp(time, close));
            trendHistory.Add(idp(time, close));
            ema10.Update(trend.Current);
            sma10.Update(trend.Current);
            emaHistory.Add(ema10.Current);
            smaHistory.Add(sma10.Current);
            var madiff1 = ema10.Current.Value - sma10.Current.Value;
            madiff.Add(idp(time, madiff1));
            stddev.Update(idp(time, madiff1));
            if (data.Time.Hour == 9 && data.Time.Minute == 31)
            {
                barcount = 1;
                tradesize = (int)(Portfolio.Cash / Convert.ToInt32(Price[0].Value + 1));
                openprice = Price[0].Value;
            }

            if (barcount > 2)
            {
                factor = 1.5m - (.5m * ((decimal)barcount / (decimal)360));
                Strategy(data);

                string logmsg =
                    string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27}",
                        data.Time,
                        barcount,
                        data[symbol].Open,
                        data[symbol].High,
                        data[symbol].Low,
                        data[symbol].Close,
                        Price[0].Value,
                        trend.Current.Value,
                        ema10.Current.Value,
                        sma10.Current.Value,
                        madiff[0].Value,
                        stddev.Current.Value * factor,
                        stddev.Current.Value * -factor,
                        ema10.Current.Value - sma10.Current.Value,
                        trend.Current.Value - ema10.Current.Value,
                        Price[0].Value - trend.Current.Value,
                        ema10.Current.Value > sma10.Current.Value && trend.Current.Value > ema10.Current.Value && Price[0].Value > trend.Current.Value,
                        trend.Current.Value < sma10.Current.Value && trend.Current.Value < ema10.Current.Value && Price[0].Value < trend.Current.Value,
                        trend.Current.Value < ema10.Current.Value,
                        trend.Current.Value > ema10.Current.Value,
                        orderId,
                        Portfolio.TotalUnrealisedProfit,
                        sharesOwned,
                        tradeprofit,
                        profit,
                        fees,
                        dayprofit,
                        dayfees,
                        daynet,
                        Portfolio.TotalPortfolioValue);
                mylog.Debug(logmsg);
            }

        }

        private void Strategy(TradeBars data)
        {
            if (Portfolio[symbol].IsLong) nStatus = 1;
            if (Portfolio[symbol].IsShort) nStatus = -1;


            /*
             * trend.Current.Value,
                        ema10.Current.Value,*/
            List<int> ss = new List<int>();
            ss.Add(Math.Sign(Price[0].Value));
            ss.Add(Math.Sign(trendHistory[0].Value));
            ss.Add(Math.Sign(emaHistory[0].Value));
            ss.Add(Math.Sign(smaHistory[0].Value));
            ss.Add(Math.Sign(Price[1].Value));
            ss.Add(Math.Sign(trendHistory[1].Value));
            ss.Add(Math.Sign(emaHistory[1].Value));
            ss.Add(Math.Sign(smaHistory[1].Value));


            if (barcount < 20) return;
            // SELL OUT AT THE EOD
            if (!SellOutEndOfDay(data))
            {
                //if (barcount == 127)
                //    Debug("here");
                #region StrategyExecution
                // If we are seeking a top, and we are outside
                //TopBottomSeeker(data);
                TripleMovingAverageStrategy();

                #endregion
            }
            sharesOwned = Portfolio[symbol].Quantity;
        }
        /// <summary>
        /// Implements the Triple Moving Average System from 
        /// http://www.tradingblox.com/Manuals/UsersGuideHTML/triplemovingaverage.htm
        /// </summary>
        private void TripleMovingAverageStrategy()
        {
            if (ema10.Current.Value > sma10.Current.Value && trend.Current.Value > ema10.Current.Value
                && ((Price[0].Value > trend.Current.Value) && !Portfolio[symbol].IsLong))
            {
                Buy(symbol, (int)Portfolio.Cash / (Price[0].Value - 1));
            }
            if (trend.Current.Value < sma10.Current.Value && trend.Current.Value < ema10.Current.Value
                && ((Price[0].Value < trend.Current.Value) && !Portfolio[symbol].IsShort))
            {
                Sell(symbol, (int)Portfolio.Cash / (Price[0].Value - 1));
            }
            if (Portfolio[symbol].IsLong && trend.Current.Value < ema10.Current.Value)
            {
                Sell(symbol, Portfolio[symbol].AbsoluteQuantity);
            }
            if (Portfolio[symbol].IsShort && trend.Current.Value > ema10.Current.Value)
            {
                Buy(symbol, Portfolio[symbol].AbsoluteQuantity);
            }
        }

        private void TopBottomSeeker(TradeBars data)
        {
            if (LookingForTrade() && outsideStdDev == 1 && SeekingTop && DirectionIsDown())
            {
                if (Portfolio[symbol].IsLong) // look for a sell point
                {
                    Sell(symbol, Portfolio[symbol].Quantity * 2);
                }
                else
                {
                    Sell(symbol, (int)Portfolio.Cash / (Price[0].Value - 1));
                }
                SeekingTop = false;
                SeekingBottom = true;
                tradeTime = data.Time;
            }
            // If we are outside and seeking a bottom
            if (LookingForTrade() && outsideStdDev == -1 && SeekingBottom && DirectionIsUp())
            {
                if (Portfolio[symbol].IsShort) // look for a sell point
                {
                    Buy(symbol, Portfolio[symbol].Quantity * 2);
                }
                else
                {
                    Buy(symbol, (int)Portfolio.Cash / (Price[0].Value - 1));
                }
                SeekingBottom = false;
                SeekingTop = true;
                tradeTime = data.Time;
            }
        }

        private bool DirectionIsUp()
        {
            return (Price[0].Value > Price[1].Value);
            //return (Price[0].Value > Price[1].Value) && (Price[1].Value > Price[2].Value);
        }

        private bool DirectionIsDown()
        {
            return Price[0].Value < Price[1].Value;
            //return Price[0].Value < Price[1].Value && (Price[1].Value < Price[2].Value);
        }

        // are we outside of the standard deviation
        private bool LookingForTrade()
        {
            if (madiff[0].Value > stddev.Current.Value * factor)
            {
                outsideStdDev = 1;
                return true;
            }
            else
            {
                if (madiff[0].Value < -stddev.Current.Value * factor)
                {
                    outsideStdDev = -1;
                    return true;
                }
                return false;
            }
        }

        private bool SellOutEndOfDay(TradeBars data)
        {
            if (shouldSellOutAtEod)
            {
                if (data.Time.Hour == 15 && data.Time.Minute > 55 && Portfolio[symbol].HoldStock)
                {
                    if (Portfolio[symbol].IsLong)
                    {
                        Sell(symbol, Portfolio[symbol].AbsoluteQuantity);
                        reversed = "EOD";
                    }
                    if (Portfolio[symbol].IsShort)
                    {
                        Buy(symbol, Portfolio[symbol].AbsoluteQuantity);
                        reversed = "EOD";
                    }
                    nStatus = 0;
                    System.Threading.Thread.Sleep(100);
                    CalculateDailyProfits();
                    sharesOwned = Portfolio[symbol].Quantity;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
            orderId = orderEvent.OrderId;
            if (orderEvent.Status == OrderStatus.Filled)
            {
                fillprice = orderEvent.FillPrice;
                foreach (SecurityHolding holding in Portfolio.Values)
                {
                    fees = holding.TotalFees;
                    tradeprofit = holding.LastTradeProfit;
                    profit = holding.Profit;
                }
                nEntryPrice = fillprice;
                tradecount++;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        public override void OnEndOfDay(string symbol)
        {
            base.OnEndOfDay();

        }
        /// <summary>
        /// Calculates profits after the last trade of the day
        /// </summary>
        private void CalculateDailyProfits()
        {
            foreach (SecurityHolding holding in Portfolio.Values)
            {
                dayprofit = holding.Profit - lastprofit;
                dayfees = holding.TotalFees - lastfees;
                daynet = dayprofit - dayfees;
                lastprofit = holding.Profit;
                lastfees = holding.TotalFees;
                string msg = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                    tradingDate.ToShortDateString(),
                    dayprofit,
                    dayfees,
                    daynet,
                    holding.Profit,
                    holding.TotalFees,
                    holding.Profit - holding.TotalFees,
                    tradecount - lasttradecount,
                    Portfolio.TotalPortfolioValue,
                    sharesOwned,
                    ""
                    );
                dailylog.Debug(msg);
                lasttradecount = tradecount;
                dayprofit = 0;
                dayfees = 0;
                daynet = 0;
            }
        }

        /// <summary>
        /// Factory function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from 
        /// new IndicatorDataPoint(data.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }


    }
}
