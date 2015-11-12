using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// MSA implementation.
    /// </summary>
    public class MSAlgorithm : QCAlgorithm
    {
        #region Fields

        private DateTime _startDate = new DateTime(2015, 07, 1);
        private DateTime _endDate = new DateTime(2015, 9, 30);
        private decimal _initialCapital = 26000;
        private static string[] Symbols = { "AAPL", "AMZN", "JNJ", "MSFT", "JPM" };

        /* +-------------------------------------------------+
         * |Algorithm Control Panel                          |
         * +-------------------------------------------------+*/
        private const int PreviousDaysN = 20;
        private const int RunsPerDay = 5;
        private const int DecyclePeriod = 30;

        private const decimal MinimumRunThreshold = 0.005m;

        private const decimal maxLeverage = 4m;
        private decimal maxExposure = 0.60m;
        private decimal maxPorfolioRiskPerPosition = 0.1m;
        private int minSharesPerTransaction = 10;

        private bool resetAtEndOfDay = false;               // Reset the strategies at EOD.
        private bool noOvernight = true;                    // Close all positions before market close.
        /* +-------------------------------------------------+*/

        // Flags the first minutes after market open.
        private bool isMarketJustOpen;

        // Flags the last minutes before market close.
        private bool isMarketAboutToClose;

        // Dictionary used to store the MSAStrategy object for each symbol.
        private Dictionary<string, MSAStrategy> Strategy = new Dictionary<string, MSAStrategy>();

        // Dictionary used to store the portfolio share-size for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        // Dictionary used to store the PSAR indicator for each symbol.
        private Dictionary<string, ParabolicStopAndReverse> PSARDict = new Dictionary<string, ParabolicStopAndReverse>();

        // Flags the open market time for normal strategy operations.
        private bool isNormalOperativeTime
        {
            get
            {
                return !(isMarketJustOpen ||
                         isMarketAboutToClose);
            }
        }

        #endregion Fields

        #region Logging stuff - Defining

        public List<StringBuilder> stockLogging = new List<StringBuilder>();
        public StringBuilder transactionLogging = new StringBuilder();
        public StringBuilder dailyProfitsLogging = new StringBuilder();
        private int barCounter = 0;

        #endregion Logging stuff - Defining

        #region QCAlgorithm overridden methods

        public override void Initialize()
        {
            SetStartDate(_startDate);               //Set Start Date
            SetEndDate(_endDate);                   //Set End Date
            SetCash(_initialCapital);              //Set Strategy Cash

            #region Logging stuff - Initializing Operations Logging

            transactionLogging.AppendLine("Time,Symbol,Order");
            dailyProfitsLogging.AppendLine("Date,Symbol,Trades,Profit/Loss");
            int i = 0;  // Only used for logging.

            #endregion Logging stuff - Initializing Operations Logging

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                // Define and register a Decycle indicator to be
                // injected in the Strategy.
                var decycle = new Decycle("Decycle_" + symbol, DecyclePeriod);
                RegisterIndicator(symbol, decycle, Resolution.Minute, Field.Close);

                // Define the PSAR for each symbol
                PSARDict.Add(symbol, new ParabolicStopAndReverse(afStart: 0.01m, afIncrement:0.001m, afMax: 0.2m));
                RegisterIndicator(symbol, PSARDict[symbol], Resolution.Minute);

                Strategy.Add(symbol, new MSAStrategy(decycle, PreviousDaysN, RunsPerDay, MinimumRunThreshold));
                // Define the Share size for each symbol.
                ShareSize.Add(symbol, (maxLeverage * maxExposure) / Symbols.Count());

                // Strategy warm-up.
                var history = History(symbol, (PreviousDaysN + 1) * 390);
                foreach (var bar in history)
                {
                    decycle.Update(bar.EndTime, bar.Close);
                }

                #region Logging stuff - Initializing Stock Logging

                stockLogging.Add(new StringBuilder());
                stockLogging[i].AppendLine("Time,Close,Decycle,PSAR,Position");
                i++;

                #endregion Logging stuff - Initializing Stock Logging
            }

            #region Schedules

            // Set flags correctly at market open
            Schedule.Event("MarketOpen")
                .EveryDay()
                .At(9, 29)
                //.AfterMarketOpen(Symbols[0], minutesAfterOpen: -1)
                .Run(() =>
                {
                    isMarketJustOpen = true;
                    isMarketAboutToClose = false;
                    Log(string.Format("========== {0} Market Open ==========", Time.DayOfWeek));
                });
            Schedule.Event("MarketOpenSpan")
                .EveryDay()
                .At(9, 50)
                .Run(() => isMarketJustOpen = false);

            Schedule.Event("MarketAboutToClose")
                .EveryDay()
                .At(15, 50)
                //.BeforeMarketClose(Symbols[0], minuteBeforeClose: 10)
                .Run(() => isMarketAboutToClose = true);

            Schedule.Event("MarketClose")
                .EveryDay()
                .At(15, 59)
                //.BeforeMarketClose(Symbols[0], minuteBeforeClose: 10)
                .Run(() => CloseDay());

            #endregion Schedules
        }

        public void OnData(TradeBars data)
        {
            OrderSignal actualOrder = OrderSignal.doNothing;

            int i = 0;  // just for logging

            foreach (string symbol in Symbols)
            {
                if (!data.ContainsKey(symbol) || !Strategy[symbol].IsReady) continue;

                bool breakCondition = Time.Date == new DateTime(2015, 08, 07);

                if (isNormalOperativeTime)
                {
                    actualOrder = Strategy[symbol].ActualSignal;
                }
                else if (noOvernight && isMarketAboutToClose)
                {
                    actualOrder = CloseAllPositions(symbol);
                }

                ExecuteStrategy(symbol, actualOrder);

                #region Logging stuff - Filling the data StockLogging

                //Time,Close,Decycle,InvFisher,LightSmoothPrice,Momersion,PSAR,Position
                string newLine = string.Format("{0},{1},{2},{3},{4}",
                                               Time.ToString("u"),
                                               data[symbol].Close,
                                               Strategy[symbol].SmoothedSeries.Current.Value,
                                               PSARDict[symbol].Current.Value,
                                               Portfolio[symbol].Invested ? Portfolio[symbol].IsLong ? 1 : -1 : 0
                                               );
                stockLogging[i].AppendLine(newLine);
                i++;

                #endregion Logging stuff - Filling the data StockLogging
            }
            barCounter++; // just for logging
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            string symbol = orderEvent.Symbol;
            int position = Portfolio[symbol].Quantity;
            var actualOrder = Transactions.GetOrderById(orderEvent.OrderId);

            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                    Log("New order sent: " + actualOrder.ToString());
                    break;

                case OrderStatus.Submitted:
                    Log("Order Submitted: " + actualOrder.ToString());
                    break;

                case OrderStatus.PartiallyFilled:
                case OrderStatus.Filled:
                    Log("Order Filled: " + actualOrder.ToString());
                    // Time,Symbol,Order
                    string newLine = string.Format("{0},{1},{2}",
                        Time.ToString("u"),
                        actualOrder.Symbol,
                        actualOrder.Direction.ToString()
                        );
                    transactionLogging.AppendLine(newLine);
                    break;

                case OrderStatus.Canceled:
                    Log("Order Canceled: " + actualOrder.ToString());
                    break;

                case OrderStatus.None:
                case OrderStatus.Invalid:
                default:
                    Log("WTF!!!!!");
                    break;
            }

            if (position > 0) Strategy[symbol].Position = StockState.longPosition;
            else if (position < 0) Strategy[symbol].Position = StockState.shortPosition;
            else Strategy[symbol].Position = StockState.noInvested;
        }

        public override void OnEndOfAlgorithm()
        {
            #region Logging stuff - Saving the logs

            int i = 0;
            string filename;
            string filePath;

            foreach (string symbol in Symbols)
            {
                filename = string.Format("MSA_price_{0}.csv", symbol);
                filePath = AssemblyLocator.ExecutingDirectory() + filename;

                if (File.Exists(filePath)) File.Delete(filePath);
                File.AppendAllText(filePath, stockLogging[i].ToString());
                Debug(string.Format("\nSymbol Name: {0}, Ending Value: {1} ", symbol, Portfolio[symbol].Profit));
                i++;
            }

            filename = string.Format("MSA_transactions.csv");
            filePath = AssemblyLocator.ExecutingDirectory() + filename;

            if (File.Exists(filePath)) File.Delete(filePath);
            File.AppendAllText(filePath, transactionLogging.ToString());

            filename = string.Format("MSA_dailyReturns.csv");
            filePath = AssemblyLocator.ExecutingDirectory() + filename;

            if (File.Exists(filePath)) File.Delete(filePath);
            File.AppendAllText(filePath, dailyProfitsLogging.ToString());

            #endregion Logging stuff - Saving the logs
        }

        #endregion QCAlgorithm overridden methods

        #region Algorithm Methods

        /// <summary>
        /// Executes the strategy.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="actualOrder">The actual order.</param>
        private void ExecuteStrategy(string symbol, OrderSignal actualOrder)
        {
            int? shares = PositionShares(symbol, actualOrder);

            switch (actualOrder)
            {
                case OrderSignal.goLong:
                case OrderSignal.goShort:
                    // If the returned shares is null then is the same than doNothing.
                    if (shares.HasValue)
                    {
                        Log("===> Market entry order sent " + symbol);
                        MarketOrder(symbol, shares.Value);
                    }
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    Log("<=== Closing Position " + symbol);
                    MarketOrder(symbol, shares.Value);
                    break;

                default: break;
            }
        }

        /// <summary>
        /// Estimate the shares to operate in the next transaction given the stock weight and the kind of order.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="actualOrder">The actual order.</param>
        /// <returns></returns>
        private int? PositionShares(string symbol, OrderSignal actualOrder)

        {
            int? positionQuantity = null;
            int quantity = 0;
            decimal price = Securities[symbol].Price;

            // Handle negative portfolio weights.
            if (ShareSize[symbol] < 0)
            {
                if (actualOrder == OrderSignal.goLong) actualOrder = OrderSignal.goShort;
                else if (actualOrder == OrderSignal.goShort) actualOrder = OrderSignal.goLong;
            }

            switch (actualOrder)
            {
                case OrderSignal.goShort:
                case OrderSignal.goLong:
                    // In the first part the estimations are in ABSOLUTE VALUE!

                    // Estimated the desired quantity to achieve target-percent holdings.
                    quantity = Math.Abs(CalculateOrderQuantity(symbol, ShareSize[symbol]));
                    // Estimate the max allowed position in dollars and compare it with the desired one.
                    decimal maxOperationDollars = Portfolio.TotalPortfolioValue * maxPorfolioRiskPerPosition;
                    decimal operationDollars = quantity * price;
                    // If the desired is bigger than the max allowed operation, then estimate a new bounded quantity.
                    if (maxOperationDollars < operationDollars) quantity = (int)(maxOperationDollars / price);

                    if (actualOrder == OrderSignal.goLong)
                    {
                        // Check the margin availability.
                        quantity = (int)Math.Min(quantity, Portfolio.MarginRemaining / price);
                    }
                    else
                    {
                        // In case of short sales, the margin should be a 150% of the operation.
                        quantity = (int)Math.Min(quantity, Portfolio.MarginRemaining / (1.5m * price));
                        // Now adjust the sing correctly.
                        quantity *= -1;
                    }
                    break;

                case OrderSignal.closeShort:
                case OrderSignal.closeLong:
                    quantity = -Portfolio[symbol].Quantity;
                    break;

                default:
                    break;
            }

            // Only assign a value to the positionQuantity if is bigger than a threshold. If not, then it'll return null.
            if (Math.Abs(quantity) > minSharesPerTransaction)
            {
                positionQuantity = quantity;
            }

            return positionQuantity;
        }
        /// <summary>
        /// Closes all positions before market close.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <returns></returns>
        private OrderSignal CloseAllPositions(string symbol)
        {
            OrderSignal actualOrder;

            if (Strategy[symbol].Position == StockState.longPosition)
            {
                actualOrder = OrderSignal.closeLong;
                Log("<=== Closing EOD Long position " + symbol);
            }
            else if (Strategy[symbol].Position == StockState.shortPosition)
            {
                actualOrder = OrderSignal.closeShort;
                Log("===> Closing EOD Short position " + symbol);
            }
            else actualOrder = OrderSignal.doNothing;
            return actualOrder;
        }

        private void CloseDay()
        {
            foreach (string symbol in Symbols)
            {
                var todayTrades = TradeBuilder.ClosedTrades
                    .Where(o => o.ExitTime.Date == Time.Date && o.Symbol == symbol);

                var todayProfit = todayTrades.Count() != 0 ? todayTrades.Sum(o => o.ProfitLoss) : 0m;

                dailyProfitsLogging.AppendLine(string.Format("{0},{1},{2},{3}",
                    Time.Date.ToString("u"),
                    symbol,
                    todayTrades.Count(),
                    todayProfit
                    ));
            }
            Log(string.Format("---------- {0} Market Close ----------", Time.DayOfWeek));
        }

        #endregion Algorithm Methods
    }
}