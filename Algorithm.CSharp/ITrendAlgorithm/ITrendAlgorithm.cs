using QuantConnect.Data.Market;
using QuantConnect.Orders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace QuantConnect.Algorithm.CSharp.ITrendAlgorithm
{
    internal class ITrendAlgorithm : QCAlgorithm
    {
        #region Fields
        private DateTime _startDate = new DateTime(2015, 5, 19);
        private DateTime _endDate = new DateTime(2015, 8, 21);

        private static int ITrendPeriod = 7;
        private static string[] Symbols = { "AAPL" };

        private static decimal maxLeverage = 3m;
        private decimal leverageBuffer = 0.25m;
        private int maxOperationQuantity = 250;

        private decimal RngFac = 0.35m;

        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, ITrendStrategy> Strategy = new Dictionary<string, ITrendStrategy>();

        // Dictionary used to store the Lists of OrderTickets object for each symbol.
        private Dictionary<string, List<OrderTicket>> Tickets = new Dictionary<string, List<OrderTicket>>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        // Dictionary used to store the las operation for each symbol.
        private Dictionary<string, OrderSignal> LastOrderSent = new Dictionary<string, OrderSignal>();

        #endregion Fields

        #region Logging stuff - Defining

        public List<StringBuilder> stockLogging = new List<StringBuilder>();
        public StringBuilder portfolioLogging = new StringBuilder();
        private int barCounter = 0;

        #endregion Logging stuff - Defining

        #region QCAlgorithm methods

        public override void Initialize()
        {
            SetStartDate(_startDate);   //Set Start Date
            SetEndDate(_endDate);    //Set End Date
            SetCash(22000);             //Set Strategy Cash

            int i = 0;
            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                Strategy.Add(symbol, new ITrendStrategy(ITrendPeriod));
                Tickets.Add(symbol, new List<OrderTicket>());
                // Equal porfolio shares for every stock.
                ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
                LastOrderSent.Add(symbol, OrderSignal.doNothing);

                #region Logging stuff - Initializing

                stockLogging.Add(new StringBuilder());
                stockLogging[i].AppendLine("Counter, Time, Close, ITrend, Momentum, Trigger,Portfolio Value, Signal, limitPrice, FillPrice, State, LastState, ShareSize, IsShort, IsLong, QuantityHold");
                i++;

                #endregion Logging stuff - Initializing
            }
        }

        public void OnData(TradeBars data)
        {
            int shares;
            OrderSignal actualOrder = OrderSignal.doNothing;
            decimal limitPrice;

            int i = 0;
            foreach (string symbol in Symbols)
            {
                // Ugly, so ugly way
                Strategy[symbol].ITrend.Update(new Indicators.IndicatorDataPoint(Time, data[symbol].Close));
                // First check if there are some limit orders not filled yet.
                if (LastOrderSent[symbol] == OrderSignal.goLong || LastOrderSent[symbol] == OrderSignal.goShort)
                {
                    CheckOrderStatus(symbol, LastOrderSent[symbol]);
                }

                limitPrice = -1m; // for debug
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

                #region Logging stuff - Filling the data

                string newLine = string.Format("{0},{1},{2},{3},{4},{5},{6}",
                                               barCounter, Time, data[symbol].Close,
                                               Strategy[symbol].ITrend.Current.Value,
                                               Strategy[symbol].ITrendMomentum.Current.Value,
                                               Strategy[symbol].ITrend.Current.Value + Strategy[symbol].ITrendMomentum.Current.Value,
                                               Portfolio.TotalPortfolioValue
                                               );
                stockLogging[i].AppendLine(newLine);
                i++;

                #endregion Logging stuff - Filling the data
            }
            barCounter++; // just for debug
        }

        public override void OnEndOfAlgorithm()
        {
            int i = 0;
            foreach (string symbol in Symbols)
            {
                string filename = string.Format("ITrendDebug_{0}.csv", symbol);
                string filePath = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\" + filename;

                if (File.Exists(filePath)) File.Delete(filePath);

                File.AppendAllText(filePath, stockLogging[i].ToString());
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

        #endregion Methods
    }
}