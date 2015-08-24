using QuantConnect.Data.Market;
using QuantConnect.Orders;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp.ITrendAlgorithm
{
    internal class ITrendAlgorithm : QCAlgorithm
    {
        #region Fields

        private static int ITrendPeriod = 7;
        private static string[] Symbols = { "AIG", "BAC", "IBM", "SPY" };

        private static decimal maxLeverage = 3m;
        private decimal leverageBuffer = 0.25m;
        private int maxOperationQuantity = 250;

        private decimal RngFac = 0.35m;

        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, ITrendStrategy> Strategy;

        // Dictionary used to store the Lists of OrderTickets object for each symbol.
        private Dictionary<string, List<OrderTicket>> Tickets;

        // Dictionary used to store the Litts of orderTickets object for each symbol.
        private Dictionary<string, int> OrderN; // hmm capaz que lo saco.

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize;

        // Dictionary used to store the las operation for each symbol.
        private Dictionary<string, OrderSignal> LastOrderSent;

        #endregion Fields

        #region QCAlgorithm methods

        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);   //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                Strategy.Add(symbol, new ITrendStrategy(ITrendPeriod));
                Tickets.Add(symbol, new List<OrderTicket>());
                OrderN.Add(symbol, 0);
                // Equal porfolio shares for every stock.
                ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
                LastOrderSent.Add(symbol, OrderSignal.doNothing);
            }
        }

        public void OnData(TradeBars data)
        {
            int shares;
            OrderSignal actualOrder;
            decimal limitPrice = 1m;

            foreach (string symbol in Symbols)
            {
                // First check if there are some limit orders not filled yet.
                if (LastOrderSent[symbol] == OrderSignal.goLong || LastOrderSent[symbol] == OrderSignal.goShort)
                {
                    CheckOrderStatus(symbol, LastOrderSent[symbol]);
                }

                // Now check if there is some signal.
                actualOrder = Strategy[symbol].CheckSignal();
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
                        // Beacuse the order is an synchronously market order, they'll fill
                        // inmediatlly. So, update the ITrend strategy and the LastOrder Dictionary.
                        Strategy[symbol].Position = StockStatus.noInvested;
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
                        if (actualOrder == OrderSignal.revertToLong) Strategy[symbol].Position = StockStatus.longPosition;
                        else if (actualOrder == OrderSignal.revertToShort) Strategy[symbol].Position = StockStatus.shortPosition;
                        Strategy[symbol].EntryPrice = Tickets[symbol].Last().AverageFillPrice;
                        LastOrderSent[symbol] = actualOrder;
                        break;

                    default: break;
                }
            }
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
                Strategy[symbol].Position = StockStatus.longPosition;
            }
            else if (lastOrder == OrderSignal.goShort)
            {
                Strategy[symbol].Position = StockStatus.shortPosition;
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

        #endregion Methods
    }
}