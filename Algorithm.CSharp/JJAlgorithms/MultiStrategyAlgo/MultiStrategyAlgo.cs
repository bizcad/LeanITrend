using System.Reflection;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Securities.Equity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace QuantConnect.Algorithm.CSharp.JJAlgorithms.MultiStrategyAlgo
{
    public class MultiStrategyAlgo : QCAlgorithm
    {
        #region "Algorithm Globals"
        private DateTime _startDate = new DateTime(2013, 10, 7);
        private DateTime _endDate = new DateTime(2013, 10, 11);
        private decimal _portfolioAmount = 10000;
        private decimal _transactionSize = 15000;
        #endregion

        #region Fields

    /* +-------------------------------------------------+
     * |Algorithm Control Panel                          |
     * +-------------------------------------------------+*/
        private static int TrendPeriod = 7;            // Instantaneous Trend period.
        private static decimal Tolerance = 0.000m;      // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m;     // Percentage tolerance before revert position.

        private static decimal maxLeverage = 1m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.00m;         // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.

        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.

        private bool resetAtEndOfDay = true;            // Reset the strategies at EOD.
        private bool noOvernight = true;                // Close all positions before market close.
    /* +-------------------------------------------------+*/

        //private static string[] Symbols = { "BAC" };
        private static string[] Symbols = { "AIG", "BAC", "IBM", "SPY" };

        // The Istrategy object that will be used as "conveyor" each time we instantiate a new Strategy
        //BaseStrategy Strategy; commented for debug
        SimpleMomentumStrategy Strategy;

        // Indicators that will be used as conveyor by the Strategies objects.
        private Decycle decycleTrend;
        private InstantaneousTrend ITrend;

        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, RollingWindow<IndicatorDataPoint>> PricesSeriesWindow = new Dictionary<string, RollingWindow<IndicatorDataPoint>>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();  
        
        private EquityExchange theMarket = new EquityExchange();

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

            #region Logging stuff - Initializing Portfolio Logging

            portfolioLogging.AppendLine("Counter, Time, Portfolio Value");
            int i = 0;  // Only used for logging.

            #endregion Logging stuff - Initializing Portfolio Logging

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                PricesSeriesWindow.Add(symbol, new RollingWindow<IndicatorDataPoint>(TrendPeriod - 1));
                RegisterRollingWindow(symbol);

                // Equal portfolio shares for every stock.
                ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
                
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
            bool isMarketAboutToClose;
            OrderSignal actualOrder = OrderSignal.doNothing;

            int i = 0;
            foreach (string symbol in Symbols)
            {
                isMarketAboutToClose = !theMarket.DateTimeIsOpen(Time.AddMinutes(10));

                if (!PricesSeriesWindow[symbol].IsReady) return;

                /*
                 * if is markes bullish then
                 *  instantiate this indicator with this period
                 * else if market bearish
                 *  instantiate that indicator with that period
                 * else etc.
                 */

                // Instantiate the inidicator we want to use given the market conditions.
                decycleTrend = new Decycle(TrendPeriod);
                //ITrend = new InstantaneousTrend(TrendPeriod);
                // Instantiate a Strategy class injecting the indicator.
                Strategy = new SimpleMomentumStrategy(decycleTrend, PricesSeriesWindow[symbol], Tolerance, RevertPCT,
                    RevertPositionCheck.vsClosePrice);
                
                // Now we can continue with the Algorithm reasoning
                
                // Operate only if the market is open
                if (theMarket.DateTimeIsOpen(Time))
                {
                    // First check if there are some limit orders not filled yet.
                    CheckSentOrderStatus(symbol);

                    // Check if the market is about to close and noOvernight is true.
                    if (noOvernight && isMarketAboutToClose)
                    {
                        if (Strategy.Position == StockState.longPosition) actualOrder = OrderSignal.closeLong;
                        else if (Strategy.Position == StockState.shortPosition) actualOrder = OrderSignal.closeShort;
                        else actualOrder = OrderSignal.doNothing;
                    }
                    else
                    {
                        // This method update the strategy's position and entry price right from the Portfolio and the Transaction.
                        CheckStrategyStatus(symbol);
                        // Now check if there is some signal and execute the strategy.
                        actualOrder = Strategy.CheckSignal(data[symbol].Close);
                    }
                    ExecuteStrategy(symbol, actualOrder, data);
                }

                #region Logging stuff - Filling the data StockLogging

                //"Counter, Time, Close, ITrend, Trigger," +
                //"Momentum, EntryPrice, Signal," +
                //"TriggerCrossOverITrend, TriggerCrossUnderITrend, ExitFromLong, ExitFromShort," +
                //"StateFromStrategy, StateFromPorfolio, Portfolio Value"
                string newLine = "none";
                    /*string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}",
                                               barCounter,
                                               Time,
                                               data[symbol].Close,
                                               Strategy.Trend.Current.Value,
                                               Strategy.Trend.Current.Value + Strategy.TrendMomentum.Current.Value,
                                               Strategy.TrendMomentum.Current.Value,
                                               (Strategy.EntryPrice == null) ? 0 : Strategy.EntryPrice,
                                               actualOrder,
                                               Strategy.TriggerCrossOverITrend.ToString(),
                                               Strategy.TriggerCrossUnderITrend.ToString(),
                                               Strategy.ExitFromLong.ToString(),
                                               Strategy.ExitFromShort.ToString(),
                                               Strategy.Position.ToString(),
                                               Portfolio[symbol].Quantity.ToString(),
                                               Portfolio.TotalPortfolioValue
                                               );*/
                stockLogging[i].AppendLine(newLine);
                i++;

                #endregion Logging stuff - Filling the data StockLogging
            }
            barCounter++; // just for debug
        }

        

        public override void OnEndOfDay()
        {
            if (resetAtEndOfDay)
            {
                foreach (string symbol in Symbols)
                {
                    PricesSeriesWindow[symbol].Reset();
                }
            }
        }

        public override void OnEndOfAlgorithm()
        {
            #region Logging stuff - Saving the logs

            int i = 0;
            foreach (string symbol in Symbols)
            {
                string filename = string.Format("MultiStrategyDebug_{0}.csv", symbol);
                string filePath = AssemblyLocator.ExecutingDirectory() + filename;

                if (File.Exists(filePath)) File.Delete(filePath);
                File.AppendAllText(filePath, stockLogging[i].ToString());
                Debug(string.Format("\nSymbol Name: {0}, Ending Value: {1} ", symbol, Portfolio[symbol].Profit));

            }

            Debug(string.Format("\nAlgorithm Name: {0}\n Ending Portfolio Value: {1} ", this.GetType().Name, Portfolio.TotalPortfolioValue));

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
        private void CheckSentOrderStatus(string symbol)
        {
            int shares;
            try
            {
                OrderTicket lastOrder = Transactions.GetOrderTickets(t => t.Symbol == symbol).Last();

                // If the ticket isn't filled...
                if (lastOrder.Status != OrderStatus.Filled)
                {
                    shares = lastOrder.Quantity;
                    // cancel the limit order and send a new market order.
                    lastOrder.Cancel();
                    MarketOrder(symbol, shares);
                }
            } 
            catch
            {
                return;
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
                    break;

                case OrderSignal.goShort:
                    operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
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

        private void CheckStrategyStatus(string symbol)
        {
            decimal? entryPrice;
            StockState actualState;
            try
            {
                OrderTicket lastOrder = Transactions.GetOrderTickets(t => t.Symbol == symbol).Last();

                if (Portfolio[symbol].HoldStock)
                {
                    actualState = (Portfolio[symbol].IsLong) ? StockState.longPosition : StockState.shortPosition;
                    entryPrice = lastOrder.AverageFillPrice;
                }
                else
                {
                    actualState = (lastOrder.Status == OrderStatus.Submitted) ? StockState.orderSent : StockState.noInvested;
                    entryPrice = null;
                }
            } 
            catch
            {
                actualState = StockState.noInvested;
                entryPrice = null;
            }
            Strategy.Position = actualState;
            Strategy.EntryPrice = entryPrice;
        }

        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="actualOrder">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(string symbol, OrderSignal actualOrder, TradeBars data)
        {
            int shares;
            decimal limitPrice = 0m;

            switch (actualOrder)
            {
                case OrderSignal.goLong:
                case OrderSignal.goShort:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);
                    // Define the limit price.
                    if (actualOrder == OrderSignal.goLong)
                    {
                        limitPrice = Math.Max(data[symbol].Low,
                                    (data[symbol].Close - (data[symbol].High - data[symbol].Low) * RngFac));
                    }
                    else if (actualOrder == OrderSignal.goShort)
                    {
                        limitPrice = Math.Min(data[symbol].High,
                                    (data[symbol].Close + (data[symbol].High - data[symbol].Low) * RngFac));
                    }
                    // Send the order.
                    LimitOrder(symbol, shares, limitPrice);
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);
                    // Send the order.
                    MarketOrder(symbol, shares);
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);
                    // Send the order.
                    MarketOrder(symbol, shares);
                    break;

                default: break;
            }
        }

        
        private void RegisterRollingWindow(string symbol)
        {
            var consolidator = new IdentityDataConsolidator<TradeBar>();
            SubscriptionManager.AddConsolidator(symbol, consolidator);
            consolidator.DataConsolidated += (sender, consolidated) =>
            {
                PricesSeriesWindow[symbol].Add(new IndicatorDataPoint(consolidated.Time, consolidated.Price));
            };
        }

        #endregion Algorithm Methods
    }
}