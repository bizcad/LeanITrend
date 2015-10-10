using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
    class InstantaneousTrendAlgorithm : QCAlgorithm
    {
        private int LiveSignalIndex = 0;


        #region "Variables"

        private DateTime _startDate = new DateTime(2015, 8, 11);
        private DateTime _endDate = new DateTime(2015, 8, 14);
        private decimal _portfolioAmount = 10000;
        private decimal _transactionSize = 15000;
        //+----------------------------------------------------------------------------------------+
        //  Algorithm Control Panel                         
        // +---------------------------------------------------------------------------------------+
        private static int ITrendPeriod = 7;            // Instantaneous Trend period.
        private static decimal Tolerance = 0.000m;      // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m;     // Percentage tolerance before revert position.

        private static decimal maxLeverage = 1m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.00m;         // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.

        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.

        private bool resetAtEndOfDay = true;            // Reset the strategies at EOD.
        private bool noOvernight = true;                // Close all positions before market close.
        // +---------------------------------------------------------------------------------------+

        private Symbol symbol = new Symbol("AAPL");
        //private string symbol = "AAPL";

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

        private string ondataheader = @"Time,BarCount,Volume,Open,High,Low,Close,EndTime,Period,DataType,IsFillForward,Time,Symbol,Value,Price,,Time,Price,Trend,sig.Signal, sig.nTrig,sig.orderFilled, sig.Entry,sig.comment,orderSignal,nTrig,orderFilled, Entry Price,comment, Exit Price,Strategy Entry Price, Trade Result,orderId, unrealized, shares owned,trade profit, trade fees, trade net,last trade fees, profit, fees, net, day profit, day fees, day net, Portfolio Value";
        private string dailyheader = @"Trading Date,Daily Profit, Portfolio Value";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private List<OrderTransaction> _transactions;
        private List<OrderEvent> _orderEvents = new List<OrderEvent>();
        private int _tradecount = 0;
        #endregion

        // Strategy
        private InstantTrendStrategy iTrendStrategy;
        private bool shouldSellOutAtEod = true;
        private int orderId = 0;
        private OrderSignal signal;
        private decimal nEntryPrice = 0;
        private string sigcomment;
        private string comment;
        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();
        private ConcurrentQueue<OrderTicket> _ticketsQueue;
        OrderSignal[] signals = new OrderSignal[1];
        

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
            iTrendStrategy = new InstantTrendStrategy(symbol, 14, this);
            for (int i = 0; i < signals.Length; i++)
                signals[i] = OrderSignal.doNothing;



            _ticketsQueue = new ConcurrentQueue<OrderTicket>();
            #region lists
            #endregion


            // for use with Tradier. Default is IB.
            //var security = Securities[symbol];
            //security.TransactionModel = new ConstantFeeTransactionModel(1.0m);

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

            if (barcount > 17)
                comment = "";

            var of = CancelUnfilledLimitOrders();
            iTrendStrategy.orderFilled = of;

            signal = Strategy(data);

            #region logging
            sharesOwned = Portfolio[symbol].Quantity;
            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
                    ",{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35},{36},{37}",
                    time,
                    barcount,
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
                    signals[0],
                    iTrendStrategy.nTrig,
                    iTrendStrategy.orderFilled,
                    iTrendStrategy.nEntryPrice,
                    comment,
                    "",
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

            // At the end of day, reset the trend and trendHistory
            if (time.Hour == 16)
            {
                trend.Reset();
                trendHistory.Reset();
                iTrendStrategy.Reset();
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
        private OrderSignal Strategy(TradeBars data)
        {

            #region "Strategy Execution"

            for (int i = 0; i < signals.Length; i++)
                signals[i] = OrderSignal.doNothing;
            // do not run the srategy after getting flat at the end of day
            if (SellOutEndOfDay(data))
            {
                int tradesize = Convert.ToInt32(GetBetSize(symbol));
               

                
                #region iTrendStrategy
                iTrendStrategy.Barcount = barcount;  // for debugging

                // If we are holding stock, set the entry price for the strategy
                //  the entry price is made absolute in the strategy to compare the the trigger
                if (Portfolio[symbol].HoldStock)
                {
                    iTrendStrategy.nEntryPrice = Portfolio[symbol].HoldingsCost / Portfolio[symbol].AbsoluteQuantity;
                }

                // Run the strategy only to check the signal

                iTrendStrategy.maketrade = false;
                iTrendStrategy.SetTradesize(tradesize);
                signals[0] = iTrendStrategy.CheckSignal(data, trend.Current, out comment);
                #endregion

                // Execute only the selected strategy with it's signal
                if(signals[LiveSignalIndex] != OrderSignal.doNothing)
                    ExecuteStrategy(symbol, signals[LiveSignalIndex], data);

                #region lists
                #endregion

            }

            #endregion

            return signals[LiveSignalIndex];
        }


        #region "Event Processiong"
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
        /// Local processing of the order event.  It only logs the transaction and orderEvent
        /// </summary>
        /// <param name="orderEvent">OrderEvent - the order event</param>
        private void ProcessOrderEvent(OrderEvent orderEvent)
        {
            IEnumerable<OrderTicket> tickets;

            //add to the list of order events which is saved to a file when running locally 
            //  I will use this file to test Stefano Raggi's code
            if (orderEvent.Status == OrderStatus.Filled)
                _orderEvents.Add(orderEvent);

            orderId = orderEvent.OrderId;
            tradeResult = orderEvent.Status;
            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                case OrderStatus.None:
                case OrderStatus.Submitted:
                    // just checking to make sure they are coming through
                    tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId && t.Status == orderEvent.Status);
                    break;
                case OrderStatus.Canceled:
                    // just checking
                    tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId && t.Status == orderEvent.Status);
                    break;
                case OrderStatus.Filled:
                case OrderStatus.PartiallyFilled:
                    tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId && t.Status == orderEvent.Status);
                    if (tickets != null)
                    {
                        foreach (OrderTicket ticket in tickets)
                        {
                            #region logging
                            // These two functions are now handled by calls preceding the call to the Strategy
                            // by checking the _ticketsQueue.  I tested and the results are the same, but here we
                            //  avoid any issues with the async nature of the event handler.  

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
                            #endregion "logging"
                        }
                    }
                    break;
            }
        }
        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            Debug(string.Format("\nAlgorithm Name: {0}\n Ending Portfolio Value: {1} \n LiveSignalIndex = {2}", this.GetType().Name, Portfolio.TotalPortfolioValue, LiveSignalIndex));
            #region logging
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
            #endregion
        }

        #endregion

        #region "Profit Calculations for logging"
        private void CalculateTradeProfit()
        {
            var lasttrade = _orderTransactionProcessor.Trades.LastOrDefault();
            tradefees = _orderTransactionProcessor.LastTradeCommission;
            if (lasttrade != null)
            {
                tradenet = lasttrade.GainOrLoss;
                tradeprofit = tradenet - tradefees;
            }
        }
        private void CalculateDailyProfits()
        {
            // get todays trades
            var trades = _orderTransactionProcessor.Trades.Where(t => t.DateAcquired.Year == tradingDate.Year
                                                                      && t.DateAcquired.Month == tradingDate.Month
                                                                      && t.DateAcquired.Day == tradingDate.Day);

            var todayNet = trades.Sum(t => t.GainOrLoss);

            #region logging
            string message = String.Format("{0},{1},{2}",
                tradingDate.ToShortDateString(),
                todayNet,
                Portfolio.TotalPortfolioValue
                );

            dailylog.Debug(message);

            lasttradecount = _tradecount;
            dayprofit = 0;
            dayfees = 0;
            daynet = 0;


            #endregion

        }
        #endregion

        #region "Methods"
        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="actualOrder">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(Symbol symbol, OrderSignal actualOrder, TradeBars data)
        {
            decimal limitPrice = 0m;
            int shares = PositionShares(symbol, actualOrder);
            ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();

            switch (actualOrder)
            {
                case OrderSignal.goLongLimit:
                    // Define the limit price.
                    limitPrice = priceCalculator.Calculate(data[symbol], actualOrder, RngFac);
                    _ticketsQueue.Enqueue(LimitOrder(symbol, shares, limitPrice));
                    break;

                case OrderSignal.goShortLimit:
                    limitPrice = priceCalculator.Calculate(data[symbol], actualOrder, RngFac);
                    _ticketsQueue.Enqueue(LimitOrder(symbol, shares, limitPrice));
                    break;

                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    _ticketsQueue.Enqueue(MarketOrder(symbol, shares)); // Send the order.
                    break;

                default: break;
            }
        }

        private decimal GetBetSize(Symbol symbol)
        {
            // *********************************
            //  ToDo: Kelly Goes here in a custom bet sizer
            //  This implementation uses the same as the original algo
            //    and just refactors it out to a class.
            // *********************************
            IBetSizer allocator = new InstantTrendBetSizer(this);
            return allocator.BetSize(symbol, Price[0].Value, _transactionSize);
        }

        /// <summary>
        /// Estimate number of shares, given a kind of operation.
        /// </summary>
        /// <param name="symbol">The symbol to operate.</param>
        /// <param name="order">The kind of order.</param>
        /// <returns>The signed number of shares given the operation.</returns>
        public int PositionShares(Symbol symbol, OrderSignal order)
        {
            int quantity;
            int operationQuantity;
            decimal targetSize;
            targetSize = GetBetSize(symbol);

            switch (order)
            {
                case OrderSignal.goLongLimit:
                case OrderSignal.goLong:
                    //operationQuantity = CalculateOrderQuantity(symbol, targetSize);     // let the algo decide on order quantity
                    operationQuantity = (int)targetSize;
                    quantity = Math.Min(maxOperationQuantity, operationQuantity);
                    break;

                case OrderSignal.goShortLimit:
                case OrderSignal.goShort:
                    //operationQuantity = CalculateOrderQuantity(symbol, targetSize);     // let the algo decide on order quantity
                    operationQuantity = (int)targetSize;
                    quantity = -Math.Min(maxOperationQuantity, operationQuantity);
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    quantity = -Portfolio[symbol].Quantity;
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    quantity = -2 * Portfolio[symbol].Quantity;
                    break;

                default:
                    quantity = 0;
                    break;
            }
            return quantity;
        }
        /// <summary>
        /// If the order did not fill within one bar, cancel it and assume the market moved away from the limit order
        /// </summary>
        private bool CancelUnfilledLimitOrders()
        {
            bool orderfilled = true;
            while (_ticketsQueue.Count > 0)
            {
                OrderTicket ticket;
                bool gotTicket = _ticketsQueue.TryDequeue(out ticket);
                if (gotTicket)
                {
                    // ToDo: I may want to check the ticket against the Transactions version of the last ticket,
                    //       but this way I can get it by OrderId instead of by Status as in the commented code below

                    if (ticket.Status == OrderStatus.Submitted)
                    {
                        ticket.Cancel();
                        orderfilled = false;
                    }
                }

            }

            //foreach (OrderTicket orderTicket in Transactions.GetOrderTickets().Where(orderTicket => orderTicket.Status == OrderStatus.Submitted || orderTicket.Status == OrderStatus.Invalid))
            //{
            //    orderTicket.Cancel();
            //    orderfilled = false;
            //}
            return orderfilled;
        }
        /// <summary>
        /// Sells out all positions at 15:50, and calculates the profits for the day
        ///  emails the transactions for the day to me
        /// </summary>
        /// <param name="data">TradeBars - the data</param>
        /// <returns>false if end of day, true during the day </returns>
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

        #endregion

        #region "Logging Methods"
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
        #endregion


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
