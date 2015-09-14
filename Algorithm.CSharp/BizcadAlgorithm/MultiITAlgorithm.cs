using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Algorithm.CSharp.BizcadAlgorithm;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Algorithm.CSharp.ITrendAlgorithm;
using QuantConnect.Algorithm.Examples;
using QuantConnect.Brokerages.Backtesting;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Interfaces;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class MultiITAlgorithm : QCAlgorithm
    {
        #region "Variables"

        #region "Algorithm Globals"
        private DateTime _startDate = new DateTime(2015, 9, 8);
        private DateTime _endDate = new DateTime(2015, 9, 10);
        private decimal _portfolioAmount = 10000;
        private decimal _transactionSize = 15000;
        #endregion
        #region "Algorithm Control Panel"
        /* +--------------------------------------------+
        *  + Algorithm Control Panel                    +
        *  +--------------------------------------------+*/
        private static int ITrendPeriod = 7;            // Instantaneous Trend period.
        private static decimal Tolerance = 0.000m;      // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m;     // Percentage tolerance before revert position.

        private static decimal maxLeverage = 1m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.00m;         // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.

        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.

        private bool resetAtEndOfDay = true;            // Reset the strategies at EOD.
        private bool noOvernight = true;                // Close all positions before market close.
        /* +-------------------------------------------+*/
        #endregion

        private string symbol = "AAPL";
        private static string[] Symbols = { "AAPL" };
        //private static string[] Symbols = { "AIG", "BAC", "IBM", "SPY" };

        private int barcount = 0;

        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;
        #region lists

        // lists
        private Dictionary<int, RollingWindow<IndicatorDataPoint>> trendHistoryList;
        private Dictionary<int, MultiITStrategy> strategyList;
        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, MultiITStrategy> Strategy = new Dictionary<string, MultiITStrategy>();
        private Dictionary<int, InstantaneousTrend> trendList;
        private Dictionary<int, decimal> entryPriceList;

        // Dictionary used to store the Lists of OrderTickets object for each symbol.
        private Dictionary<string, List<OrderTicket>> Tickets = new Dictionary<string, List<OrderTicket>>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        // Dictionary used to store the last operation for each symbol.
        private Dictionary<string, OrderSignal> LastOrderSent = new Dictionary<string, OrderSignal>();

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
        private decimal nEntryPrice1 = 0;
        private decimal nExitPrice = 0;

        private Maximum MaxDailyProfit;
        private Minimum MinDailyProfit;


        #endregion
        #region "Custom Logging"
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        //private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");
        private List<OrderTransaction> _transactions;

        private string ondataheader = @"Time,BarCount,trade size,Open,High,Low,Close,Time,Price,comment,signal, Entry Price, Exit Price,orderId , unrealized, shares owned,trade profit, trade fees, trade net, Portfolio Value";
        private string dailyheader = @"Trading Date,Daily Profit, Daily Fees, Daily Net, Cum profit, Cum Fees, Cum Net, Trades/day, Portfolio Value, Shares Owned";
        //private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private string comment;

        #endregion

        // strategyList
        private InstantTrendStrategy iTrendStrategy;
        private bool shouldSellOutAtEod = true;
        private int orderId = 0;
        private int tradesize;
        private OrderSignal signal;

        #endregion
        #region ProForma

        private BrokerSimulator _brokerSimulator;

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



            var days = _endDate.Subtract(_startDate).TotalDays;
            MaxDailyProfit = new Maximum("MaxDailyProfit", (int)days);
            MinDailyProfit = new Minimum("MinDailyProfit", (int)days);
            #endregion

            //Initialize dates
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);

            //Add as many securities as you like. All the data will be passed into the event handler:
            //AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

            // Initialize the Symbol indexed dictionaries
            foreach (string s in Symbols)
            {
                AddSecurity(SecurityType.Equity, s, Resolution.Minute);
                Strategy.Add(symbol, new MultiITStrategy(s, ITrendPeriod, this));
                Tickets.Add(s, new List<OrderTicket>());
                // Equal portfolio shares for every stock.
                ShareSize.Add(s, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
                LastOrderSent.Add(s, OrderSignal.doNothing);

                #region Logging stuff - Initializing Stock Logging

                //stockLogging.Add(new StringBuilder());
                //stockLogging[i].AppendLine("Counter, Time, Close, ITrend, Trigger," +
                //    "Momentum, EntryPrice, Signal," +
                //    "TriggerCrossOverITrend, TriggerCrossUnderITrend, ExitFromLong, ExitFromShort," +
                //    "StateFromStrategy, StateFromPorfolio, Portfolio Value");
                //i++;

                #endregion Logging stuff - Initializing Stock Logging
            }

            // Indicators
            Price = new RollingWindow<IndicatorDataPoint>(14);      // The price history

            // ITrend
            trend = new InstantaneousTrend("Main", 7, .25m);
            trendHistory = new RollingWindow<IndicatorDataPoint>(14);

            // The ITrendStrategy
            iTrendStrategy = new InstantTrendStrategy(symbol, 14, this);
            iTrendStrategy.ShouldSellOutAtEod = shouldSellOutAtEod;

            #region lists
            // Initialize the lists for the strategies
            trendList = new Dictionary<int, InstantaneousTrend>();
            trendHistoryList = new Dictionary<int, RollingWindow<IndicatorDataPoint>>();
            strategyList = new Dictionary<int, MultiITStrategy>();
            entryPriceList = new Dictionary<int, decimal>();

            int listIndex = 0;
            for (decimal d = .25m; d < .26m; d += .01m)
            {
                trendList.Add(listIndex, new InstantaneousTrend("ITrend_" + d, 7, d));  // eg ITrend.25, period 7, alpha .25
                trendHistoryList.Add(listIndex, new RollingWindow<IndicatorDataPoint>(4));
                strategyList.Add(listIndex, new MultiITStrategy(symbol, 7, this));
                entryPriceList.Add(listIndex, 0);
                listIndex++;
            }

            #endregion
            #region Proforma

            _brokerSimulator = new BrokerSimulator(this);



            #endregion
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
            // Logs a TradeBar to the mylog
            TradeBar tradebar;
            List<TradeBar> list = new List<TradeBar>();
            foreach (var item in data.Values)
            {
                list.Add( new TradeBar(item.EndTime,item.Symbol,item.Open,item.High,item.Low,item.Close,item.Volume,null));
                
            }
            string output = JsonConvert.SerializeObject(list);

            


            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "BrokerSimulatorTestData.json";
            if (File.Exists(pathname)) File.Delete(pathname);
            using (StreamWriter sw = new StreamWriter(pathname))
            {
                sw.Write(output);
                sw.Flush();
                sw.Close();
            }
            
            

            
            _brokerSimulator.PricesWindow.Add(data);
            // Add the history for the bar
            var time = data.Time;
            Price.Add(idp(time, (data[symbol].Close + data[symbol].Open) / 2));

            //// Update the indicators
            trend.Update(idp(time, Price[0].Value));
            trendHistory.Add(CalculateNewTrendHistoryValue(barcount, time, Price, trend));
            #region lists

            foreach (var listitem in trendHistoryList)
            {
                trendList[listitem.Key].Update(idp(time, Price[0].Value));
                listitem.Value.Add(idp(time, CalculateNewTrendHistoryValue(barcount, time, Price, trendList[listitem.Key])));
            }

            #endregion
            if (Portfolio[symbol].Invested)
            {
                tradesize = Math.Abs(Portfolio[symbol].Quantity);
            }
            else
            {
                tradesize = (int)(_transactionSize / Convert.ToInt32(Price[0].Value + 1));
            }


            string matrix = GetTradingSignals(data);

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
                    signal,
                    nEntryPrice,
                    nEntryPrice1,
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
                trendHistory.Reset();
                iTrendStrategy.Reset();
                foreach (var r in trendHistoryList)
                {
                    r.Value.Reset();
                    strategyList[r.Key].Reset();
                }
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
        private string GetTradingSignals(TradeBars data)
        {

            #region "strategyList Execution"

            string ret = "";

            foreach (var s in Symbols)
            {
                if (SellOutEndOfDay(data))
                {
                    // if there were limit order tickets to cancel, wait a bar to execute the strategy
                    if (!CanceledUnfilledLimitOrder())
                    {
                        if (nEntryPrice != 0)
                            comment = "entryprice";

                        if (barcount == 1)
                            comment = "bar 1";
                        iTrendStrategy.Barcount = barcount;  // for debugging
                        iTrendStrategy.nEntryPrice = nEntryPrice;
                        iTrendStrategy.maketrade = true;
                        //signal = iTrendStrategy.ExecuteStrategy(data, tradesize, trend.Current, out comment);
                        //if (iTrendStrategy.trendHistory[0].Value != trendHistory[0].Value)
                        //    throw new Exception("Trend history not flowing through to strategy correctly.");

                        #region lists


                        StringBuilder sb = new StringBuilder();
                        StringBuilder sb2 = new StringBuilder();
                        StringBuilder sb3 = new StringBuilder();
                        foreach (var it in strategyList)
                        {
                            it.Value.Barcount = barcount;
                            it.Value.ShouldSellOutAtEod = shouldSellOutAtEod;
                            it.Value.nEntryPrice = entryPriceList[it.Key];  // inject the entry price
                            it.Value.nEntryPrice = nEntryPrice1;  // inject the entry price
                            OrderSignal current = it.Value.CheckSignal(data, tradesize, trendList[it.Key].Current, out comment);
                            //                            OrderSignal current = it.Value.CheckSignal(data, tradesize, trend.Current, out comment);
                            //entryPriceList[it.Key] = it.Value.nEntryPrice;  // save the entry price


                            var thcompareS = it.Value.trendHistory[0].Value;
                            var thcompareL = trendHistoryList[it.Key][0].Value;
                            //if (thcompareL != thcompareS)
                            //    throw new Exception("Trend history not flowing through strategy correctly.");
                            //if (signal != current)
                            //    comment = "signals not the same";

                            #region logging
                            sb.Append(((int)current).ToString(CultureInfo.InvariantCulture));
                            sb.Append(",");
                            sb2.Append(it.Value.sTrig);
                            sb2.Append(",");
                            sb3.Append(it.Value.trendHistory[0].Value);
                            sb3.Append(@",");
                            #endregion

                            if (current != OrderSignal.doNothing)
                            {
                                //ExecuteStrategy(s, entryPriceList[it.Key], current, data);
                                ExecuteStrategy(s, nEntryPrice, current, data);
                            }
                        }
                        ret = sb.ToString() + "," + sb2.ToString() + "," + sb3.ToString();

                        #endregion
                    }
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

            if (tickets.Count() > 1)
                throw new Exception("Multiple tickets in unfilled order");
            var ticket = tickets.FirstOrDefault();
            if (ticket != null)
            {
                ticket.Cancel();
                retval = true;
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

                        OrderReportFormatter reportFormatter = new OrderReportFormatter((QCAlgorithm)this);
                        var t = reportFormatter.ReportTransaction(orderEvent, ticket, false);
                        _transactions.Add(t);
                        _tradecount++;
                        #endregion


                        if (Portfolio[orderEvent.Symbol].Invested)
                        {
                            nEntryPrice = orderEvent.FillPrice;
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
        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="actualOrder">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(string symbol, decimal entryPrice, OrderSignal actualOrder, TradeBars data)
        {
            int shares;
            decimal limitPrice = 0m;

            switch (actualOrder)
            {
                case OrderSignal.goLong:
                    shares = PositionShares(symbol, actualOrder);
                    Tickets[symbol].Add(MarketOrder(symbol, shares));
                    Strategy[symbol].Position = StockState.shortPosition;
                    Strategy[symbol].nEntryPrice = entryPrice;
                    LastOrderSent[symbol] = actualOrder;
                    break;

                case OrderSignal.goShort:
                    shares = PositionShares(symbol, actualOrder);
                    Tickets[symbol].Add(MarketOrder(symbol, shares));
                    Strategy[symbol].Position = StockState.longPosition;
                    Strategy[symbol].nEntryPrice = entryPrice;
                    LastOrderSent[symbol] = actualOrder;
                    break;

                case OrderSignal.goLongLimit:

                    shares = PositionShares(symbol, actualOrder);

                    // Define the limit price.
                    //limitPrice = Math.Max(data[symbol].Low, (data[symbol].Close - (data[symbol].High - data[symbol].Low) * RngFac));
                    limitPrice = Math.Round(Math.Max(data[symbol].Low, (data[symbol].Close - (data[symbol].High - data[symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);

                    // Send the order.
                    Tickets[symbol].Add(LimitOrder(symbol, shares, limitPrice));
                    // Update the LastOrderSent dictionary.
                    LastOrderSent[symbol] = actualOrder;
                    break;

                case OrderSignal.goShortLimit:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);

                    // Define the limit price.
                    //limitPrice = Math.Min(data[symbol].High, (data[symbol].Close + (data[symbol].High - data[symbol].Low) * RngFac));
                    limitPrice = Math.Round(Math.Min(data[symbol].High, (data[symbol].Close + (data[symbol].High - data[symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);

                    // Send the order.
                    var security = Securities[symbol];
                    ProformaOrderTicket x = _brokerSimulator.LimitOrder(symbol, shares, limitPrice);
                   

                    Tickets[symbol].Add(LimitOrder(symbol, shares, limitPrice));
                    // Update the LastOrderSent dictionary.
                    LastOrderSent[symbol] = actualOrder;
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);
                    // Send the order.
                    Tickets[symbol].Add(MarketOrder(symbol, shares));
                    // Because the order is an synchronously market order, they'll fill
                    // immediately. So, update the ITrend strategy and the LastOrder Dictionary.
                    Strategy[symbol].Position = StockState.noInvested;
                    Strategy[symbol].nEntryPrice = entryPrice;
                    LastOrderSent[symbol] = OrderSignal.doNothing;
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);
                    // Send the order.
                    Tickets[symbol].Add(MarketOrder(symbol, shares));
                    // Beacuse the order is an synchronously market order, they'll fill
                    // immediately. So, update the ITrend strategy and the LastOrder Dictionary.
                    if (actualOrder == OrderSignal.revertToLong) Strategy[symbol].Position = StockState.longPosition;
                    else if (actualOrder == OrderSignal.revertToShort) Strategy[symbol].Position = StockState.shortPosition;
                    Strategy[symbol].nEntryPrice = Tickets[symbol].Last().AverageFillPrice;
                    LastOrderSent[symbol] = actualOrder;
                    break;

                default: break;
            }
        }



        /// <summary>
        /// Estimate number of shares, given a kind of operation.
        /// </summary>
        /// <param name="symbol">The symbol to operate.</param>
        /// <param name="order">The kind of order.</param>
        /// <returns>The signed number of shares given the operation.</returns>
        public int PositionShares(string symbol, OrderSignal order)
        {
            int quantity;
            int operationQuantity;

            switch (order)
            {
                case OrderSignal.goLong:
                    operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
                    quantity = Math.Min(maxOperationQuantity, operationQuantity);
                    quantity = tradesize;       // override for development
                    break;

                case OrderSignal.goShort:
                    operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
                    quantity = -Math.Min(maxOperationQuantity, operationQuantity);
                    quantity = -tradesize;       // override for development
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    quantity = -Portfolio[symbol].Quantity;
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    quantity = -2 * Portfolio[symbol].Quantity;
                    break;

                case OrderSignal.goLongLimit:
                    quantity = tradesize;
                    break;

                case OrderSignal.goShortLimit:
                    quantity = -tradesize;
                    break;

                default:
                    quantity = 0;
                    break;
            }
            return quantity;
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
            Debug(string.Format("\nAlgorithm Name: {0}\n Ending Portfolio Value: {1} ", this.GetType().Name, Portfolio.TotalPortfolioValue));

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
