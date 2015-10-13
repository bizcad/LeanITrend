
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class InstantTrendAlgorithmOriginal : QCAlgorithm
    {
        #region "Variables"

        private DateTime _startDate = new DateTime(2015, 8, 11);
        private DateTime _endDate = new DateTime(2015, 8, 14);
        private decimal _portfolioAmount = 10000;
        private decimal _transactionSize = 15000;

        private string symbol = "AAPL";

        private int barcount = 0;

        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;
        #region lists
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
        private decimal totalProfit = 0;
        
        private int lasttradecount;
        private DateTime tradingDate;
        private decimal nExitPrice = 0;
        private OrderStatus tradeResult;


        #endregion
        #region "Custom Logging"
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        //private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");
        private readonly OrderTransactionFactory _orderTransactionFactory;

        private string ondataheader = @"Time,BarCount,trade size,Volume,Open,High,Low,Close,EndTime,Period,DataType,IsFillForward,Time,Symbol,Value,Price,,Time,Price,Trend,comment,signal, Entry Price, Exit Price,Trade Result,orderId, unrealized, shares owned,trade profit, trade fees, trade net,last trade fees, profit, fees, net, day profit, day fees, day net, Portfolio Value";
        private string dailyheader = @"Trading Date,Daily Profit, Daily Fees, Daily Net, Cum profit, Cum Fees, Cum Net, Trades/day, Portfolio Value, Shares Owned";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private List<OrderTransaction> _transactions;
        private List<OrderEvent> _orderEvents = new List<OrderEvent>();
        private int _tradecount = 0;
        #endregion

        // Strategy
        private InstantTrendStrategyOriginal iTrendStrategy;
        private bool shouldSellOutAtEod = true;
        private int orderId = 0;
        private int tradesize;
        private OrderSignal signal;
        private decimal nEntryPrice = 0;
        private string comment;
        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();

        #endregion

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
            string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
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

            // The ITrendStrategy
            iTrendStrategy = new InstantTrendStrategyOriginal(symbol, 14, this);
            iTrendStrategy.ShouldSellOutAtEod = shouldSellOutAtEod;
            #region lists
            #endregion

            var security = Securities[symbol];
            security.TransactionModel = new ConstantFeeTransactionModel(1.0m);

        }
        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            #region logging
            comment = string.Empty;
            tradingDate = this.Time;
            #endregion
            barcount++;

            // Add the history for the bar
            var time = this.Time;
            Price.Add(idp(time, (data[symbol].Close + data[symbol].Open) / 2));

            // Update the indicators
            trend.Update(idp(time, Price[0].Value));
            trendHistory.Add(CalculateNewTrendHistoryValue(barcount, time, Price, trend));
            #region lists
            #endregion
            if (Portfolio[symbol].Invested)
            {
                tradesize = Math.Abs(Portfolio[symbol].Quantity);
            }
            else
            {
                tradesize = (int)(_transactionSize / Convert.ToInt32(Price[0].Value + 1));
            }
            if (barcount > 100)
                comment = "";
            CanceledUnfilledLimitOrder();

            Strategy(data);

            #region logging
            sharesOwned = Portfolio[symbol].Quantity;
            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33}",
                    time,
                    barcount,
                    tradesize,
                    data[symbol].Volume,
                    data[symbol].Open,
                    data[symbol].High,
                    data[symbol].Low,
                    data[symbol].Close,
                    data[symbol].EndTime,
                    data[symbol].Period,
                    data[symbol].DataType,
                    data[symbol].IsFillForward,
                    data[symbol].Time,
                    data[symbol].Symbol,
                    data[symbol].Value,
                    data[symbol].Price,
                    "",
                    time.ToShortTimeString(),
                    Price[0].Value,
                    trend.Current.Value,
                    comment,
                    signal,
                    nEntryPrice,
                    nExitPrice,
                    tradeResult,
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

            // reset the trade profit
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;
            #endregion

            if (time.Hour == 16)
            {
                //trend.Reset();
                //trendHistory.Reset();
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
            if (SellOutEndOfDay(data))
            {
                // if there were limit order tickets to cancel, wait a bar to execute the strategy

                iTrendStrategy.Barcount = barcount;  // for debugging
                iTrendStrategy.nEntryPrice = nEntryPrice;
                signal = iTrendStrategy.ExecuteStrategy(data, tradesize, trend.Current, out comment);
                #region lists
                #endregion

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

            //var tickets = Transactions.GetOrderTickets(p => p.Time > Transactions.UtcTime.AddMinutes(-2));

            foreach (OrderTicket orderTicket in Transactions.GetOrderTickets().Where(orderTicket => orderTicket.Status == OrderStatus.Submitted || orderTicket.Status == OrderStatus.Invalid))
            {
                orderTicket.Cancel();
                retval = true;
            }

            #endregion

            return retval;
        }
        public bool SellOutEndOfDay(TradeBars data)
        {
            if (shouldSellOutAtEod)
            {
                if (this.Time.Hour == 15 && this.Time.Minute > 49 || this.Time.Hour == 16)
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
                    if (this.Time.Hour == 16)
                    {
                        CalculateDailyProfits();
                        sharesOwned = Portfolio[symbol].Quantity;
                        var _transactionsAsCsv = CsvSerializer.Serialize<OrderTransaction>(",", _transactions, true);
                        StringBuilder sb = new StringBuilder();
                        foreach (string s in _transactionsAsCsv)
                            sb.AppendLine(s);
                        string attachment = sb.ToString();
                        Notify.Email("nicholasstein@cox.net",
                            "Todays Trades " + this.Time.ToLongDateString(),
                            "Number of Trades: " + _tradecount,
                            attachment);
                        SendTransactionsToFile();
                        _transactions = new List<OrderTransaction>();

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
            var security = Securities[orderEvent.Symbol];
            IEnumerable<OrderTicket> tickets;
            var tm = this.BrokerageModel.GetTransactionModel(security);
            if (orderEvent.Status == OrderStatus.Filled)
                _orderEvents.Add(orderEvent);
            orderId = orderEvent.OrderId;
            tradeResult = orderEvent.Status;
            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                case OrderStatus.None:
                case OrderStatus.Submitted:
                    tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId && t.Status == orderEvent.Status);
                    
                    break;
                case OrderStatus.Canceled:
                    tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId && t.Status == orderEvent.Status);
                    iTrendStrategy.orderFilled = false;
                    break;
                case OrderStatus.Filled:
                case OrderStatus.PartiallyFilled:
                    tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId && t.Status == orderEvent.Status);
                    if (tickets != null)
                    {
                        foreach (OrderTicket ticket in tickets)
                        {
                            iTrendStrategy.orderFilled = true;
                            if (Portfolio[orderEvent.Symbol].Invested)
                            {
                                nEntryPrice = Portfolio[symbol].IsLong ? orderEvent.FillPrice : orderEvent.FillPrice * -1;
                                nExitPrice = 0;
                            }
                            else
                            {
                                nExitPrice = nEntryPrice < 0 ? orderEvent.FillPrice : orderEvent.FillPrice * -1;
                                nEntryPrice = 0;
                            }

                            #region "log the ticket as a OrderTransacton"

                            var transactionFactory = new OrderTransactionFactory((QCAlgorithm)this);
                            var t = transactionFactory.Create(orderEvent, ticket, false);
                            _transactions.Add(t);
                            _orderTransactionProcessor.ProcessTransaction(t);
                            _tradecount++;
                            if (_orderTransactionProcessor.TotalProfit != totalProfit)
                            {
                                CalculateTradeProfit();
                            }
                            totalProfit = _orderTransactionProcessor.TotalProfit;

                            #endregion
                        }
                    }

                    break;

            }

        }
        #region "Profit Calculations for logging"
        private void CalculateTradeProfit()
        {
            var lasttrade = _orderTransactionProcessor.Trades.LastOrDefault();
            tradefees = _orderTransactionProcessor.LastTradeCommission;
            if (lasttrade != null) tradeprofit = lasttrade.GainOrLoss;
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
                    Portfolio.TotalPortfolioValue,
                    sharesOwned,
                    ""
                    );
                dailylog.Debug(msg);

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
            Debug(string.Format("\nAlgorithm Name: {0}\n Ending Portfolio Value: {1} ", this.GetType().Name, Portfolio.TotalPortfolioValue));
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

            //SendOrderEventsToFile();
            SendTradesToFile();
        }

        private void SendTradesToFile()
        {
            string filepath = AssemblyLocator.ExecutingDirectory() + "trades.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<MatchedTrade>(",", _orderTransactionProcessor.Trades, true);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                    fs.WriteLine(s);
                fs.Flush();
                fs.Close();
            }
        }

        private void SendTransactionsToFile()
        {
            string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            //if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<OrderTransaction>(",", _transactions, true);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                    fs.WriteLine(s);
                fs.Flush();
                fs.Close();
            }
        }
        private void SendOrderEventsToFile()
        {
            string filepath = AssemblyLocator.ExecutingDirectory() + "orderEvents.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<OrderEvent>(",", _orderEvents, true);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                    fs.WriteLine(s);
                fs.Flush();
                fs.Close();
            }
        }

        /// <summary>
        /// Convenience function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from 
        /// new IndicatorDataPoint(this.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

    }

}

