using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Securities.Equity;
using QuantConnect.Securities;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp.ITrendAlgorithm
{
    internal class ITrendAlgorithm : QCAlgorithm
    {
        #region Fields

    /* +-------------------------------------------------+
     * |Algorithm Control Panel                          |
     * +-------------------------------------------------+*/
        private static int ITrendPeriod = 7;            // Instantaneous Trend period.
        private static decimal Tolerance = 0.005m;      // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m;     // Percentage tolerance before revert position.
        
        private static decimal maxLeverage = 3m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.25m;         // Percentage of Leverage left unused.
        private int maxOperationQuantity = 250;         // Maximum shares per operation.

        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.

        private bool resetAtEndOfDay = true;           // Reset the strategies at EOD.
        private bool noOvernight = true;                // Close all positions before market close.
    /* +-------------------------------------------------+*/
        
        private static string[] Symbols = { "AIG", "BAC", "IBM", "SPY" };
        
        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, ITrendStrategy> Strategy = new Dictionary<string, ITrendStrategy>();

        // Dictionary used to store the Lists of OrderTickets object for each symbol.
        private Dictionary<string, List<OrderTicket>> Tickets = new Dictionary<string, List<OrderTicket>>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        // Dictionary used to store the last operation for each symbol.
        private Dictionary<string, OrderSignal> LastOrderSent = new Dictionary<string, OrderSignal>();

        EquityExchange theMarket = new EquityExchange();
        
        #endregion Fields

        #region Logging stuff - Defining

        public List<StringBuilder> stockLogging = new List<StringBuilder>();
        public StringBuilder portfolioLogging = new StringBuilder();
        private int barCounter = 0;

        #endregion Logging stuff - Defining

        #region QCAlgorithm methods

        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);   //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            int i = 0;
            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                Strategy.Add(symbol, new ITrendStrategy(ITrendPeriod, Tolerance, RevertPCT));
                Tickets.Add(symbol, new List<OrderTicket>());
                // Equal portfolio shares for every stock.
                ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
                LastOrderSent.Add(symbol, OrderSignal.doNothing);

                #region Logging stuff - Initializing

                stockLogging.Add(new StringBuilder());
                stockLogging[i].AppendLine("Counter, Time, Close, ITrend, Momentum, Trigger, Signal," +
                    "MomentumWindow[1], MomentumWindow[0]," +
                    "TriggerCrossOverITrend, TriggerCrossUnderITrend, ExitFromLong, ExitFromShort," +
                    "StateFromStrategy, StateFromPorfolio,");
                //"Counter, Time, Close, ITrend, Momentum, MomentumWindow, Signal, limitPrice, FillPrice, State, LastState, ShareSize, IsShort, IsLong, QuantityHold");
                i++;

                #endregion Logging stuff - Initializing
            }
        }

        public void OnData(TradeBars data)
        {
            OrderSignal actualOrder = OrderSignal.doNothing;
            bool isMarketAboutToClose;

            int i = 0;
            foreach (string symbol in Symbols)
            {
                // Update the ITrend indicator in the strategy object.
                Strategy[symbol].ITrend.Update(new IndicatorDataPoint(Time, data[symbol].Close));
                
                isMarketAboutToClose = !theMarket.DateTimeIsOpen(Time.AddMinutes(10));

                // Operate only if the market is open 
                if (theMarket.DateTimeIsOpen(Time))
                {
                    // First check if there are some limit orders not filled yet.
                    if (LastOrderSent[symbol] == OrderSignal.goLong || LastOrderSent[symbol] == OrderSignal.goShort)
                    {
                        CheckOrderStatus(symbol, LastOrderSent[symbol]);
                    }
                    
                    // Check if the market is about to close and noOvernight is true.
                    if(noOvernight && isMarketAboutToClose)
                    {
                        if (Strategy[symbol].Position == StockState.longPosition) actualOrder = OrderSignal.closeLong;
                        else if (Strategy[symbol].Position == StockState.shortPosition) actualOrder = OrderSignal.closeShort;
                        else actualOrder = OrderSignal.doNothing;
                    }
                    else
                    {
                        // Now check if there is some signal and execute the strategy.
                        actualOrder = Strategy[symbol].CheckSignal();
                    }
                    ExecuteStrategy(symbol, actualOrder, data);
                }
                
                #region Logging stuff - Filling the data

                //    "Counter, Time, Close, ITrend, Momentum, Trigger, Signal,"+
                //    "MomentumWindow[1], MomentumWindow[0]," +
                //    "TriggerCrossOverITrend, TriggerCrossUnderITrend, ExitFromLong, ExitFromShort,"+
                //    "StateFromStrategy, StateFromPorfolio,"
                string newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}",
                                               barCounter,
                                               Time,
                                               data[symbol].Close,
                                               Strategy[symbol].ITrend.Current.Value,
                                               Strategy[symbol].ITrendMomentum.Current.Value,
                                               Strategy[symbol].ITrend.Current.Value + Strategy[symbol].ITrendMomentum.Current.Value,
                                               actualOrder,
                                               (Strategy[symbol].MomentumWindow.IsReady) ? Strategy[symbol].MomentumWindow[1] : 0,
                                               (Strategy[symbol].MomentumWindow.IsReady) ? Strategy[symbol].MomentumWindow[0] : 0,
                                               Strategy[symbol].TriggerCrossOverITrend.ToString(),
                                               Strategy[symbol].TriggerCrossUnderITrend.ToString(),
                                               Strategy[symbol].ExitFromLong.ToString(),
                                               Strategy[symbol].ExitFromShort.ToString(),
                                               Strategy[symbol].Position.ToString(),
                                               Portfolio[symbol].Quantity.ToString()
                                               );
                stockLogging[i].AppendLine(newLine);
                i++;

                #endregion Logging stuff - Filling the data
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

            int i = 0;
            foreach (string symbol in Symbols)
            {
                string filename = string.Format("ITrendDebug_{0}.csv", symbol);
                string filePath = @"C:\Users\JJ\Desktop\MA y señales\ITrend Debug\" + filename;

                if (File.Exists(filePath)) File.Delete(filePath);

                File.AppendAllText(filePath, stockLogging[i].ToString());
            }

            #endregion Logging stuff - Saving the logs
        }

        #endregion QCAlgorithm methods

        #region Methods

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
            if (Tickets[symbol].Last().Status != OrderStatus.Filled)
            {
                shares = Tickets[symbol].Last().Quantity;
                // cancel the limit order and send a new market order.
                Tickets[symbol].Last().Cancel();
                Tickets[symbol].Add(MarketOrder(symbol, shares));
            }
            // Once the ticket is filled, update the ITrenStrategy object for the symbol.
            if (lastOrder == OrderSignal.goLong)
            {
                Strategy[symbol].Position = StockState.longPosition;
            }
            else if (lastOrder == OrderSignal.goShort)
            {
                Strategy[symbol].Position = StockState.shortPosition;
            }
            Strategy[symbol].EntryPrice = Tickets[symbol].Last().AverageFillPrice;
            // Update the LastOrderSent dictionary, to avoid check filled orders many times.
            LastOrderSent[symbol] = OrderSignal.doNothing;

            // TODO: If the ticket is partially filled.
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
                    // inmediatelly. So, update the ITrend strategy and the LastOrder Dictionary.
                    Strategy[symbol].Position = StockState.noInvested;
                    Strategy[symbol].EntryPrice = null;
                    LastOrderSent[symbol] = OrderSignal.doNothing;
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    // Define the operation size.
                    shares = PositionShares(symbol, actualOrder);
                    // Send the order.
                    Tickets[symbol].Add(MarketOrder(symbol, shares));
                    // Beacuse the order is an synchronously market order, they'll fill
                    // inmediatlly. So, update the ITrend strategy and the LastOrder Dictionary.
                    if (actualOrder == OrderSignal.revertToLong) Strategy[symbol].Position = StockState.longPosition;
                    else if (actualOrder == OrderSignal.revertToShort) Strategy[symbol].Position = StockState.shortPosition;
                    Strategy[symbol].EntryPrice = Tickets[symbol].Last().AverageFillPrice;
                    LastOrderSent[symbol] = actualOrder;
                    break;

                default: break;
            }
        }

        #endregion Methods
    }
}