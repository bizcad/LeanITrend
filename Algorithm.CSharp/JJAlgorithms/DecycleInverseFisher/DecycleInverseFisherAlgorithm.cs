using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace QuantConnect.Algorithm.CSharp.JJAlgorithms.DecycleInverseFisher
{
    /// <summary>
    /// This strategy uses 3 indicator to evaluate the entry signal:
    ///    - the Inverse Fisher of a fast Decycle.
    ///    - The Momersion
    ///    - the PSAR
    /// Once the strategy is holding position, a trailing stop order follows the price for a good exit.
    /// </summary>
    public class DecycleInverseFisherAlgorithm : QCAlgorithm
    {
        #region Algorithm Globals

        private DateTime _startDate = new DateTime(2015, 05, 19);
        private DateTime _endDate = new DateTime(2015, 10, 16);
        private decimal _portfolioAmount = 25000;
        private static string[] Symbols = { "AMZN", "AAPL" };

        #endregion Algorithm Globals

        #region Fields

        /* +-------------------------------------------------+
     * |Algorithm Control Panel                          |
     * +-------------------------------------------------+*/
        private const int DecyclePeriod = 10;
        private const int InvFisherPeriod = 270;
        private const decimal Threshold = 0.9m;
        private const decimal Tolerance = 0.001m;

        private const decimal GlobalStopLossPercent = 0.001m;
        private const decimal PercentProfitStartPsarTrailingStop = 0.0003m;

        private const decimal maxLeverage = 1m;          // Maximum Leverage.
        private decimal leverageBuffer = 0.00m;          // Percentage of Leverage left unused.

        private bool resetAtEndOfDay = false;            // Reset the strategies at EOD.
        private bool noOvernight = true;                 // Close all positions before market close.
        /* +-------------------------------------------------+*/

        // Flags the first minutes after market open.
        private bool isMarketJustOpen;

        // Flags the last minutes before market close.
        private bool isMarketAboutToClose;

        // This flag is used to indicate we've switched from a global, non changing
        // stop loss to a dynamic trailing stop using the PSAR.
        private Dictionary<string, bool> EnablePsarTrailingStop = new Dictionary<string, bool>();

        // This is the ticket from our stop loss order (exit)
        private Dictionary<string, OrderTicket> StopLossTickets = new Dictionary<string, OrderTicket>();

        // Dictionary used to store the DIFStrategy object for each symbol.
        private Dictionary<string, DIFStrategy> Strategy = new Dictionary<string, DIFStrategy>();

        // Dictionary used to store the PSAR indicator for each symbol.
        private Dictionary<string, ParabolicStopAndReverse> PSARDict = new Dictionary<string, ParabolicStopAndReverse>();

        // Dictionary used to store the portfolio share-size for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

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
        private int barCounter = 0;

        #endregion Logging stuff - Defining

        #region QCAlgorithm overridden methods

        public override void Initialize()
        {
            SetStartDate(_startDate);               //Set Start Date
            SetEndDate(_endDate);                   //Set End Date
            SetCash(_portfolioAmount);              //Set Strategy Cash

            #region Logging stuff - Initializing Operations Logging

            transactionLogging.AppendLine("Time,Symbol,Order");
            int i = 0;  // Only used for logging.

            #endregion Logging stuff - Initializing Operations Logging

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                // Define and register an Identity indicator with the price, this indicator will be
                // injected in the Strategy.
                var PriceIdentity = Identity(symbol);
                //Identity PriceIdentity = new Identity("Price" + symbol);
                //RegisterIndicator(symbol, PriceIdentity, Resolution.Minute, Field.Close);
                // Define the Strategy
                Strategy.Add(symbol, new DIFStrategy(PriceIdentity, DecyclePeriod, InvFisherPeriod, Threshold, Tolerance));
                // Define the PSAR for each symbol
                PSARDict.Add(symbol, new ParabolicStopAndReverse(afStart: 0.0001m, afIncrement: 0.0001m));
                RegisterIndicator(symbol, PSARDict[symbol], Resolution.Minute);
                // Define the Share size for each symbol.
                ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
                // Fill the stop order dictionary with the symbols keys.
                StopLossTickets.Add(symbol, null);
                // Fill the start Psar trailing stop flag dictionary.
                EnablePsarTrailingStop.Add(symbol, false);

                // At 15 minutes after market open, check for a entry signal.
                Schedule.Event("CheckEarlyEntry")
                .EveryDay()
                .AfterMarketOpen(symbol, minutesAfterOpen: 15)
                .Run(() => CheckEarlyEntry(symbol));

                #region Logging stuff - Initializing Stock Logging

                stockLogging.Add(new StringBuilder());
                stockLogging[i].AppendLine("Time,Close,Decycle,InvFisher,LightSmoothPrice,Momersion,PSAR,Position");
                i++;

                #endregion Logging stuff - Initializing Stock Logging
            }

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
                //.BeforeMarketClose(Symbols[0], minuteBeforeClose: 10)
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
        }

        public void OnData(TradeBars data)
        {
            OrderSignal actualOrder = OrderSignal.doNothing;

            int i = 0;  // just for logging

            foreach (string symbol in Symbols)
            {
                if (isNormalOperativeTime)
                {
                    ManageStopLoss(symbol);
                    actualOrder = Strategy[symbol].ActualSignal;
                }
                else if (noOvernight && isMarketAboutToClose)
                {
                    actualOrder = CloseAllPositions(symbol);
                }

                ExecuteStrategy(symbol, actualOrder);

                #region Logging stuff - Filling the data StockLogging

                //Time,Close,Decycle,InvFisher,LightSmoothPrice,Momersion,PSAR,Position
                string newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                                               Time.ToString("u"),
                                               data[symbol].Close,
                                               Strategy[symbol].DecycleTrend.Current.Value,
                                               Strategy[symbol].InverseFisher.Current.Value,
                                               Strategy[symbol].LightSmoothPrice.Current.Value,
                                               Strategy[symbol].Momersion.Current.Value,
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
                filename = string.Format("LittleWing_{0}.csv", symbol);
                filePath = AssemblyLocator.ExecutingDirectory() + filename;

                if (File.Exists(filePath)) File.Delete(filePath);
                File.AppendAllText(filePath, stockLogging[i].ToString());
                Debug(string.Format("\nSymbol Name: {0}, Ending Value: {1} ", symbol, Portfolio[symbol].Profit));
                i++;
            }

            filename = string.Format("LittleWing_transactions.csv");
            filePath = AssemblyLocator.ExecutingDirectory() + filename;

            if (File.Exists(filePath)) File.Delete(filePath);
            File.AppendAllText(filePath, transactionLogging.ToString());

            Debug(string.Format("\nAlgorithm Name: {0}\n Ending Portfolio Value: {1} ", this.GetType().Name, Portfolio.TotalPortfolioValue));

            #endregion Logging stuff - Saving the logs
        }

        #endregion QCAlgorithm overridden methods

        #region Algorithm methods

        /// <summary>
        /// Checks if the open market conditions are good enough to take a position.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        private void CheckEarlyEntry(string symbol)
        {
            OrderSignal actualOrder = OrderSignal.doNothing;

            if (Strategy[symbol].Momersion > 50)
            {
                if ((PSARDict[symbol] > Securities[symbol].Price) &&
                   (Strategy[symbol].InverseFisher < -Threshold))
                {
                    Log("=== Day early short entry triggered ===");
                    actualOrder = OrderSignal.goShort;
                }

                if ((PSARDict[symbol] < Securities[symbol].Price) &&
                    (Strategy[symbol].InverseFisher > Threshold))
                {
                    Log("=== Day early long entry triggered ===");
                    actualOrder = OrderSignal.goLong;
                }
            }
            ExecuteStrategy(symbol, actualOrder);
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

        /// <summary>
        /// Executes the strategy.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="actualOrder">The actual order.</param>
        private void ExecuteStrategy(string symbol, OrderSignal actualOrder)
        {
            int? shares;

            switch (actualOrder)
            {
                case OrderSignal.goLong:
                case OrderSignal.goShort:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);
                    // Send the order.
                    if (shares.HasValue)
                    {
                        Log("===> Market entry order sent " + symbol);
                        int orderShares = shares.Value;
                        var entryOrder = MarketOrder(symbol, orderShares);

                        // submit stop loss order for max loss on the trade
                        decimal stopPrice = (actualOrder == OrderSignal.goLong) ?
                            Securities[symbol].Low * (1 - GlobalStopLossPercent) :
                            Securities[symbol].High * (1 + GlobalStopLossPercent);
                        StopLossTickets[symbol] = StopMarketOrder(symbol, -orderShares, stopPrice);
                        EnablePsarTrailingStop[symbol] = false;
                    }
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    Log("<=== Liquidate " + symbol);
                    Liquidate(symbol);
                    StopLossTickets[symbol].Cancel();
                    StopLossTickets[symbol] = null;
                    break;

                default: break;
            }
        }

        public int? PositionShares(string symbol, OrderSignal order, int? maxShares = null,
            decimal? maxAmount = null, decimal? maxPortfolioShare = null)
        {
            int? quantity;
            int operationQuantity;

            switch (order)
            {
                case OrderSignal.goLong:
                    operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
                    quantity = operationQuantity;
                    break;

                case OrderSignal.goShort:
                    operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
                    quantity = -operationQuantity;
                    break;

                default:
                    quantity = null;
                    break;
            }
            return quantity;
        }

        /// <summary>
        /// Manages our stop loss ticket
        /// </summary>
        private void ManageStopLoss(string symbol)
        {
            // if we've already exited then no need to do more
            if (StopLossTickets[symbol] == null || StopLossTickets[symbol].Status == OrderStatus.Filled) return;

            // Get the current stop price.
            var stopPrice = StopLossTickets[symbol].Get(OrderField.StopPrice);

            // check for enabling the psar trailing stop
            EnablePsarTrailingStop[symbol] = ShouldEnablePsarTrailingStop(symbol, stopPrice);

            // we've trigger the psar trailing stop, so start updating our stop loss tick
            if (EnablePsarTrailingStop[symbol] && PSARDict[symbol].IsReady)
            {
                StopLossTickets[symbol].Update(new UpdateOrderFields { StopPrice = PSARDict[symbol] });
                Log("Update stop loss price @ " + PSARDict[symbol].Current.Value.SmartRounding());
            }
        }

        /// <summary>
        /// Method to check if we can enable the PSAR trailing stop.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="stopPrice">The stop price.</param>
        /// <returns></returns>
        private bool ShouldEnablePsarTrailingStop(string symbol, decimal stopPrice)
        {
            if (EnablePsarTrailingStop[symbol]) return true;

            // Determines whether or not the PSAR stop price is better than the specified stop price.
            bool IsPsarMoreProfitableThanStop = (Portfolio[symbol].IsLong && PSARDict[symbol] > stopPrice)
                || (Portfolio[symbol].IsShort && PSARDict[symbol] < stopPrice);

            // Determines whether or not the PSAR is on the right side of price depending on our long/short.
            bool PsarIsOnRightSideOfPrice = (Portfolio[symbol].IsLong && PSARDict[symbol] < Securities[symbol].Close)
                    || (Portfolio[symbol].IsShort && PSARDict[symbol] > Securities[symbol].Close);

            // Covered the certain percentage, we'll use PSAR to control our stop.
            bool isMinimunPercentageCovered = Portfolio[symbol].UnrealizedProfitPercent > PercentProfitStartPsarTrailingStop;

            return PsarIsOnRightSideOfPrice &&
                   IsPsarMoreProfitableThanStop &&
                   isMinimunPercentageCovered;
        }

        private void CloseDay()
        {
            var todayProfit = TradeBuilder.ClosedTrades
                .Where(o => o.ExitTime.Date == Time.Date)
                .Sum(o => o.ProfitLoss);
            Log("Today profit/loss was $" + todayProfit);

            Log(string.Format("---------- {0} Market Close ----------", Time.DayOfWeek));

            foreach (string symbol in Symbols)
            {
                Strategy[symbol].Momersion.Reset();
                PSARDict[symbol].Reset();
                if (resetAtEndOfDay) Strategy[symbol].Reset();
            }
        }

        #endregion Algorithm methods
    }
}