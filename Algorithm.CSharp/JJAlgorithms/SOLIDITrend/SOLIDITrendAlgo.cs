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

namespace QuantConnect.Algorithm.CSharp.JJAlgorithms.SOLIDITrend
{
    public class SOLIDITrendAlgo : QCAlgorithm
    {
        #region "Algorithm Globals"
        private DateTime _startDate = new DateTime(2013, 10, 7);
        private DateTime _endDate = new DateTime(2013, 10, 11);
        private decimal _portfolioAmount = 100000;
        private decimal _transactionSize = 15000;
        private decimal _initialStockAccount = 1500;
        #endregion

        #region Fields

    /* +-------------------------------------------------+
     * |Algorithm Control Panel                          |
     * +-------------------------------------------------+*/
        private static int TrendPeriod = 5;            // Instantaneous Trend period.
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

        // Indicators that will be injected in the Strategies objects.
        Dictionary<string, IndicatorBase<IndicatorDataPoint>> TrenDict = new Dictionary<string, IndicatorBase<IndicatorDataPoint>>();

        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, SimpleMomentumStrategy> Strategy = new Dictionary<string, SimpleMomentumStrategy>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        // Dictionary used to store the last order sent for each symbol.
        private Dictionary<string, Queue<OrderTicket>> OrderSent = new Dictionary<string, Queue<OrderTicket>>();
        
        // Dictionary used to store a account for each symbol. Used for testing stock's performance with a boundary.
        private Dictionary<string, decimal> StockAccount = new Dictionary<string, decimal>();  
        
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
            //SetCash(_portfolioAmount);              //Set Strategy Cash
            SetCash(_initialStockAccount * Symbols.Count());

            #region Logging stuff - Initializing Portfolio Logging

            portfolioLogging.AppendLine("Counter, Time, Portfolio Value");
            int i = 0;  // Only used for logging.

            #endregion Logging stuff - Initializing Portfolio Logging

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

                TrenDict.Add(symbol, new Decycle(TrendPeriod));
                RegisterTrend(symbol);
                Strategy.Add(symbol, new SimpleMomentumStrategy(TrenDict[symbol], Tolerance, RevertPCT, RevertPositionCheck.vsTrigger));

                StockAccount.Add(symbol, _initialStockAccount);
                
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
                // If the Stock account fall below the 5% of the inital value, dont operate it anymore.
                if (StockAccount[symbol] < 0.05m * _initialStockAccount) return;
                
                isMarketAboutToClose = !theMarket.DateTimeIsOpen(Time.AddMinutes(10));

                // Operate only if the market is open
                if (theMarket.DateTimeIsOpen(Time))
                {
                    // First check if there are some limit orders not filled yet.
                    CheckSentOrderStatus(symbol);

                    // Check if the market is about to close and noOvernight is true.
                    if (noOvernight && isMarketAboutToClose)
                    {
                        if (Strategy[symbol].Position == StockState.longPosition) actualOrder = OrderSignal.closeLong;
                        else if (Strategy[symbol].Position == StockState.shortPosition) actualOrder = OrderSignal.closeShort;
                        else actualOrder = OrderSignal.doNothing;
                    }
                    else
                    {
                        // This method update the strategy's position and entry price right from the Portfolio and the Transaction.
                        CheckStrategyStatus(symbol);
                        // Now check if there is some signal and execute the strategy.
                        actualOrder = Strategy[symbol].CheckSignal(data[symbol].Close);
                    }
                    ExecuteStrategy(symbol, actualOrder, data);
                    // Update the Stocl account
                    StockAccount[symbol] = _initialStockAccount + Portfolio[symbol].NetProfit;
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
                    Strategy[symbol].Reset();
                }
            }
        }

        public override void OnEndOfAlgorithm()
        {

            #region Logging stuff - Saving the logs
            string filename; 
            StringBuilder StockNetProfit = new StringBuilder();
            string filePath = AssemblyLocator.ExecutingDirectory();

            int i = 0;
            foreach (string symbol in Symbols)
            {
                StockNetProfit.AppendLine(string.Format("{0},{1}", symbol, Portfolio[symbol].NetProfit));
                filename = filePath + string.Format("MultiStrategyDebug_{0}.csv", symbol);

                if (File.Exists(filename)) File.Delete(filename);
                File.AppendAllText(filename, stockLogging[i].ToString());
            }
            filename = filePath + "StockNetProfit.csv";
            if (File.Exists(filename)) File.Delete(filename);
            File.AppendAllText(filename, StockNetProfit.ToString());
            
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
            var ordersEnumerable = Transactions.GetOrderTickets(t => t.Symbol == symbol);
            if (ordersEnumerable.Count() == 0) return;
            
            OrderTicket lastOrder = ordersEnumerable.Last();
            // If the ticket isn't filled...
            if (lastOrder.Status != OrderStatus.Filled)
            {
                shares = lastOrder.Quantity;
                // cancel the limit order and send a new market order.
                lastOrder.Cancel();
                MarketOrder(symbol, shares);
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
                    //operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
                    operationQuantity = (int)(StockAccount[symbol] / Securities[symbol].Price);
                    quantity = Math.Min(maxOperationQuantity, operationQuantity);
                    break;

                case OrderSignal.goShort:
                    //operationQuantity = CalculateOrderQuantity(symbol, ShareSize[symbol]);
                    operationQuantity = (int)(StockAccount[symbol] / Securities[symbol].Price);
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
            
            var ordersEnumerable = Transactions.GetOrderTickets(t => t.Symbol == symbol);
            if (ordersEnumerable.Count() != 0) 
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
            else
            {
                actualState = StockState.noInvested;
                entryPrice = null;
            }
            Strategy[symbol].Position = actualState;
            Strategy[symbol].EntryPrice = entryPrice;
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
                    if (shares == 0) break;
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
                    if (lastOrderSent.ContainsKey(symbol))
                    {
                        lastOrderSent[symbol] = LimitOrder(symbol, shares, limitPrice);
                    }
                    else
                    {
                        lastOrderSent.Add(symbol, LimitOrder(symbol, shares, limitPrice));
                    }


                        break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);
                    if (shares == 0) break;
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

        private void RegisterTrend(string symbol)
        {
            var consolidator = new IdentityDataConsolidator<TradeBar>();
            SubscriptionManager.AddConsolidator(symbol, consolidator);
            consolidator.DataConsolidated += (sender, consolidated) =>
            {
                TrenDict[symbol].Update(new IndicatorDataPoint(consolidated.Time, consolidated.Price));
            };
        }
               
        #endregion Algorithm Methods
    }
}