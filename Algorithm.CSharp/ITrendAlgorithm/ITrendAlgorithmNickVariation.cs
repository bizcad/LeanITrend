using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities.Equity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    public class ITrendAlgorithmNickVariation : QCAlgorithm
    {
        #region "Algorithm Globals"
        DateTime startTime = DateTime.Now;
        //private DateTime _startDate = new DateTime(2015, 8, 10);
        //private DateTime _endDate = new DateTime(2015, 8, 14);
        private DateTime _startDate = new DateTime(2015, 5, 19);
        private DateTime _endDate = new DateTime(2015, 11, 3);
        private decimal _portfolioAmount = 26000;

        #endregion

        #region Fields

        /* +-------------------------------------------------+
     * |Algorithm Control Panel                          |
     * +-------------------------------------------------+*/
        private static int ITrendPeriod = 15;            // Instantaneous Trend period.
        private static decimal Tolerance = 0.0001m;      // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m;     // Percentage tolerance before revert position.

        private static decimal maxLeverage = 1m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.25m;         // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.

        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.

        private bool resetAtEndOfDay = true;            // Reset the strategies at EOD.
        private bool noOvernight = true;                // Close all positions before market close.
        private decimal lossThreshhold = -55;           // When unrealized losses fall below, revert position
        /* +-------------------------------------------------+*/

        //private static string[] Symbols = { "NFLX" };
        private List<Symbol> Symbols = new List<Symbol>();
        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, ITrendStrategy> Strategy = new Dictionary<string, ITrendStrategy>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        private EquityExchange theMarket = new EquityExchange();
        #region "Custom Logging"
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        //private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");
        private readonly OrderTransactionFactory _orderTransactionFactory;

        private string ondataheader =
            @"Symbol,Time,BarCount,Volume, Open,High,Low,Close,,,Time,Price,Trend, Trigger, orderSignal, Comment,, EntryPrice, Exit Price,Unrealized,Order Id, Owned, TradeNet, Portfolio";

        private SigC _scig5C = new SigC();

        private string json;


        private string dailyheader = @"Trading Date,Daily Profit, Portfolio Value";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private List<OrderTransaction> _transactions;
        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();
        //private List<OrderTicket> _ticketsQueue;

        private List<OrderTransaction> _proformatransactions;


        private List<OrderEvent> _orderEvents = new List<OrderEvent>();
        private int _tradecount = 0;
        private string comment;
        private DateTime tradingDate;
        private int sharesOwned = 0;

        #endregion

        #endregion Fields

        #region Logging stuff - Defining

        public List<StringBuilder> stockLogging = new List<StringBuilder>();
        public StringBuilder portfolioLogging = new StringBuilder();
        private int barCounter = 0;

        #endregion Logging stuff - Defining

        #region QCAlgorithm methods

        public override void Initialize()
        {
            SetStartDate(_startDate);               //Set Start Date
            SetEndDate(_endDate);                   //Set End Date
            SetCash(_portfolioAmount);              //Set Strategy Cash
            #region Nick logging
            var algoname = this.GetType().Name;
            mylog.Debug(algoname);

            mylog.Debug(ondataheader);
            dailylog.Debug(algoname);
            dailylog.Debug(dailyheader);
            _proformatransactions = new List<OrderTransaction>();
            string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
            #endregion
            #region Logging stuff - Initializing Portfolio Logging

            portfolioLogging.AppendLine("Counter, Time, Portfolio Value");
            int i = 0;  // Only used for logging.

            #endregion Logging stuff - Initializing Portfolio Logging

            #region "Read Symbols from File"
            /**********************************************
             THIS SECTION IS FOR READING SYMBOLS FROM A FILE
            ************************************************/
            //string symbols;
            

            var filename = AssemblyLocator.ExecutingDirectory() + "symbols.txt";
            using (StreamReader sr = new StreamReader(filename))
            {
                string[] symbols = { };
                var readLine = sr.ReadLine();
                if (readLine != null) symbols = readLine.Split(',');

                foreach (string t in symbols)
                {
                    Symbols.Add(new Symbol(t));
                }

                sr.Close();
            }
            // Make sure the list contains the static symbol
            //if (!Symbols.Contains(symbol))
            //{
            //    Symbols.Add(symbol);
            //}
            #endregion
            

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                var priceIdentity = Identity(symbol, selector: Field.Close);

                Strategy.Add(symbol, new ITrendStrategy(priceIdentity, ITrendPeriod,
                    Tolerance, RevertPCT));
                // Equally weighted portfolio.
                //ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
                ShareSize.Add(symbol, .58m);


                #region Logging stuff - Initializing Stock Logging

                stockLogging.Add(new StringBuilder());
                stockLogging[i].AppendLine("Counter, Time, Close, ITrend, Trigger," +
                    "Momentum, EntryPrice, Signal," +
                    "TriggerCrossOverITrend, TriggerCrossUnderITrend, ExitFromLong, ExitFromShort," +
                    "StateFromStrategy, StateFromPorfolio, Portfolio Value");
                i++;

                #endregion Logging stuff - Initializing Stock Logging
            }
        }

        public void OnData(TradeBars data)
        {
            barCounter++; // just for debug

            #region logging

            comment = string.Empty;
            tradingDate = this.Time;

            #endregion
            bool isMarketAboutToClose = !theMarket.DateTimeIsOpen(Time.AddMinutes(10));
            OrderSignal actualOrder = OrderSignal.doNothing;

            int i = 0;
            foreach (string symbol in Symbols)
            {
                // Operate only if the market is open
                if (theMarket.DateTimeIsOpen(Time))
                {
                    // First check if there are some limit orders not filled yet.
                    if (Transactions.LastOrderId > 0)
                    {
                        CheckLimitOrderStatus(symbol, data);
                    }
                    // Check if the market is about to close and noOvernight is true.
                    if (noOvernight && isMarketAboutToClose)
                    {
                        actualOrder = ClosePositions(symbol);
                    }
                    else
                    {
                        // Now check if there is some signal and execute the strategy.
                        actualOrder = Strategy[symbol].ActualSignal;
                    }
                    ExecuteStrategy(symbol, actualOrder);

                    sharesOwned = Portfolio[symbol].Quantity;

                }

                #region Logging stuff - Filling the data StockLogging

                //"Counter, Time, Close, ITrend, Trigger," +
                //"Momentum, EntryPrice, Signal," +
                //"TriggerCrossOverITrend, TriggerCrossUnderITrend, ExitFromLong, ExitFromShort," +
                //"StateFromStrategy, StateFromPorfolio, Portfolio Value"
                string newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}",
                                               barCounter,
                                               Time,
                                               (data[symbol].Close + data[symbol].Open) / 2,
                                               Strategy[symbol].ITrend.Current.Value,
                                               Strategy[symbol].ITrend.Current.Value + Strategy[symbol].ITrendMomentum.Current.Value,
                                               Strategy[symbol].ITrendMomentum.Current.Value,
                                               (Strategy[symbol].EntryPrice == null) ? 0 : Strategy[symbol].EntryPrice,
                                               actualOrder,
                                               Strategy[symbol].TriggerCrossOverITrend.ToString(),
                                               Strategy[symbol].TriggerCrossUnderITrend.ToString(),
                                               Strategy[symbol].ExitFromLong.ToString(),
                                               Strategy[symbol].ExitFromShort.ToString(),
                                               Strategy[symbol].Position.ToString(),
                                               Portfolio[symbol].Quantity.ToString(),
                                               Portfolio.TotalPortfolioValue
                                               );
                stockLogging[i].AppendLine(newLine);
                i++;

                #endregion Logging stuff - Filling the data StockLogging
                #region "biglog"

                string logmsg =
                    string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
                        ",{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32}",
                        symbol,
                        data[symbol].EndTime,
                        barCounter,
                        data[symbol].Volume,
                        data[symbol].Open,
                        data[symbol].High,
                        data[symbol].Low,
                        data[symbol].Close,
                        "",
                        "",
                        data[symbol].EndTime.ToShortTimeString(),
                        data[symbol].Close,
                        Strategy[symbol].ITrend.Current.Value,
                        "",
                        Strategy[symbol].ActualSignal,
                        comment,
                        "",
                        Strategy[symbol].EntryPrice ?? 0,
                        "",
                        Portfolio.TotalUnrealisedProfit,
                        "",
                        sharesOwned,
                        "",
                        Portfolio.TotalPortfolioValue,
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


                //tradeprofit = 0;
                //tradefees = 0;
                //tradenet = 0;
                #endregion

            }
            if (tradingDate.Hour == 16)
            {
                barCounter = 0;
            }
        }

        private OrderSignal ClosePositions(string symbol)
        {
            OrderSignal actualOrder;
            if (Strategy[symbol].Position == StockState.longPosition) actualOrder = OrderSignal.closeLong;
            else if (Strategy[symbol].Position == StockState.shortPosition) actualOrder = OrderSignal.closeShort;
            else actualOrder = OrderSignal.doNothing;
            return actualOrder;
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            string symbol = orderEvent.Symbol;
            int portfolioPosition = Portfolio[symbol].Quantity;
            var actualTicket = Transactions.GetOrderTickets(t => t.OrderId == orderEvent.OrderId).Single();
            var actualOrder = Transactions.GetOrderById(orderEvent.OrderId);

            switch (orderEvent.Status)
            {
                case OrderStatus.Submitted:
                    Strategy[symbol].Position = StockState.orderSent;
                    //Log("New order submitted: " + actualOrder.ToString());
                    break;

                case OrderStatus.PartiallyFilled:
                    //Log("Order partially filled: " + actualOrder.ToString());
                    //Log("Canceling order");
                    actualTicket.Cancel();
                    //do { }
                    //while (actualTicket.GetMostRecentOrderResponse().IsSuccess);
                    goto case OrderStatus.Filled;

                case OrderStatus.Filled:
                    if (portfolioPosition > 0) Strategy[symbol].Position = StockState.longPosition;
                    else if (portfolioPosition < 0) Strategy[symbol].Position = StockState.shortPosition;
                    else Strategy[symbol].Position = StockState.noInvested;

                    Strategy[symbol].EntryPrice = actualTicket.AverageFillPrice;

                    //Log("Order filled: " + actualOrder.ToString());
                    break;

                case OrderStatus.Canceled:
                    //Log("Order successfully canceled: " + actualOrder.ToString());
                    break;

                default:
                    break;
            }
        }

        public override void OnEndOfDay()
        {
            if (resetAtEndOfDay)
            {
                foreach (string symbol in Symbols)
                {
                    Strategy[symbol].Reset();
                }
            }
        }

        public override void OnEndOfAlgorithm()
        {
            #region Logging stuff - Saving the logs

            int i = 0;
            foreach (string symbol in Symbols)
            {
                string filename = string.Format("ITrendDebug_{0}.csv", symbol);
                string filePath = @"C:\Users\JJ\Desktop\MA y señales\ITrend Debug\" + filename;
                // JJ do not delete this line it locates my engine\bin\debug folder
                //  I just uncomment it when I run on my local machine
                filePath = AssemblyLocator.ExecutingDirectory() + filename;

                if (File.Exists(filePath)) File.Delete(filePath);
                File.AppendAllText(filePath, stockLogging[i].ToString());
                Debug(string.Format("\nSymbol Name: {0}, Ending Value: {1} ", symbol, Portfolio[symbol].Profit));

            }

            StringBuilder sb = new StringBuilder();
            //sb.Append(" Symbols: ");
            foreach (var s in Symbols)
            {

                sb.Append(s.ToString());
                sb.Append(",");
            }
            string symbolsstring = sb.ToString();
            symbolsstring = symbolsstring.Substring(0, symbolsstring.LastIndexOf(",", System.StringComparison.Ordinal));
            string debugstring =
                string.Format(
                    "\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n lossThreshhold = {3}\n Start Time: {4}\n End Time: {5}",
                    this.GetType().Name, symbolsstring, Portfolio.TotalPortfolioValue, lossThreshhold, startTime,
                    DateTime.Now);
            Log(debugstring);
            #endregion Logging stuff - Saving the logs
        }

        #endregion QCAlgorithm methods

        #region Algorithm Methods

        /// <summary>
        /// Checks if the limits order are filled, and updates the ITrenStrategy object and the
        /// LastOrderSent dictionary.
        /// If the limit order aren't filled, then cancels the order and send a market order.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="lastOrder">The last order.</param>
        private void CheckLimitOrderStatus(string symbol, TradeBars data)
        {
            // Pick the submitted limit tickets for the symbol.
            var actualSubmittedTicket = Transactions.GetOrderTickets(t => t.Symbol == symbol
                                                              && t.OrderType == OrderType.Limit
                                                              && t.Status == OrderStatus.Submitted);
            // If there is none, return.
            if (actualSubmittedTicket.Count() == 0) return;
            // if there is more than one, stop the algorithm, something is wrong.
            else if (actualSubmittedTicket.Count() != 1) throw new ApplicationException("More than one submitted limit order");

            //Log("||| Cancel Limit order and send a market order");
            // Now, define the ticket to handle the actual OrderTicket.
            var actualTicket = actualSubmittedTicket.Single();

            //foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            //{
            //    if (kvp.Key == symbol)
            //    {
            //        if (actualTicket.Quantity > 0)
            //        {
            //            AlterLongLimit(kvp, actualTicket);
            //        }
            //        else
            //        {
            //            AlterShortLimit(kvp, actualTicket);
            //        }
            //    }
            //}
            /* Removed by Nick */
            // Retrieve the operation quantity. 
            int shares = actualTicket.Quantity;
            // Cancel the order.
            actualTicket.Cancel();
            // Send a market order.
            MarketOrder(symbol, shares);
            /* End Removed by Nick */

            // We just altered the price, so we do not do anything in this bar.
            Strategy[symbol].ActualSignal = OrderSignal.doNothing;
        }

        private void AlterLongLimit(KeyValuePair<Symbol, TradeBar> data, OrderTicket liveticket)
        {
            var limit = liveticket.Get(OrderField.LimitPrice);
            decimal newLimit = limit;
            //currentSignalInfo.TradeAttempts++;
            if (newLimit < data.Value.Low)
            {
                newLimit = data.Value.Close + 0.01m;
            }
            OrderResponse response = liveticket.Update(new UpdateOrderFields
            {
                LimitPrice = newLimit,
                Tag = "Update #" + (liveticket.UpdateRequests.Count + 1)
            });
            if (response.IsSuccess)
            {
                Log(string.Format("Long Order {0}. Status: {1} Updated {2} to new price {3}.", liveticket.OrderId, liveticket.Status, limit, newLimit));
            }
            else
            {
                if (!response.IsProcessed)
                {
                    Log(string.Format("Order {0} not yet processed to new price {1}", liveticket.OrderId, limit));
                }
                if (response.IsError)
                {
                    Log(response.ToString());
                }
            }
        }

        private void AlterShortLimit(KeyValuePair<Symbol, TradeBar> data, OrderTicket liveticket)
        {
            var limit = liveticket.Get(OrderField.LimitPrice);
            decimal newLimit = limit;
            //currentSignalInfo.TradeAttempts++;
            if (newLimit < data.Value.Low)
            {
                newLimit = data.Value.Close - 0.01m;
            }
            OrderResponse response = liveticket.Update(new UpdateOrderFields
            {
                LimitPrice = newLimit,
                Tag = "Update #" + (liveticket.UpdateRequests.Count + 1)
            });
            if (response.IsSuccess)
            {
                Log(string.Format("Long Order {0}. Status: {1} Updated {2} to new price {3}.", liveticket.OrderId, liveticket.Status, limit, newLimit));
            }
            else
            {
                if (!response.IsProcessed)
                {
                    Log(string.Format("Order {0} not yet processed to new price {1}", liveticket.OrderId, limit));
                }
                if (response.IsError)
                {
                    Log(response.ToString());
                }
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
                case OrderSignal.goLongLimit:
                    operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
                    quantity = Math.Min(maxOperationQuantity, operationQuantity);
                    break;

                case OrderSignal.goShort:
                case OrderSignal.goShortLimit:
                    operationQuantity = CalculateOrderQuantity(symbol, -ShareSize[symbol]);
                    quantity = Math.Max(-maxOperationQuantity, operationQuantity);
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
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="actualOrder">The actual order to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(string symbol, OrderSignal actualOrder)
        {
            if (barCounter >= 21)
                comment = "";
            // Define the operation size.
            int shares = PositionShares(symbol, actualOrder);

            if (shares == 0)
                actualOrder = OrderSignal.doNothing;

            switch (actualOrder)
            {
                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.goLongLimit:
                case OrderSignal.goShortLimit:
                    //Log("===> Entry to Market");
                    decimal limitPrice;
                    var barPrices = Securities[symbol];

                    // Define the limit price.
                    if (actualOrder == OrderSignal.goLong ||
                        actualOrder == OrderSignal.goLongLimit)
                    {
                        limitPrice = Math.Max(barPrices.Low,
                                    (barPrices.Close - (barPrices.High - barPrices.Low) * RngFac));
                    }
                    else
                    {
                        limitPrice = Math.Min(barPrices.High,
                                    (barPrices.Close + (barPrices.High - barPrices.Low) * RngFac));
                    }
                    // Send the order.
                    LimitOrder(symbol, shares, limitPrice);
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    //Log("<=== Closing Position");
                    // Send the order.
                    MarketOrder(symbol, shares);
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    //Log("<===> Reverting Position");
                    // Send the order.
                    MarketOrder(symbol, shares);
                    break;

                default: break;
            }
        }



        #endregion Algorithm Methods
    }
}
