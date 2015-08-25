using System;
using System.Linq;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Examples
{
    class InstantaneousTrendAlgorithm : QCAlgorithm
    {
        private DateTime _startDate = new DateTime(2015, 5, 19);
        private DateTime _endDate = new DateTime(2015, 8, 21);
        private decimal _portfolioAmount = 22000;
        private decimal _transactionSize = 22000;

        private string symbol = "AAPL";

        #region "Custom Logging"
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");
        private readonly OrderReporter _orderReporter;

        private string ondataheader = @"Time,BarCount,trade size,Open,High,Low,Close,Time,Price,Trend,Trigger,ZeroLag,SMA20, iFishTrend,CyberCycle,iFishCyberCycle,maximum, minimum,pricePassedMax,pricePassedMin,ROC,RSI High,RSI Low, iFishRSIHigh, iFishRSILow, direction,slope, comment, Entry Price, Exit Price,orderId , unrealized, shares owned,trade profit, trade fees, trade net,last trade fees, profit, fees, net, day profit, day fees, day net, Portfolio Value";
        private string dailyheader = @"Trading Date,Daily Profit, Daily Fees, Daily Net, Cum profit, Cum Fees, Cum Net, Trades/day, Portfolio Value, Shares Owned";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private string comment;

        #endregion

        private int barcount = 0;

        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;
        private RollingWindow<IndicatorDataPoint> trendTrigger;

        #region logging
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

        private decimal nEntryPrice = 0;
        private decimal nExitPrice = 0;
        private DateTime tradingDate;



        #endregion

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
            trendTrigger = new RollingWindow<IndicatorDataPoint>(14);

            // The ITrendStrategy
            iTrendStrategy = new InstantTrendStrategy(symbol, 14, this);
            iTrendStrategy.ShouldSellOutAtEod = shouldSellOutAtEod;
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            comment = string.Empty;
            barcount++;
            tradingDate = data.Time;
            decimal slope = 0;

            // Add the history for the bar
            var time = data.Time;
            Price.Add(idp(time, (data[symbol].Close + data[symbol].Open) / 2));

            // Update the indicators
            trend.Update(idp(time, Price[0].Value));
            trendHistory.Add(idp(time, trend.Current.Value)); //add last iteration value for the cycle
            trendTrigger.Add(idp(time, trend.Current.Value));
            if (barcount == 1)
            {
                tradesize = (int)(Portfolio.Cash / Convert.ToInt32(Price[0].Value + 1));
            }

            // Logging and iTrendStrategy start on bar 3 because it uses trendHistory[0] - trendHistory[3]
            if (barcount < 7 && barcount > 2)
            {
                trendHistory[0].Value = (Price[0].Value + 2 * Price[1].Value + Price[2].Value) / 4;
            }

            if (barcount > 2)
            {
                trendTrigger[0].Value = 2 * trendHistory[0].Value - trendHistory[2].Value;
            }

            Strategy(data);
            sharesOwned = Portfolio[symbol].Quantity;

            #region logging
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
                    trendTrigger[0].Value,
                    comment,
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
                    ""
                    );
            mylog.Debug(logmsg);

            // reset the trade profit for logging
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;
            #endregion

            if (data.Time.Hour == 16)
            {
                trend.Reset();
                trendHistory.Reset();
                trendTrigger.Reset();
                barcount = 0;
            }

        }


        /// <summary>
        /// Run the strategy associated with this algorithm
        /// </summary>
        /// <param name="data">TradeBars - the data received by the OnData event</param>
        private void Strategy(TradeBars data)
        {
            comment = string.Empty;
            #region "Strategy Execution"

            if (SellOutEndOfDay(data))
            {
                iTrendStrategy.Barcount = barcount;  // for debugging

                // if there were limit order tickets to cancel, wait a bar to execute the strategy
                if (!CanceledUnfilledLimitOrder())
                    comment = iTrendStrategy.ExecuteStrategy(data, tradesize, trend.Current, trendTrigger[0]);
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
                        #endregion
                        reporter.ReportTransaction(orderEvent, ticket);

                        tradecount++;

                        if (Portfolio[orderEvent.Symbol].Invested)
                        {
                            iTrendStrategy.nEntryPrice = orderEvent.FillPrice; ;
                            tradefees = Securities[symbol].Holdings.TotalFees - lasttradefees;
                        }
                        else
                        {
                            tradefees += Securities[symbol].Holdings.TotalFees - lasttradefees;
                            CalculateTradeProfit(ticket);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the net profit on the last trade and updates the last trade fees
        /// </summary>
        /// <param name="ticket">OrderTicket - the ticket for the trade</param>
        private void CalculateTradeProfit(OrderTicket ticket)
        {
            tradeprofit = Securities[symbol].Holdings.LastTradeProfit;
            tradenet = tradeprofit - tradefees;
            lasttradefees = Securities[symbol].Holdings.TotalFees;
        }
        /// <summary>
        /// Calculates and reports profits or losses after the last trade of the day
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
                #region logging
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
                #endregion
                lasttradecount = tradecount;
                dayprofit = 0;
                dayfees = 0;
                daynet = 0;
            }
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
                    if (data.Time.Hour == 16)
                    {
                        CalculateDailyProfits();
                        sharesOwned = Portfolio[symbol].Quantity;
                    }

                    return false;
                }
            }
            return true;
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
