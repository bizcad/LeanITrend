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

        // Dictionary used to store the las operation for each symbol
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
                // First check if there are some orders not filled yet.
                if (LastOrderSent[symbol] != OrderSignal.doNothing)
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
                    case OrderSignal.revertToLong:
                    case OrderSignal.revertToShort:
                        shares = PositionShares(symbol, actualOrder);
                        Tickets[symbol].Add(MarketOrder(symbol, shares));
                        LastOrderSent[symbol] = actualOrder;
                        break;

                    default: break;
                }
            }
        }

        #endregion QCAlgorithm methods

        #region Methods

        private void CheckOrderStatus(string symbol, OrderSignal order)
        {
            OrderTicket actualOrder = Tickets[symbol].Last();
            // If the order is filled, update the ITrenStrategy object for the symbol.
            if (actualOrder.Status == OrderStatus.Filled)
            {
                if (order == OrderSignal.closeLong || order == OrderSignal.closeShort)
                {
                    Strategy[symbol].Position = StockStatus.noInvested;
                    Strategy[symbol].EntryPrice = null;
                }
                else
                {
                    if (order == OrderSignal.goLong || order == OrderSignal.revertToLong)
                    {
                        Strategy[symbol].Position = StockStatus.longPosition;
                    }
                    else if (order == OrderSignal.goShort || order == OrderSignal.revertToShort)
                    {
                        Strategy[symbol].Position = StockStatus.shortPosition;
                    }
                    Strategy[symbol].EntryPrice = actualOrder.AverageFillPrice;
                }
                // Update the LastOrderSent dictionary, to avoid check filled orders many times.
                LastOrderSent[symbol] = OrderSignal.doNothing;
            }
            // TODO: If the order isn't filled yet.
            else
            {
                throw new NotImplementedException();
            }
        }

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