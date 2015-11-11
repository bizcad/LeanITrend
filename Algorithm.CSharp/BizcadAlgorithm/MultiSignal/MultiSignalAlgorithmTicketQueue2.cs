using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class MultiSignalAlgorithmTicketQueue2 : QCAlgorithm
    {
        private int LiveSignalIndex = 0;

        #region "Variables"
        DateTime startTime = DateTime.Now;
        //private DateTime _startDate = new DateTime(2015, 8, 10);
        //private DateTime _endDate = new DateTime(2015, 8, 14);
        private DateTime _startDate = new DateTime(2015, 10, 19);
        private DateTime _endDate = new DateTime(2015, 10, 28);
        private decimal _portfolioAmount = 26000;
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
        private decimal lossThreshhold = -55;           // When unrealized losses fall below, revert position
        // +---------------------------------------------------------------------------------------+

        private Symbol symbol;

        private int barcount = 0;

        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;

        #region ITrend
        private ITrendSignal iTrendSignal;
        private Dictionary<string, OrderSignal> LastOrderSent = new Dictionary<string, OrderSignal>();
        #endregion

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
            @"Time,BarCount,Volume, Open,High,Low,Close,,,Time,Price,Trend, Trigger, orderSignal, Comment,, EntryPrice, Exit Price,Unrealized,Order Id, Owned, TradeNet, Portfolio";

        private SigC _scig5C = new SigC();

        private string json;


        private string dailyheader = @"Trading Date,Daily Profit, Portfolio Value";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private List<OrderTransaction> _transactions;
        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();
        private List<OrderTicket> _ticketsQueue;

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
        private StringBuilder minuteReturns = new StringBuilder();
        private StringBuilder minuteHeader = new StringBuilder();
        private bool minuteHeaderFlag = true;


        // for simulated orders
        private List<ProformaOrderTicket> _ticketsSubmitted = new List<ProformaOrderTicket>();


        //private string sig7comment;

        //private TradeBarConsolidator fifteenMinuteConsolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(15));
        //private InstantaneousTrend trend15Min;

        //private bool CanMakeTrade = true;
        //private bool MinuteDataActivated = false;

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
            _proformatransactions = new List<OrderTransaction>();
            string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
            #endregion


            //Initialize dates
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);

            symbol = new Symbol("AAPL");
            #region "Read Symbols from File"
            /**********************************************
             THIS SECTION IS FOR READING SYMBOLS FROM A FILE
            ************************************************/
            string symbols;
            var filename = AssemblyLocator.ExecutingDirectory() + "symbols.txt";
            using (StreamReader sr = new StreamReader(filename))
            {
                symbols = sr.ReadLine();
                sr.Close();
            }
            //symbol = new Symbol(symbols);
            #endregion

            minuteReturns.AppendFormat("{0},{1}", symbol, _startDate.ToShortDateString());
            minuteHeader.AppendFormat("Symbol,Date");

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

            // Indicators
            Price = new RollingWindow<IndicatorDataPoint>(14);      // The price history

            // ITrend
            trend = new InstantaneousTrend("Main", 7, .24m);

            _orderTransactionProcessor = new OrderTransactionProcessor();
            _transactions = new List<OrderTransaction>();
            _ticketsQueue = new List<OrderTicket>();
            #region ITrend
            iTrendSignal = new ITrendSignal(7);
            LastOrderSent.Add(symbol, OrderSignal.doNothing);
            #endregion

            #region lists

            signalInfos.Add(new SignalInfo
            {
                Id = 0,
                Name = "Minutes_001",
                IsActive = true,
                SignalJson = string.Empty,
                Value = OrderSignal.doNothing,
                InternalState = string.Empty,
                SignalType = typeof(Sig9)
            });
            //signalInfos.Add(new SignalInfo
            //{
            //    Id = 1,
            //    Name = "ITrend",
            //    IsActive = true,
            //    SignalJson = string.Empty,
            //    Value = OrderSignal.doNothing,
            //    InternalState = string.Empty,
            //    SignalType = typeof(ITrendSignal)
            //});


            //foreach (SignalInfo s in signalInfos)
            //{
            //    s.IsActive = false;
            //    if (s.Id == LiveSignalIndex)
            //    {
            //        s.IsActive = true;
            //    }
            //}

            #endregion
            #region "15 Minute"
            // define our 15 minute consolidator
            //var fifteenMinuteConsolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(15));

            // if we want to make decisions every 15 minutes as well, we can add an event handler
            // to the DataConsolidated event
            //fifteenMinuteConsolidator.DataConsolidated += OnFiftenMinuteAAPL;

            //trend15Min = new InstantaneousTrend(3);
            //RegisterIndicator(symbol, trend15Min, fifteenMinuteConsolidator, Field.Close);

            //int fast = 15;

            //int slow = 30;

            //// define our EMA, we'll manually register this, so we aren't using the helper function 'EMA(...)'
            //var fastEmaOnFifteenMinuteBars = new ExponentialMovingAverage("AAPL_EMA15", fast);
            //var slowEmaOnFifteenMinuteBars = new ExponentialMovingAverage("AAPL_EMA30", slow);

            //// register our indicator and consolidator together. this will wire the consolidator up to receive
            //// data for the specified symbol, and also set up the indicator to receive its data from the consolidator
            //RegisterIndicator("AAPL", fastEmaOnFifteenMinuteBars, fifteenMinuteConsolidator, Field.Close);
            //RegisterIndicator("AAPL", slowEmaOnFifteenMinuteBars, fifteenMinuteConsolidator, Field.Close);
            #endregion

            // for use with Tradier. Default is IB.
            //var security = Securities[symbol];
            //security.TransactionModel = new ConstantFeeTransactionModel(1.0m);

        }
        #region "15 minute events here:"
        //public void OnFiftenMinuteAAPL(object sender, TradeBar data)
        //{

        //    if (barcount == 375)
        //        comment = "";
        //    if (!trend15Min.IsReady)
        //        return;
        //    KeyValuePair<Symbol, TradeBar> kvp = new KeyValuePair<Symbol, TradeBar>(data.Symbol, data);
        //    List<SignalInfo> signalInfos15 = new List<SignalInfo>(signalInfos.Where(s => s.Name == "Minutes_015"));
        //    if (signalInfos15.Any())
        //    {
        //        GetOrderSignals(kvp, signalInfos15);
        //        foreach (var signalInfo15 in signalInfos15)
        //        {
        //            if (signalInfo15.Value != OrderSignal.doNothing)
        //            {

        //                if (CanMakeTrade)
        //                {
        //                    MinuteDataActivated = true;
        //                }
        //                else
        //                {
        //                    MinuteDataActivated = false;
        //                }
        //                if (Time.Hour == 15 && Time.Minute > 44 || Time.Hour == 16)
        //                {
        //                    MinuteDataActivated = true;
        //                }
        //            }
        //        }
        //    }
        //    #region "biglog"

        //    string logmsg =
        //        string.Format(
        //            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
        //            ",{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35},{36},{37},{38},{39}",
        //            Time,
        //            barcount,
        //            data.Open,
        //            data.High,
        //            data.Low,
        //            data.Close,
        //            data.EndTime,
        //            data.Period,
        //            data.DataType,
        //            data.IsFillForward,
        //            data.Time,
        //            data.Symbol,
        //            data.Price,
        //            "",
        //            Time.ToShortTimeString(),
        //            Price[0].Value,
        //            trend15Min.Current.Value,
        //            comment,
        //            orderSignal,
        //            sharesOwned,
        //            Portfolio.TotalUnrealisedProfit,
        //            tradenet,
        //            Portfolio.TotalPortfolioValue,
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            "",
        //            ""
        //            );
        //    mylog.Debug(logmsg);

        //    #endregion
        //}
        #endregion
        #region "one minute events"
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
        private void OnDataForSymbol(KeyValuePair<Symbol, TradeBar> data)
        {
            #region logging

            comment = string.Empty;
            tradingDate = this.Time;

            #endregion

            barcount++;
            // Add the history for the bar
            var time = this.Time;

            Price.Add(idp(time, (data.Value.Close + data.Value.Open) / 2));

            // Update the indicators
            trend.Update(idp(time, Price[0].Value));

            List<SignalInfo> signalInfosMinute = new List<SignalInfo>(signalInfos.Where(s => s.Name == "Minutes_001"));
            if (signalInfosMinute.Any())
            {
                GetOrderSignals(data, signalInfosMinute);
                foreach (var signalInfo001 in signalInfosMinute)
                {
                    OrderSignal signal = signalInfo001.Value;
                    SellOutAtEndOfDay(data, ref signal);
                    signalInfo001.Value = signal;
                    if (signalInfo001.Value != OrderSignal.doNothing)
                    {
                        ExecuteStrategy(symbol, signalInfo001, data);
                    }

                }
            }
            sharesOwned = Portfolio[data.Key].Quantity;
            #region "biglog"

            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
                    ",{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32}",
                    time,
                    barcount,
                    data.Value.Volume,
                    data.Value.Open,
                    data.Value.High,
                    data.Value.Low,
                    data.Value.Close,
                    "",
                    "",
                    time.ToShortTimeString(),
                    signalInfos[0].Price[0].Value,
                    signalInfos[0].trend.Current.Value,
                    signalInfos[0].nTrig,
                    signalInfos[0].Value,
                    comment,
                    "",
                    nEntryPrice,
                    signalInfos[0].IsActive,
                    Portfolio.TotalUnrealisedProfit,
                    orderId,
                    sharesOwned,
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
                    ""
                    );
            mylog.Debug(logmsg);

            minuteReturns.AppendFormat(",{0}", Math.Round(Portfolio.TotalPortfolioValue, 2));
            if (minuteHeaderFlag)
            {
                minuteHeader.AppendFormat(",{0}", data.Value.EndTime.ToLongTimeString());
            }
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;
            #endregion
            // At the end of day, reset the trend and trendHistory
            if (time.Hour == 16)
            {
                minuteReturns.AppendLine(string.Format(@",{0}", CalculateDailyProfits()));
                minuteReturns.AppendFormat("{0},{1}", symbol, this.Time.AddDays(1).ToShortDateString());
                if (minuteHeaderFlag)
                {
                    minuteHeader.AppendLine(",P/L");
                    minuteHeaderFlag = false;
                }
                barcount = 0;
                iTrendSignal.Reset();
            }
        }


        #endregion

        /// <summary>
        /// Run the strategy associated with this algorithm
        /// </summary>
        /// <param name="data">TradeBars - the data received by the OnData event</param>
        public void GetOrderSignals(KeyValuePair<Symbol, TradeBar> data, List<SignalInfo> signalInfos)
        {
            #region "GetOrderSignals Execution"
            nEntryPrice = 0;
            nExitPrice = 0;
            List<ProformaOrderTicket> handledTickets = null;

            #region lists
            foreach (SignalInfo info in signalInfos)
            {
                if (info.Id == 1)  // ITrend
                {
                    handledTickets = HandleTickets();
                    iTrendSignal.ITrend.Update(Price[0]);
                    if (LastOrderSent[symbol] == OrderSignal.goLong || LastOrderSent[symbol] == OrderSignal.goShort)
                    {
                        CheckOrderStatus(symbol, LastOrderSent[symbol]);
                    }
                    info.Value = iTrendSignal.CheckSignal(data.Value.Close);
                    handledTickets = null;

                }
                if (info.Id == 0)   // Minutes_001
                {

                    var id = info.Id;
                    info.Value = OrderSignal.doNothing;

                    Type t = info.SignalType;
                    var sig = Activator.CreateInstance(t) as ISigSerializable;
                    if (sig != null)
                    {
                        handledTickets = HandleTickets();
                        if (info.IsActive)
                        {
                            sig.symbol = data.Key;
                            sig.Deserialize(info.SignalJson);
                            sig.Barcount = barcount; // for debugging

                            decimal entryPrice = sig.nEntryPrice;
                            // set the properties from the handled ticket.

                            var handledTicket = handledTickets.FirstOrDefault();
                            if (handledTicket != null)
                            {
                                switch (handledTicket.Status)
                                {
                                    // sig.orderFilled defaults to true in the three constructors
                                    case OrderStatus.Filled:
                                    case OrderStatus.PartiallyFilled:
                                        sig.orderFilled = true;
                                        entryPrice = Portfolio[symbol].HoldStock ? handledTicket.AverageFillPrice : 0;
                                        _ticketsQueue.Remove(_ticketsQueue.FirstOrDefault(z => z.OrderId == handledTicket.OrderId));
                                        break;
                                    case OrderStatus.Canceled:
                                    case OrderStatus.Invalid:
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
                                {"trend", trend.Current.Value.ToString(CultureInfo.InvariantCulture)},
                                {"lossThreshhold", lossThreshhold.ToString(CultureInfo.InvariantCulture)},
                                {"UnrealizedProfit", Portfolio[symbol].UnrealizedProfit.ToString(CultureInfo.InvariantCulture)}
                            };


                            info.Value = sig.CheckSignal(data, paramlist, out comment);
                            info.nTrig = sig.nTrig;

                            info.Comment = comment;

                            if (Time.Hour == 16)
                            {
                                sig.Reset();
                            }
                            info.SignalJson = sig.Serialize();
                            info.InternalState = sig.GetInternalStateFields().ToString();

                        }
                        else
                        {
                            info.Comment = "Signal is null";
                        }
                        handledTickets = null;

                    }
                }
            }
            #endregion  // lists

            #endregion  // execution
        }

        /// <summary>
        /// Checks if the limits order are filled, and updates the ITrenStrategy object and the
        /// LastOrderSent dictionary.
        /// If the limit order aren't filled, then cancels the order and send a market order.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="lastOrder">The last order.</param>
        private void CheckOrderStatus(string symbol, OrderSignal lastOrder)
        {
            int shares;

            // If the ticket isn't filled...
            if (_ticketsQueue.Last().Status != OrderStatus.Filled)
            {
                shares = _ticketsQueue.Last().Quantity;
                // cancel the limit order and send a new market order.
                _ticketsQueue.Last().Cancel();
                _ticketsQueue.Add(MarketOrder(symbol, shares));
            }
            // Once the ticket is filled, update the ITrenStrategy object for the symbol.
            if (lastOrder == OrderSignal.goLong)
            {
                iTrendSignal.Position = StockState.longPosition;
            }
            else if (lastOrder == OrderSignal.goShort)
            {
                iTrendSignal.Position = StockState.shortPosition;
            }
            iTrendSignal.EntryPrice = _ticketsQueue.Last().AverageFillPrice;
            // Update the LastOrderSent dictionary, to avoid check filled orders many times.
            LastOrderSent[symbol] = OrderSignal.doNothing;

            // TODO: If the ticket is partially filled.
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
                    tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId && t.Status == orderEvent.Status);
                    if (tickets != null)
                    {
                        foreach (OrderTicket ticket in tickets)
                        {
                            int infoId = Convert.ToInt32(ticket.Tag);
                            SignalInfo si = signalInfos.FirstOrDefault(f => f.Id == infoId);
                            if (si != null)
                                si.IsActive = true;
                        }
                    }
                    break;
                case OrderStatus.Filled:
                case OrderStatus.PartiallyFilled:
                    tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId && t.Status == orderEvent.Status);
                    if (tickets != null)
                    {
                        foreach (OrderTicket ticket in tickets)
                        {
                            int infoId = Convert.ToInt32(ticket.Tag);
                            SignalInfo si = signalInfos.FirstOrDefault(f => f.Id == infoId);
                            if (si != null)
                                si.IsActive = true;

                            #region "log the ticket as a OrderTransacton"
                            OrderTransactionFactory transactionFactory = new OrderTransactionFactory((QCAlgorithm)this);
                            OrderTransaction t = transactionFactory.Create(orderEvent, ticket, false);
                            _transactions.Add(t);
                            _orderTransactionProcessor.ProcessTransaction(t);
                            _tradecount++;
                            if (_orderTransactionProcessor.TotalProfit != totalProfit)
                            {
                                CalculateTradeProfit(t.Symbol);
                            }
                            totalProfit = _orderTransactionProcessor.TotalProfit;
                            #endregion

                        }
                    }
                    break;
            }
        }

        #endregion

        #region "Profit Calculations for logging"
        private void CalculateTradeProfit(Symbol symbol)
        {
            tradenet = _orderTransactionProcessor.CalculateLastTradePandL(symbol);
        }
        private decimal CalculateDailyProfits()
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

            return todayNet;
        }
        #endregion

        #region "Methods"
        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="signalInfo">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(Symbol symbol, SignalInfo signalInfo, KeyValuePair<Symbol, TradeBar> data)
        {
            decimal limitPrice = 0m;
            int shares = PositionShares(symbol, signalInfo);
            if (shares == 0)
                return;
            ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
            OrderTicket ticket;
            switch (signalInfo.Value)
            {
                case OrderSignal.goLongLimit:
                    // Define the limit price.
                    signalInfo.IsActive = false;
                    limitPrice = priceCalculator.Calculate(data.Value, signalInfo, RngFac);
                    ticket = LimitOrder(symbol, shares, limitPrice, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                    _ticketsQueue.Add(ticket);
                    break;

                case OrderSignal.goShortLimit:
                    signalInfo.IsActive = false;
                    limitPrice = priceCalculator.Calculate(data.Value, signalInfo, RngFac);
                    ticket = LimitOrder(symbol, shares, limitPrice, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                    _ticketsQueue.Add(ticket);
                    break;

                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    signalInfo.IsActive = false;
                    ticket = MarketOrder(symbol, shares, false, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                    _ticketsQueue.Add(ticket);
                    break;

                default: break;
            }
        }


        /// <summary>
        /// This function is called prior to calling the GetOrderSignals to check to see if a ticket was filled
        /// If the ticket did not fill within one bar, cancel it and assume the market moved away from the limit order
        /// 
        /// It re
        /// </summary>

        private List<ProformaOrderTicket> HandleTickets()
        {
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

                        switch (liveticket.Status)
                        {
                            case OrderStatus.Canceled:
                            case OrderStatus.New:
                            case OrderStatus.None:
                                break;
                            case OrderStatus.Invalid:
                                proformaLiveTicket.ErrorMessage = liveticket.GetMostRecentOrderResponse().ErrorMessage;
                                break;
                            case OrderStatus.Submitted:
                                liveticket.Cancel();
                                proformaLiveTicket.Status = OrderStatus.Canceled;
                                proformaLiveTicket.QuantityFilled = 0; // they are probably already 0
                                proformaLiveTicket.AverageFillPrice = 0;
                                break;
                            case OrderStatus.Filled:
                            case OrderStatus.PartiallyFilled:

                                #region logging
                                if (Portfolio[symbol].Invested)
                                {
                                    nEntryPrice = Portfolio[symbol].IsLong ? liveticket.AverageFillPrice : liveticket.AverageFillPrice * -1;
                                    nExitPrice = 0;
                                }
                                else
                                {
                                    nExitPrice = liveticket.AverageFillPrice;
                                    nEntryPrice = 0;
                                }
                                #endregion
                                proformaLiveTicket.Direction = liveticket.Quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell;
                                proformaLiveTicket.Status = OrderStatus.Filled;
                                proformaLiveTicket.QuantityFilled = (int)liveticket.QuantityFilled;
                                proformaLiveTicket.AverageFillPrice = liveticket.AverageFillPrice;
                                break;
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

            return quantity;
        }

        /// <summary>
        /// Sells out all positions at 15:50, and calculates the profits for the day
        ///  emails the transactions for the day to me
        /// </summary>
        /// <param name="data">TradeBars - the data</param>
        /// <param name="signalInfosMinute"></param>
        /// <returns>false if end of day, true during the day </returns>
        private void SellOutAtEndOfDay(KeyValuePair<Symbol, TradeBar> data, ref OrderSignal signal)
        {
            if (shouldSellOutAtEod)
            {
                #region logging
                if (Time.Hour == 16)
                {

                    sharesOwned = Portfolio[symbol].Quantity;
                    #region logging

                    SendTransactionsToFile();
                    #endregion

                    NotifyUser();
                }
                #endregion

                if (Time.Hour == 15 && Time.Minute > 45)
                {
                    signal = OrderSignal.doNothing;
                    if (Portfolio[symbol].IsLong)
                    {
                        signal = OrderSignal.goShort;
                    }
                    if (Portfolio[symbol].IsShort)
                    {
                        signal = OrderSignal.goLong;
                    }
                }
            }
        }

        private void NotifyUser()
        {
            #region logging

            if (this.Time.Hour == 16)
            {
                sharesOwned = Portfolio[symbol].Quantity;
                var _transactionsAsCsv = CsvSerializer.Serialize<OrderTransaction>(",", _transactions, true);
                StringBuilder sb = new StringBuilder();
                foreach (string s in _transactionsAsCsv)
                    sb.AppendLine(s);
                string attachment = sb.ToString();
                Notify.Email("nicholasstein@cox.net",
                    string.Format("Todays Date: {0} \nNumber of Transactions: {1}", Time.ToLongDateString(),
                        _transactions.Count()),
                    attachment);
                var _tradesAsCsv = CsvSerializer.Serialize<MatchedTrade>(",",
                    _orderTransactionProcessor.Trades.Where(f => f.DateAcquired == tradingDate), true);
                sb = new StringBuilder();
                foreach (string s in _tradesAsCsv)
                {
                    sb.AppendLine(s);
                }
                attachment = sb.ToString();
                Notify.Email("nicholasstein@cox.net",
                    string.Format("Todays Date: {0} \nNumber of Trades: {1}", Time.ToLongDateString(), _tradesAsCsv.Count()),
                    attachment);
                //SendTradesToFile("trades.csv", _orderTransactionProcessor.Trades);
                _transactions = new List<OrderTransaction>();
            }

            #endregion
        }
        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            Logging.Log.Trace(string.Format("\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n lossThreshhold = {3}\n Start Time: {4}\n End Time: {5}", this.GetType().Name, symbol, Portfolio.TotalPortfolioValue, lossThreshhold, startTime, DateTime.Now));
            #region logging
            NotifyUser();
            using (
                StreamWriter sw =
                    new StreamWriter(string.Format(@"{0}Logs\{1}.csv", AssemblyLocator.ExecutingDirectory(), symbol)))
            {
                sw.Write(minuteHeader.ToString());
                sw.Write(minuteReturns.ToString());
                sw.Flush();
                sw.Close();
            }

            #endregion
        }

        //private IndicatorDataPoint CalculateNewTrendHistoryValue(int barcount, DateTime time, RollingWindow<IndicatorDataPoint> price, InstantaneousTrend tr)
        //{
        //    //if (barcount < 7 && barcount > 2)
        //    //{
        //    //    return (idp(time, (price[0].Value + 2 * price[1].Value + price[2].Value) / 4));
        //    //}
        //    //else
        //    //{
        //    //    return (idp(time, tr.Current.Value)); //add last iteration value for the cycle
        //    //}
        //    if (!trendHistory.IsReady)
        //        return idp(time, price[0].Value);
        //    return (idp(time, tr.Current.Value)); //add last iteration value for the cycle
        //}

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
