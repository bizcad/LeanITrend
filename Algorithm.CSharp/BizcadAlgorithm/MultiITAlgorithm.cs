using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    public class MultiITAlgorithm : QCAlgorithm
    {
        private DateTime _startDate = new DateTime(2015, 9, 2);
        private DateTime _endDate = new DateTime(2015, 9, 3);
        private decimal _portfolioAmount = 10000;
        private decimal _transactionSize = 15000;

        private string symbol = "AAPL";

        private int barcount = 0;

        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;

        // lists
        private Dictionary<int, RollingWindow<IndicatorDataPoint>> trendHistoryList;
        private Dictionary<int, MultiITStrategy> strategyList;
        private Dictionary<int, InstantaneousTrend> trendList;
        //private RollingWindow<IndicatorDataPoint> trendTrigger;

        #region "Custom Logging"
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");
        private readonly OrderReporter _orderReporter;
        private List<OrderTransaction> _transactions;

        private string ondataheader = @"Time,BarCount,trade size,Open,High,Low,Close,Time,Price,comment,signal, Entry Price, Exit Price,orderId , unrealized, shares owned,trade profit, trade fees, trade net, Portfolio Value";
        private string dailyheader = @"Trading Date,Daily Profit, Daily Fees, Daily Net, Cum profit, Cum Fees, Cum Net, Trades/day, Portfolio Value, Shares Owned";
        //private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        
        private string comment;
        #endregion
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
        private int _tradecount = 0;
        private int lasttradecount;
        private DateTime tradingDate;
        private decimal nEntryPrice = 0;
        private decimal nExitPrice = 0;

        private Maximum MaxDailyProfit;
        private Minimum MinDailyProfit;


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
            _transactions = new List<OrderTransaction>();

            
            
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
            trendHistory = new RollingWindow<IndicatorDataPoint>(4);
            //trendTrigger = new RollingWindow<IndicatorDataPoint>(14);
            iTrendStrategy = new InstantTrendStrategy(symbol, 7, this);

            // lists
            trendList = new Dictionary<int, InstantaneousTrend>();
            trendHistoryList = new Dictionary<int, RollingWindow<IndicatorDataPoint>>();
            strategyList = new Dictionary<int, MultiITStrategy>();

            int listIndex = 0;
            for (decimal d = .05m; d < .3m; d += .01m)
            {
                trendList.Add(listIndex, new InstantaneousTrend("ITrend_" + d, 7, d));  // eg ITrend.25, period 7, alpha .25
                trendHistoryList.Add(listIndex, new RollingWindow<IndicatorDataPoint>(4));
                strategyList.Add(listIndex, new MultiITStrategy(symbol, this, trendHistoryList[listIndex]));
                listIndex++;
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

            // Add the history for the bar
            var time = data.Time;
            Price.Add(idp(time, (data[symbol].Close + data[symbol].Open) / 2));

            //// Update the indicators
            trend.Update(idp(time, Price[0].Value));
            // iTrendStrategy starts on bar 3 because it uses trendHistory[0] - trendHistory[3]
            trendHistory.Add(CalculateNewTrendHistoryValue(barcount, time, Price, trend));
            foreach (var thitem in trendHistoryList)
            {
                trendList[thitem.Key].Update(idp(time, Price[0].Value));
                thitem.Value.Add(idp(time, CalculateNewTrendHistoryValue(barcount, time, Price, trendList[thitem.Key])));
            }

            if (Portfolio[symbol].Invested)
            {
                tradesize = Math.Abs(Portfolio[symbol].Quantity);
            }
            else
            {
                tradesize = (int)(_transactionSize / Convert.ToInt32(Price[0].Value + 1));
            }


            if (barcount == 285)
                Debug("here");
            if (barcount == 294)
                Debug("here");
            if (barcount == 305)
                Debug("here");


            string matrix = Strategy(data);


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
                //trend.Current.Value,
                //trendTrigger[0].Value,
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
                    "",
                    "",
                    ""
                    );
            logmsg += matrix;
            mylog.Debug(logmsg);

            // reset the trade profit
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;
            #endregion

            if (data.Time.Hour == 16)
            {
                trend.Reset();
                //trendHistory.Reset();
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
        private string Strategy(TradeBars data)
        {

            #region "Strategy Execution"

            string ret = "";
            OrderSignal current = OrderSignal.doNothing;
            if (SellOutEndOfDay(data))
            {
                if (barcount == 4)
                    comment = string.Empty;
                // if there were limit order tickets to cancel, wait a bar to execute the strategy
                if (!CanceledUnfilledLimitOrder())
                {
                    StringBuilder sb = new StringBuilder();
                    // The ITrendStrategy
                    iTrendStrategy.ShouldSellOutAtEod = shouldSellOutAtEod;
                    iTrendStrategy.Barcount = barcount;  // for debugging
                     
                    current = iTrendStrategy.ExecuteStrategy(data, tradesize, trend.Current, out comment);

                    StringBuilder sb2 = new StringBuilder();
                    StringBuilder sb3 = new StringBuilder();
                    foreach (var it in strategyList)
                    {
                        it.Value.Barcount = barcount;
                        it.Value.ShouldSellOutAtEod = shouldSellOutAtEod;
                        current = it.Value.CheckSignal(data, tradesize, out comment);

                        sb.Append(((int)current).ToString(CultureInfo.InvariantCulture));
                        sb.Append(",");
                        sb2.Append(it.Value.sTrig);
                        sb2.Append(",");
                        sb3.Append(it.Value._trendHistory[0].Value);
                        sb3.Append(@",");
                    }
                    ret = sb.ToString() + "," + sb2.ToString() + "," + sb3.ToString();
                }
            }

            #endregion

            return ret;
        }
        /// <summary>
        /// If the order did not fill within one bar, cancel it and assume the market moved away from the limit order
        /// </summary>
        private bool CanceledUnfilledLimitOrder()
        {
            #region "Unfilled Limit Orders"

            bool retval = false;
            var closedtickets = Transactions.GetOrderTickets(t => t.Status.IsClosed());
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
                        _transactions.Add(reporter.ReportTransaction(orderEvent, ticket, false));
                        _tradecount++;
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
                    _tradecount - lasttradecount,
                    Portfolio[symbol].HoldingsValue,
                    sharesOwned,
                    ""
                    );
                dailylog.Debug(msg);

                MaxDailyProfit.Update(idp(tradingDate, daynet));
                MinDailyProfit.Update(idp(tradingDate, daynet));

                lasttradecount = _tradecount;
                dayprofit = 0;
                dayfees = 0;
                daynet = 0;


                #endregion
            }
        }
        #endregion
        public override void OnEndOfAlgorithm()
        {
            #region Logging stuff - Saving the logs

            int i = 0;
            //foreach (string symbol in Symbols)
            //{
            //    string filename = string.Format("ITrendDebug_{0}.csv", symbol);
            //    string filePath = @"C:\Users\JJ\Desktop\MA y señales\ITrend Debug\" + filename;
            //    // JJ do not delete this line it locates my engine\bin\debug folder
            //    //  I just uncomment it when I run on my local machine
            //    filePath = AssemblyLocator.ExecutingDirectory() + filename;

            //    if (File.Exists(filePath)) File.Delete(filePath);
            //    File.AppendAllText(filePath, stockLogging[i].ToString());
            //    Debug(string.Format("\nSymbol Name: {0}, Ending Portfolio Value: {1} ", symbol, Portfolio[symbol].Profit));

            //}
            string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = ObjectToCsv.ToCsv<OrderTransaction>(",", _transactions, true);
            using (StreamWriter fs = new StreamWriter(filepath))
            {
                foreach (var s in liststring)
                    fs.WriteLine(s);
                fs.Flush();
                fs.Close();
            }

            Debug(string.Format("\nAlgorithm Name: {0}\nEnding Portfolio Value: {1} ", this.GetType().Name, Portfolio.TotalPortfolioValue));

            #endregion Logging stuff - Saving the logs
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
