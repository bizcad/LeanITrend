using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Securities.Equity;

namespace QuantConnect.Algorithm.CSharp.BizcadAlgorithm.MulitSymbol
{
    class MultisymbolAlgorithm : QCAlgorithm
    {

        #region "Variables"

        private DateTime startTime = DateTime.Now;
        private DateTime _startDate = new DateTime(2015, 8, 14);
        private DateTime _endDate = new DateTime(2015, 8, 14);
        private decimal _portfolioAmount = 25000;

        /* +-------------------------------------------------+
         * |Algorithm Control Panel                          |
         * +-------------------------------------------------+*/
        private static int SMAPeriod = 22;            // Instantaneous Trend period.
        private static decimal Tolerance = 0.0001m;      // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m;     // Percentage tolerance before revert position.
        private static decimal maxLeverage = 1m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.25m;         // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.
        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.
        private bool noOvernight = true;                // Close all positions before market close.
        /* +-------------------------------------------------+*/

        string[] symbolarray = new string[] {"AAPL", "NFLX", "AMZN", "SPY"};
        List<Symbol> Symbols = new List<Symbol>();

        // Dictionary used to store the RSIStrategy object for each symbol.
        private Dictionary<string, MultiSymbolStrategy> Strategy = new Dictionary<string, MultiSymbolStrategy>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        private EquityExchange theMarket = new EquityExchange();



        #endregion

        public override void Initialize()
        {
            SetStartDate(_startDate);       //Set Start Date
            SetEndDate(_endDate);           //Set End Date
            SetCash(_portfolioAmount);      //Set Strategy Cash

            foreach (string t in symbolarray)
            {
                Symbols.Add(new Symbol(t));
            }

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                var priceIdentity = Identity(symbol, selector: Field.Close);

                Strategy.Add(symbol, new MultiSymbolStrategy(priceIdentity, SMAPeriod, Tolerance, RevertPCT));

                // Equally weighted portfolio.
                ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
            }

        }

        public void OnData(TradeBars data)
        {
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
                        CheckLimitOrderStatus(symbol);
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
                }
            }
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
                    Log("New order submitted: " + actualOrder.ToString());
                    break;

                case OrderStatus.PartiallyFilled:
                    Log("Order partially filled: " + actualOrder.ToString());
                    Log("Canceling order");
                    actualTicket.Cancel();
                    //do { }
                    //while (actualTicket.GetMostRecentOrderResponse().IsSuccess);
                    goto case OrderStatus.Filled;

                case OrderStatus.Filled:
                    if (portfolioPosition > 0) Strategy[symbol].Position = StockState.longPosition;
                    else if (portfolioPosition < 0) Strategy[symbol].Position = StockState.shortPosition;
                    else Strategy[symbol].Position = StockState.noInvested;

                    Strategy[symbol].EntryPrice = actualTicket.AverageFillPrice;

                    Log("Order filled: " + actualOrder.ToString());
                    break;

                case OrderStatus.Canceled:
                    Log("Order successfully canceled: " + actualOrder.ToString());
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Checks if the limits order are filled, and updates the ITrenStrategy object and the
        /// LastOrderSent dictionary.
        /// If the limit order aren't filled, then cancels the order and send a market order.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="lastOrder">The last order.</param>
        private void CheckLimitOrderStatus(string symbol)
        {
            // Pick the submitted limit tickets for the symbol.
            var actualSubmittedTicket = Transactions.GetOrderTickets(t => t.Symbol == symbol
                                                              && t.OrderType == OrderType.Limit
                                                              && t.Status == OrderStatus.Submitted);
            // If there is none, return.
            if (actualSubmittedTicket.Count() == 0) return;
            // if there is more than one, stop the algorithm, something is wrong.
            else if (actualSubmittedTicket.Count() != 1) throw new ApplicationException("More than one submitted limit order");

            Log("||| Cancel Limit order and send a market order");
            // Now, define the ticket to handle the actual OrderTicket.
            var actualTicket = actualSubmittedTicket.Single();
            // Retrieve the operation quantity. 
            int shares = actualTicket.Quantity;
            // Cancel the order.
            actualTicket.Cancel();
            // Send a market order.
            MarketOrder(symbol, shares);
        }
        private OrderSignal ClosePositions(string symbol)
        {
            OrderSignal actualOrder;
            if (Strategy[symbol].Position == StockState.longPosition) actualOrder = OrderSignal.closeLong;
            else if (Strategy[symbol].Position == StockState.shortPosition) actualOrder = OrderSignal.closeShort;
            else actualOrder = OrderSignal.doNothing;
            return actualOrder;
        }
        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="actualOrder">The actual order to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(string symbol, OrderSignal actualOrder)
        {
            // Define the operation size.
            int shares = PositionShares(symbol, actualOrder);

            switch (actualOrder)
            {
                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.goLongLimit:
                case OrderSignal.goShortLimit:
                    Log("===> Entry to Market");
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
                    Log("<=== Closing Position");
                    // Send the order.
                    MarketOrder(symbol, shares);
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    Log("<===> Reverting Position");
                    // Send the order.
                    MarketOrder(symbol, shares);
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
    }
}
