using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class MultiSignalConsolidatorAlgorithm : QCAlgorithm
    {
        private int LiveSignalIndex = 9;

        #region "Variables"

        private DateTime _startDate = new DateTime(2015, 6, 1);
        private DateTime _endDate = new DateTime(2015, 6, 10);
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

        List<SignalInfo> signalInfos = new List<SignalInfo>();
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

        private string ondataheader =
            @"Time,BarCount,Open,High,Low,Close,EndTime,Period,DataType,IsFillForward,Time,Symbol,Price,,Time,Price,Trend15, Trend001,comment,orderSignal,Owned,Unrealized, TradeProfit, TradeFees, TradeNet, Portf Val";

        private SigC _scig5C = new SigC();

        private string json;


        private string dailyheader = @"Trading Date,Daily Profit, Portfolio Value";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private List<OrderTransaction> _transactions;
        private List<OrderTransaction> _proformatransactions;
        private List<OrderEvent> _orderEvents = new List<OrderEvent>();
        private int _tradecount = 0;
        #endregion


        private bool shouldSellOutAtEod = true;
        private int orderId = 0;
        private OrderSignal orderSignal;
        private decimal nEntryPrice = 0;
        private string sigcomment;
        private string comment;
        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();

        // for live orders
        private List<OrderTicket> _ticketsQueue;

        // for simulated orders
        private List<ProformaOrderTicket> _ticketsSubmitted = new List<ProformaOrderTicket>();


        private string sig7comment;

        private TradeBarConsolidator fifteenMinuteConsolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(15));
        private InstantaneousTrend trend15Min;

        private bool CanMakeTrade = true;
        private bool MinuteDataActivated = false;

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
            var algoname = this.GetType().Name + " UseSig=" + LiveSignalIndex;
            mylog.Debug(algoname);

            mylog.Debug(ondataheader);
            dailylog.Debug(algoname);
            dailylog.Debug(dailyheader);
            _transactions = new List<OrderTransaction>();
            _proformatransactions = new List<OrderTransaction>();
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


            //_ticketsQueue = new ConcurrentQueue<OrderTicket>();
            _ticketsQueue = new List<OrderTicket>();
            //sim = new BrokerSimulator(this);

            #region lists

            signalInfos.Add(new SignalInfo
            {
                Id = 0,
                Name = "Minutes_015",
                IsActive = false,
                SignalJson = string.Empty,
                Value = OrderSignal.doNothing,
                InternalState = string.Empty,
                SignalType = typeof(Sig9)
            });

            signalInfos.Add(new SignalInfo
            {
                Id = 1,
                Name = "Minutes_001",
                IsActive = false,
                SignalJson = string.Empty,
                Value = OrderSignal.doNothing,
                InternalState = string.Empty,
                SignalType = typeof(Sig9)
            });

            foreach (SignalInfo s in signalInfos)
            {
                s.IsActive = false;
                if (s.Id == LiveSignalIndex)
                {
                    s.IsActive = true;
                }
            }

            #endregion

            // define our 15 minute consolidator
            //var fifteenMinuteConsolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(15));

            // if we want to make decisions every 15 minutes as well, we can add an event handler
            // to the DataConsolidated event
            fifteenMinuteConsolidator.DataConsolidated += OnFiftenMinuteAAPL;

            trend15Min = new InstantaneousTrend(3);
            RegisterIndicator(symbol, trend15Min, fifteenMinuteConsolidator, Field.Close);

            //int fast = 15;

            //int slow = 30;

            //// define our EMA, we'll manually register this, so we aren't using the helper function 'EMA(...)'
            //var fastEmaOnFifteenMinuteBars = new ExponentialMovingAverage("AAPL_EMA15", fast);
            //var slowEmaOnFifteenMinuteBars = new ExponentialMovingAverage("AAPL_EMA30", slow);

            //// register our indicator and consolidator together. this will wire the consolidator up to receive
            //// data for the specified symbol, and also set up the indicator to receive its data from the consolidator
            //RegisterIndicator("AAPL", fastEmaOnFifteenMinuteBars, fifteenMinuteConsolidator, Field.Close);
            //RegisterIndicator("AAPL", slowEmaOnFifteenMinuteBars, fifteenMinuteConsolidator, Field.Close);


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
            foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            {
                OnDataForSymbol(kvp);
            }
        }

        //15 minute events here:
        public void OnFiftenMinuteAAPL(object sender, TradeBar data)
        {

            if (barcount == 375)
                comment = "";
            if (!trend15Min.IsReady)
                return;
            KeyValuePair<Symbol, TradeBar> kvp = new KeyValuePair<Symbol, TradeBar>(data.Symbol, data);
            List<SignalInfo> signalInfos15 = new List<SignalInfo>(signalInfos.Where(s => s.Name == "Minutes_015"));
            if (signalInfos15.Any())
            {
                GetOrderSignals(kvp, signalInfos15);
                foreach (var signalInfo15 in signalInfos15)
                {
                    if (signalInfo15.Value != OrderSignal.doNothing)
                    {

                        if (CanMakeTrade)
                        {
                            MinuteDataActivated = true;
                        }
                        else
                        {
                            MinuteDataActivated = false;
                        }
                        if (Time.Hour == 15 && Time.Minute > 44 || Time.Hour == 16)
                        {
                            MinuteDataActivated = true;
                        }
                    }
                }
            }
            #region "biglog"

            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
                    ",{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35},{36},{37},{38},{39},{40}" +
                    ",{41}",
                    Time,
                    barcount,
                    data.Open,
                    data.High,
                    data.Low,
                    data.Close,
                    data.EndTime,
                    data.Period,
                    data.DataType,
                    data.IsFillForward,
                    data.Time,
                    data.Symbol,
                    data.Price,
                    "",
                    Time.ToShortTimeString(),
                    Price[0].Value,
                    trend15Min.Current.Value,
                    comment,
                    orderSignal,
                    sharesOwned,
                    Portfolio.TotalUnrealisedProfit,
                    tradeprofit,
                    tradefees,
                    tradenet,
                    Portfolio.TotalPortfolioValue,
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                    );
            //mylog.Debug(logmsg);

            #endregion
        }

        private void OnDataForSymbol(KeyValuePair<Symbol, TradeBar> data)
        {
            #region logging

            comment = string.Empty;
            tradingDate = this.Time;

            #endregion

            barcount++;
            if (Time.Hour == 9 && Time.Minute == 31)
            {
                CanMakeTrade = true;
                MinuteDataActivated = false;
            }
            if (barcount >= 375)
                comment = "";
            // Add the history for the bar
            var time = this.Time;

            Price.Add(idp(time, (data.Value.Close + data.Value.Open) / 2));

            // Update the indicators
            trend.Update(idp(time, Price[0].Value));
            var x = trend.Current;
            trendHistory.Add(CalculateNewTrendHistoryValue(barcount, time, Price, trend));

            List<SignalInfo> signalInfos001 = new List<SignalInfo>(signalInfos.Where(s => s.Name == "Minutes_001"));
            if (signalInfos001.Any())
            {

                GetOrderSignals(data, signalInfos001);
                if (MinuteDataActivated)
                {
                    if (SoldOutAtEndOfDay(data))
                    {
                        foreach (var signalInfo001 in signalInfos001)
                        {
                            List<SignalInfo> signalInfos15 =
                                new List<SignalInfo>(signalInfos.Where(s => s.Name == "Minutes_015"));
                            if (signalInfos15.Any())
                            {
                                if (CanMakeTrade)
                                {
                                    foreach (var signalInfo15 in signalInfos15)
                                    {
                                        if (signalInfo15.Value == signalInfo001.Value && CanMakeTrade && signalInfo001.Value != OrderSignal.doNothing)
                                        {
                                            signalInfo001.IsActive = true;
                                            ExecuteStrategy(symbol, signalInfo001, data);
                                        }

                                    }
                                }
                            }

                        }
                    }
                }
            }

            #region logging

            sharesOwned = Portfolio[symbol].Quantity;

            #region "biglog"
            string logmsg =
            string.Format(
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
                ",{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35},{36},{37},{38},{39},{40}" +
                ",{41}",
                Time,
                barcount,
                data.Value.Open,
                data.Value.High,
                data.Value.Low,
                data.Value.Close,
                data.Value.EndTime,
                data.Value.Period,
                data.Value.DataType,
                data.Value.IsFillForward,
                data.Value.Time,
                data.Value.Symbol,
                data.Value.Price,
                "",
                Time.ToShortTimeString(),
                Price[0].Value,
                trend15Min.Current.Value,
                trend.Current.Value,
                comment,
                orderSignal,
                sharesOwned,
                Portfolio.TotalUnrealisedProfit,
                tradeprofit,
                tradefees,
                tradenet,
                Portfolio.TotalPortfolioValue,
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                ""
                );

            #endregion

            mylog.Debug(logmsg);

            // reset the trade profit
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;

            #endregion

            // At the end of day, reset the trend and trendHistory
            if (time.Hour == 16)
            {
                barcount = 0;
            }
        }



        /// <summary>
        /// Run the strategy associated with this algorithm
        /// </summary>
        /// <param name="data">TradeBars - the data received by the OnData event</param>
        private void GetOrderSignals(KeyValuePair<Symbol, TradeBar> data, List<SignalInfo> signalInfos)
        {

            // ToDo:  Handle Partial Fills

            #region "GetOrderSignals Execution"

            List<ProformaOrderTicket> handledTickets = HandleTickets();

            #region lists
            foreach (SignalInfo info in signalInfos)
            {
                var id = info.Id;
                info.Value = OrderSignal.doNothing;
                Type t = info.SignalType;
                var sig = Activator.CreateInstance(t) as ISigSerializable;
                if (sig != null)
                {
                    sig.symbol = data.Key;
                    if (barcount > 1)
                    {
                        sig.Deserialize(info.SignalJson);
                    }
                    sig.Barcount = barcount; // for debugging
                    //sig.maketrade = info.IsActive;

                    // Todo: handle partial fills.

                    decimal entryPrice = sig.nEntryPrice;
                    // set the properties from the handled ticket.
                    var handledTicket = handledTickets.FirstOrDefault();
                    if (handledTicket != null)
                    {

                        switch (handledTicket.Status)
                        {
                            // sig.orderFilled defaults to true in the three constructors

                            case OrderStatus.Filled:
                                sig.orderFilled = true;
                                if (Portfolio[symbol].HoldStock)
                                {
                                    // Remember sig.nEntryPrice is carried forward as is xOver
                                    entryPrice = handledTicket.AverageFillPrice;
                                }
                                _ticketsQueue.Remove(_ticketsQueue.FirstOrDefault(z => z.OrderId == handledTicket.OrderId));
                                break;
                            case OrderStatus.Canceled:
                                sig.orderFilled = false;
                                _ticketsQueue.Remove(_ticketsQueue.FirstOrDefault(z => z.OrderId == handledTicket.OrderId));
                                entryPrice = 0;
                                break;
                        }


                    }
                    else
                    {
                        info.Comment = string.Format("Handled Ticket for barcount {0} is null.", barcount);
                    }
                    sig.IsLong = Portfolio[symbol].IsLong;
                    sig.IsShort = Portfolio[symbol].IsShort;
                    sig.nEntryPrice = entryPrice;

                    Dictionary<string, string> paramlist = new Dictionary<string, string>
                    {
                        {"symbol", data.Key.ToString()},
                        {"Barcount", barcount.ToString(CultureInfo.InvariantCulture)},
                        {"nEntryPrice", entryPrice.ToString(CultureInfo.InvariantCulture)},
                        {"IsLong", Portfolio[symbol].IsLong.ToString()},
                        {"IsShort", Portfolio[symbol].IsShort.ToString()},
                        {"trend", trend.Current.Value.ToString(CultureInfo.InvariantCulture)}
                    };


                    info.Value = sig.CheckSignal(data, paramlist, out comment);

                    //info.Value = sig.CheckSignal(data, trend.Current, out comment);
                    info.Comment = comment;

                    if (Time.Hour == 16)
                    {
                        sig.Reset();
                    }
                    if (barcount >= 0)
                    {
                        json = sig.Serialize();
                        info.SignalJson = json;
                    }
                    info.InternalState = sig.GetInternalStateFields().ToString();

                }
                else
                {
                    info.Comment = "Signal is null";
                }
            }
            #endregion  // lists

            #endregion  // execution
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

                            OrderTransactionFactory transactionFactory = new OrderTransactionFactory((QCAlgorithm)this);
                            OrderTransaction t = transactionFactory.Create(orderEvent, ticket, false);
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
            Debug(string.Format("\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n UseSig = {3}", this.GetType().Name, symbol, Portfolio.TotalPortfolioValue, LiveSignalIndex));
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
            SendTradesToFile("trades.csv", _orderTransactionProcessor.Trades);
            //SendTradesToFile("simtrades.csv", _proformaProcessor.Trades);
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
                tradeprofit = lasttrade.GainOrLoss;
                tradenet = tradeprofit + tradefees;
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
        private void ExecuteStrategy(Symbol symbol, SignalInfo actualOrder, KeyValuePair<Symbol, TradeBar> data)
        {
            decimal limitPrice = 0m;
            int shares = PositionShares(symbol, actualOrder);
            ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
            OrderTicket ticket;
            switch (actualOrder.Value)
            {
                case OrderSignal.goLongLimit:
                    // Define the limit price.
                    limitPrice = priceCalculator.Calculate(data.Value, actualOrder, RngFac);
                    ticket = LimitOrder(symbol, shares, limitPrice, actualOrder.Id.ToString(CultureInfo.InvariantCulture));
                    _ticketsQueue.Add(ticket);
                    break;

                case OrderSignal.goShortLimit:
                    limitPrice = priceCalculator.Calculate(data.Value, actualOrder, RngFac);
                    ticket = LimitOrder(symbol, shares, limitPrice, actualOrder.Id.ToString(CultureInfo.InvariantCulture));
                    _ticketsQueue.Add(ticket);
                    break;

                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    ticket = MarketOrder(symbol, shares, false, actualOrder.Id.ToString(CultureInfo.InvariantCulture));
                    _ticketsQueue.Add(ticket);
                    break;

                default: break;
            }
            MinuteDataActivated = true;
        }


        /// <summary>
        /// This function is called prior to calling the GetOrderSignals to check to see if a ticket was filled
        /// If the ticket did not fill within one bar, cancel it and assume the market moved away from the limit order
        /// 
        /// It re
        /// </summary>

        private List<ProformaOrderTicket> HandleTickets()
        {
            if (barcount == 20)
                comment = "103";

            List<ProformaOrderTicket> proformaOrderTickets = new List<ProformaOrderTicket>();
            //if (maketrade)
            //{
            // process a real order
            foreach (OrderTicket queuedTicket in _ticketsQueue)
            {
                ProformaOrderTicket proformaLiveTicket = new ProformaOrderTicket();

                // Check the ticket against the Transactions version of the ticket
                IEnumerable<OrderTicket> livetickets = Transactions.GetOrderTickets(t => t.OrderId == queuedTicket.OrderId);

                if (livetickets != null)
                {
                    foreach (OrderTicket liveticket in livetickets)
                    {
                        proformaLiveTicket.Status = liveticket.Status;
                        proformaLiveTicket.OrderId = liveticket.OrderId;
                        proformaLiveTicket.Symbol = liveticket.Symbol;
                        proformaLiveTicket.Source = liveticket.Tag;
                        proformaLiveTicket.TicketTime = liveticket.Time;
                        proformaLiveTicket.Security_Type = liveticket.SecurityType;
                        proformaLiveTicket.Tag = liveticket.Tag;
                        proformaLiveTicket.TicketOrderType = liveticket.OrderType;
                        proformaLiveTicket.Direction = liveticket.Quantity > 0
                            ? OrderDirection.Buy
                            : OrderDirection.Sell;

                        if (liveticket.Status == OrderStatus.Canceled)
                        {
                            proformaLiveTicket.Status = OrderStatus.Canceled;
                        }

                        if (liveticket.Status == OrderStatus.Submitted)
                        {
                            liveticket.Cancel();
                            proformaLiveTicket.Status = OrderStatus.Canceled;
                            proformaLiveTicket.QuantityFilled = 0; // they are probably already 0
                            proformaLiveTicket.AverageFillPrice = 0;
                        }
                        // ToDo:  Handle partial tickets
                        if (liveticket.Status == OrderStatus.Filled)
                        {

                            proformaLiveTicket.Status = OrderStatus.Filled;
                            proformaLiveTicket.QuantityFilled = (int)liveticket.QuantityFilled;
                            proformaLiveTicket.AverageFillPrice = liveticket.AverageFillPrice;
                            CanMakeTrade = false;
                        }
                    }
                }
                else
                {
                    proformaLiveTicket.ErrorMessage =
                        string.Format("Ticket with Id {0} could not be found in Transactions.", queuedTicket.OrderId);


                }
                proformaOrderTickets.Add(proformaLiveTicket);
            }
            return proformaOrderTickets;
            //}
            //else
            //{
            //    // Process a sumulated order
            //    var tickets = _ticketsSubmitted.Where(t => System.Convert.ToInt32(t.Source) == sigId);
            //    foreach (var ticket in tickets)
            //    {
            //        var simticket = sim._orderTickets.FirstOrDefault(s => s.Value.OrderId == ticket.OrderId).Value;

            //        if (simticket.Status == OrderStatus.Submitted)
            //        {
            //            // Todo: do something with the cancelled ticket such as save it to a file or collection for later analysis

            //            if (sim.TryCancelTicket(simticket))
            //            {
            //                // remove the cancelled ticket from the simulator
            //                var cancelledTicket = sim.RemoveCancelledTicket(simticket);

            //                // remove the cancelled ticket from the _ticketsSubmitted collection
            //                _ticketsSubmitted.Remove(ticket);

            //                // it was cancelled so the ticket did not fill
            //                orderfilled = false;
            //                return cancelledTicket;
            //            }
            //            return null;
            //        }

            //        if (simticket.Status == OrderStatus.Filled)
            //        {

            //            // Create a transaction and add it to the OrderProcessor
            //            OrderTransactionFactory fac = new OrderTransactionFactory(this);
            //            OrderTransaction transaction = fac.Create(sim, simticket);
            //            if (simticket.OrderId == 27)
            //                comment = "";
            //            _proformatransactions.Add(transaction);
            //            _proformaProcessor.ProcessTransaction(transaction);

            //            ProformaOrderTicket filledTicket = sim.RemoveTicket(simticket);
            //            _ticketsSubmitted.Remove(ticket);

            //            // orderfilled = true;  // no need to change the filled
            //            return filledTicket;
            //        }

            //        orderfilled = false;
            //        _ticketsSubmitted.Remove(ticket);
            //        return simticket;

            //        // ToDo: Partial fills
            //    }
            //}

            return null;
        }

        private decimal GetBetSize(Symbol symbol, SignalInfo signalInfo)
        {
            // *********************************
            //  ToDo: Kelly Goes here in a custom bet sizer
            //  This implementation uses the same as the original algo
            //    and just refactors it out to a class.
            // *********************************
            IBetSizer allocator = new InstantTrendBetSizer(this);
            //if (!signalInfo.IsActive)
            //    return allocator.BetSize(symbol, Price[0].Value, _transactionSize, signalInfo, _proformaProcessor);
            return allocator.BetSize(symbol, Price[0].Value, _transactionSize, signalInfo);
        }

        /// <summary>
        /// Estimate number of shares, given a kind of operation.
        /// </summary>
        /// <param name="symbol">The symbol to operate.</param>
        /// <param name="order">The kind of order.</param>
        /// <returns>The signed number of shares given the operation.</returns>
        public int PositionShares(Symbol symbol, SignalInfo signalInfo)
        {
            int quantity = 0;
            int operationQuantity;
            decimal targetSize;


            targetSize = GetBetSize(symbol, signalInfo);

            switch (signalInfo.Value)
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
                    if (signalInfo.IsActive)
                    {
                        quantity = -Portfolio[symbol].Quantity;
                    }
                    //else
                    //{
                    //    quantity = -_proformaProcessor.GetPosition(symbol);
                    //}
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    if (signalInfo.IsActive)
                    {
                        quantity = -2 * Portfolio[symbol].Quantity;
                    }
                    //else
                    //{
                    //    quantity = -2 * _proformaProcessor.GetPosition(symbol);
                    //}
                    break;

                default:
                    quantity = 0;
                    break;
            }

            if (quantity == 0)
                System.Diagnostics.Debug.WriteLine("Position Quantity is 0");
            return quantity;
        }

        /// <summary>
        /// Sells out all positions at 15:50, and calculates the profits for the day
        ///  emails the transactions for the day to me
        /// </summary>
        /// <param name="data">TradeBars - the data</param>
        /// <returns>false if end of day, true during the day </returns>
        public bool SoldOutAtEndOfDay(KeyValuePair<Symbol, TradeBar> data)
        {
            if (shouldSellOutAtEod)
            {
                if (this.Time.Hour == 15 && this.Time.Minute > 49 || this.Time.Hour == 16)
                {
                    if (CanMakeTrade)
                    {
                        if (Portfolio[symbol].IsLong)
                        {
                            Sell(symbol, Portfolio[symbol].AbsoluteQuantity);
                        }
                        if (Portfolio[symbol].IsShort)
                        {
                            Buy(symbol, Portfolio[symbol].AbsoluteQuantity);
                        }
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
        private IndicatorDataPoint CalculateNewTrendHistoryValue(int barcount, DateTime time, RollingWindow<IndicatorDataPoint> price, InstantaneousTrend tr)
        {
            //if (barcount < 7 && barcount > 2)
            //{
            //    return (idp(time, (price[0].Value + 2 * price[1].Value + price[2].Value) / 4));
            //}
            //else
            //{
            //    return (idp(time, tr.Current.Value)); //add last iteration value for the cycle
            //}
            if (!trendHistory.IsReady)
                return idp(time, price[0].Value);
            return (idp(time, tr.Current.Value)); //add last iteration value for the cycle
        }

        #endregion

        #region "Logging Methods"
        private void SendTradesToFile(string filename, IList<MatchedTrade> tradelist)
        {
            string filepath = AssemblyLocator.ExecutingDirectory() + filename;
            if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<MatchedTrade>(",", tradelist);
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
                {
                    if (!s.Contains("Symbol"))
                        fs.WriteLine(s);
                }
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
