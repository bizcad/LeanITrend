using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Algorithm.Examples;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    class InstantaneousTrendAlgorithm : QCAlgorithm
    {
        private DateTime _startDate = new DateTime(2015, 9, 8);
        private DateTime _endDate = new DateTime(2015, 9, 10);
        private decimal _portfolioAmount = 10000;
        private decimal _transactionSize = 15000;

        private string symbol = "AAPL";

        private int barcount = 0;

        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;
        //private RollingWindow<IndicatorDataPoint> trendTrigger;
        private bool dowarmup = false;

        #region "logging P&L"

        // P & L
        private int sharesOwned = 0;
        decimal tradeprofit = 0m;
        decimal tradefees = 0m;
        decimal tradenet = 0m;
        private decimal lasttradefees = 0;
        decimal profit = 0m;
        decimal fees = 0m;
        private decimal netprofit = 0;
        private decimal dayprofit = 0;
        private decimal dayfees = 0;
        private decimal daynet = 0;
        private decimal lastprofit = 0;
        private decimal lastfees = 0;
        private int tradecount;
        private int lasttradecount;
        private DateTime tradingDate;
        private decimal nEntryPrice = 0;
        private decimal nExitPrice = 0;

        private Maximum MaxDailyProfit;
        private Minimum MinDailyProfit;


        #endregion
        #region "Custom Logging"
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");
        private readonly OrderReporter _orderReporter;

        private string ondataheader = @"Time,BarCount,trade size,Open,High,Low,Close,Time,Price,Trend,Trigger,comment,signal, Entry Price, Exit Price,orderId , unrealized, shares owned,trade profit, trade fees, trade net,last trade fees, profit, fees, net, day profit, day fees, day net, Portfolio Value";
        private string dailyheader = @"Trading Date,Daily Profit, Daily Fees, Daily Net, Cum profit, Cum Fees, Cum Net, Trades/day, Portfolio Value, Shares Owned";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private string comment;
        private OrderSignal signal;
        #endregion

        // Warm up
        private List<TradeBar> tradeBarList;

        // Strategy
        private InstantTrendStrategy iTrendStrategy;
        private bool shouldSellOutAtEod = true;
        private int orderId = 0;
        private int tradesize;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {
            #region logging
            var algoname = this.GetType().Name;
            mylog.Debug(algoname);
            mylog.Debug(ondataheader);
            dailylog.Debug(algoname);
            dailylog.Debug(dailyheader);
            transactionlog.Debug(transactionheader);
            var days = _endDate.Subtract(_startDate).TotalDays;
            MaxDailyProfit = new Maximum("MaxDailyProfit", (int)days);
            MinDailyProfit = new Minimum("MinDailyProfit", (int)days);
            #endregion

            //Initialize dates
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

            // Indicators
            Price = new RollingWindow<IndicatorDataPoint>(14);      // The price history

            // ITrend
            trend = new InstantaneousTrend(7);
            trendHistory = new RollingWindow<IndicatorDataPoint>(14);
            //trendTrigger = new RollingWindow<IndicatorDataPoint>(14);

            // The ITrendStrategy
            iTrendStrategy = new InstantTrendStrategy(symbol, 14, this);
            iTrendStrategy.ShouldSellOutAtEod = shouldSellOutAtEod;

            string warmup = @"Volume,Open,High,Low,Close,EndTime,Period,DataType,IsFillForward,Time,Symbol,Value,Price
373832,107.545,107.79,107.509,107.77,9/1/2015 3:46:00 PM,00:01:00,TradeBar,False,9/1/2015 3:45:00 PM,AAPL,107.77,107.77
558930,107.79,108.29,107.68,108.08,9/1/2015 3:47:00 PM,00:01:00,TradeBar,False,9/1/2015 3:46:00 PM,AAPL,108.08,108.08
519871,108.08,108.3,108.03,108.0899,9/1/2015 3:48:00 PM,00:01:00,TradeBar,False,9/1/2015 3:47:00 PM,AAPL,108.0899,108.0899
419835,108.085,108.28,108.08,108.11,9/1/2015 3:49:00 PM,00:01:00,TradeBar,False,9/1/2015 3:48:00 PM,AAPL,108.11,108.11
487146,108.12,108.2,107.9,108.07,9/1/2015 3:50:00 PM,00:01:00,TradeBar,False,9/1/2015 3:49:00 PM,AAPL,108.07,108.07
377260,108.071,108.28,108.069,108.2,9/1/2015 3:51:00 PM,00:01:00,TradeBar,False,9/1/2015 3:50:00 PM,AAPL,108.2,108.2
432494,108.21,108.46,108.12,108.42,9/1/2015 3:52:00 PM,00:01:00,TradeBar,False,9/1/2015 3:51:00 PM,AAPL,108.42,108.42
505142,108.41,108.42,108.085,108.09,9/1/2015 3:53:00 PM,00:01:00,TradeBar,False,9/1/2015 3:52:00 PM,AAPL,108.09,108.09
276146,108.0808,108.2,108.03,108.09,9/1/2015 3:54:00 PM,00:01:00,TradeBar,False,9/1/2015 3:53:00 PM,AAPL,108.09,108.09
365589,108.09,108.18,107.9551,108.18,9/1/2015 3:55:00 PM,00:01:00,TradeBar,False,9/1/2015 3:54:00 PM,AAPL,108.18,108.18
281108,108.18,108.2,108.07,108.1,9/1/2015 3:56:00 PM,00:01:00,TradeBar,False,9/1/2015 3:55:00 PM,AAPL,108.1,108.1
465920,108.1,108.14,108,108.08,9/1/2015 3:57:00 PM,00:01:00,TradeBar,False,9/1/2015 3:56:00 PM,AAPL,108.08,108.08
474265,108.08,108.45,108.05,108.37,9/1/2015 3:58:00 PM,00:01:00,TradeBar,False,9/1/2015 3:57:00 PM,AAPL,108.37,108.37
447959,108.36,108.46,108.24,108.3999,9/1/2015 3:59:00 PM,00:01:00,TradeBar,False,9/1/2015 3:58:00 PM,AAPL,108.3999,108.3999
631543,108.4,108.635,108.3,108.315,9/1/2015 4:00:00 PM,00:01:00,TradeBar,False,9/1/2015 3:59:00 PM,AAPL,108.315,108.315";
            //public TradeBar(DateTime time, string symbol, decimal open, decimal high, decimal low, decimal close, long volume, TimeSpan? period = null)
            if (dowarmup)
            {
                string[] arrWarmup = warmup.Split('\n');
                int linenumber = 0;
                foreach (string s in arrWarmup)
                {
                    if (linenumber++ > 0)
                    {
                        string[] fields = s.Split(',');
                        try
                        {
                            var vol = fields[0].ToInt64();
                            var dt = DateTime.Parse(fields[5]);
                            TradeBar tb = new TradeBar(dt,
                                fields[10],
                                System.Convert.ToDecimal(fields[1]),
                                System.Convert.ToDecimal(fields[2]),
                                System.Convert.ToDecimal(fields[3]),
                                System.Convert.ToDecimal(fields[4]),
                                vol,
                                null
                                );
                            Price.Add(idp(tb.EndTime, (tb.Close + tb.Open) / 2));
                            trend.Update(idp(tb.EndTime, Price[0].Value));
                            trendHistory.Add(idp(tb.EndTime, trend.Current.Value));
                            //trendTrigger.Add(idp(tb.EndTime, trend.Current.Value));
                            //if (linenumber > 3)
                            //{
                            //    trendTrigger[0].Value = 2 * trendHistory[0].Value - trendHistory[2].Value;
                            //}
                            iTrendStrategy.WarmUpTrendHistory(idp(tb.EndTime, trend.Current.Value));
                        }
                        catch (Exception ex)
                        {
                            throw new Exception(ex.Message);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            #region logging
            comment = string.Empty;
            tradingDate = data.Time;
            #endregion
            barcount++;

            if (dowarmup)
                if (data.Time.Month == _endDate.Month)
                    if (data.Time.Day == _endDate.Day)
                        if ((data.Time.Hour == 15 && data.Time.Minute > 45) || (data.Time.Hour == 16 && data.Time.Minute == 0))
                        {
                            if (tradeBarList == null)
                                tradeBarList = new List<TradeBar>();
                            tradeBarList.Add(data[symbol]);
                        }

            // Add the history for the bar
            var time = data.Time;
            Price.Add(idp(time, (data[symbol].Close + data[symbol].Open) / 2));

            // Update the indicators
            trend.Update(idp(time, Price[0].Value));
            trendHistory.Add(CalculateNewTrendHistoryValue(barcount, time, Price, trend));
            if (Portfolio[symbol].Invested)
            {
                tradesize = Math.Abs(Portfolio[symbol].Quantity);
            }
            else
            {
                tradesize = (int)(_transactionSize / Convert.ToInt32(Price[0].Value + 1));
            }

            
            Strategy(data);

            #region logging
            sharesOwned = Portfolio[symbol].Quantity;
            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23}",
                    data.Time,
                    barcount,
                    tradesize,
                    data[symbol].Open,
                    data[symbol].High,
                    data[symbol].Low,
                    data[symbol].Close,
                    data.Time.ToShortTimeString(),
                    Price[0].Value,
                    trend.Current.Value,
                    //trendTrigger[0].Value,
                    comment,
                    signal,
                    nEntryPrice,
                    nExitPrice,
                    orderId,
                    Portfolio.TotalUnrealisedProfit,
                    sharesOwned,
                    tradeprofit,
                    tradefees,
                    tradenet,
                    Portfolio.TotalPortfolioValue,
                    "",
                    "",
                    "",
                    ""
                    );
            mylog.Debug(logmsg);

            // reset the trade profit
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;
            #endregion

            if (data.Time.Hour == 16)
            {
                trend.Reset();
                trendHistory.Reset();
                //trendTrigger.Reset();
                barcount = 0;
                Plot("Strategy Equity", "Portfolio", Portfolio.TotalPortfolioValue);
            }

        }


        private IndicatorDataPoint CalculateNewTrendHistoryValue(int barcount, DateTime time, RollingWindow<IndicatorDataPoint> price, InstantaneousTrend tr)
        {
            if (barcount < 7 && barcount > 2)
            {
                return (idp(time, (price[0].Value + 2 * price[1].Value + price[2].Value) / 4));
            }
            else
            {
                return (idp(time, tr.Current.Value)); //add last iteration value for the cycle
            }
        }
        /// <summary>
        /// Run the strategy associated with this algorithm
        /// </summary>
        /// <param name="data">TradeBars - the data received by the OnData event</param>
        private void Strategy(TradeBars data)
        {

            #region "Strategy Execution"

            if (SellOutEndOfDay(data))
            {
                iTrendStrategy.Barcount = barcount;  // for debugging

                // if there were limit order tickets to cancel, wait a bar to execute the strategy
                if (!CanceledUnfilledLimitOrder())
                {
                    signal = iTrendStrategy.ExecuteStrategy(data, tradesize, trend.Current, out comment);
                }
            }

            #endregion

        }
        /// <summary>
        /// If the order did not fill within one bar, cancel it and assume the market moved away from the limit order
        /// </summary>
        private bool CanceledUnfilledLimitOrder()
        {
            #region "Unfilled Limit Orders"

            bool retval = false;
            var tickets = Transactions.GetOrderTickets(t => !t.Status.IsClosed());
            if (tickets != null && tickets.Any())
            {
                foreach (var ticket in tickets)
                {
                    ticket.Cancel();
                    retval = true;
                }
            }
            #endregion

            return retval;
        }
        public bool SellOutEndOfDay(TradeBars data)
        {
            if (shouldSellOutAtEod)
            {
                if (data.Time.Hour == 15 && data.Time.Minute > 49 || data.Time.Hour == 16)
                {
                    if (Portfolio[symbol].IsLong)
                    {
                        Sell(symbol, Portfolio[symbol].AbsoluteQuantity);
                    }
                    if (Portfolio[symbol].IsShort)
                    {
                        Buy(symbol, Portfolio[symbol].AbsoluteQuantity);
                    }

                    // Daily Profit
                    #region logging
                    if (data.Time.Hour == 16)
                    {
                        CalculateDailyProfits();
                        sharesOwned = Portfolio[symbol].Quantity;


                    }
                    #endregion

                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// Handle order events
        /// </summary>
        /// <param name="orderEvent">the order event</param>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
            ProcessOrderEvent(orderEvent);
        }
        /// <summary>
        /// Local processing of the order event
        /// </summary>
        /// <param name="orderEvent">OrderEvent - the order event</param>
        private void ProcessOrderEvent(OrderEvent orderEvent)
        {
            orderId = orderEvent.OrderId;
            var tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId);
            nEntryPrice = 0;
            nExitPrice = 0;

            if (tickets.Any())
            {
                foreach (OrderTicket ticket in tickets)
                {
                    var status = ticket.Status;
                    if (ticket.Status == OrderStatus.Canceled)
                    {
                        iTrendStrategy.orderFilled = false;
                    }
                    if (ticket.Status == OrderStatus.Filled)
                    {
                        iTrendStrategy.orderFilled = true;

                        #region logging
                        OrderReporter reporter = new OrderReporter((QCAlgorithm)this, transactionlog);
                        reporter.ReportTransaction(orderEvent, ticket);
                        tradecount++;
                        #endregion


                        if (Portfolio[orderEvent.Symbol].Invested)
                        {
                            iTrendStrategy.nEntryPrice = orderEvent.FillPrice;
                            #region logging
                            tradefees = Securities[symbol].Holdings.TotalFees - lasttradefees;
                            nEntryPrice = orderEvent.FillPrice;

                            #endregion


                        }
                        #region logging
                        else
                        {
                            tradefees += Securities[symbol].Holdings.TotalFees - lasttradefees;
                            nExitPrice = orderEvent.FillPrice;
                            CalculateTradeProfit(ticket);
                        }
                        #endregion
                    }
                }
            }
        }
        #region "Profit Calculations for logging"
        private void CalculateTradeProfit(OrderTicket ticket)
        {
            tradeprofit = Securities[symbol].Holdings.LastTradeProfit;
            tradenet = tradeprofit - tradefees;
            lasttradefees = Securities[symbol].Holdings.TotalFees;
        }
        private void CalculateDailyProfits()
        {
            foreach (SecurityHolding holding in Portfolio.Values)
            {
                #region logging
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

                MaxDailyProfit.Update(idp(tradingDate, daynet));
                MinDailyProfit.Update(idp(tradingDate, daynet));

                lasttradecount = tradecount;
                dayprofit = 0;
                dayfees = 0;
                daynet = 0;


                #endregion
            }
        }
        #endregion
        public override void OnEndOfAlgorithm()
        {
            Debug(string.Format("\nAlgorithm Name: {0}\n Ending Portfolio Value: {1} ", this.GetType().Name, Portfolio.TotalPortfolioValue));
            var tbl = ObjectToCsv.ToCsv(@",", tradeBarList, false);
            StringBuilder sb = new StringBuilder();

            int c = tbl.Count();
            if (c <= 0) return;
            foreach (var line in tbl)
            {
                sb.AppendLine(line);
            }
            dailylog.Debug(sb.ToString());
        }
        /// <summary>
        /// Convenience function which creates an IndicatorDataPoint
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
