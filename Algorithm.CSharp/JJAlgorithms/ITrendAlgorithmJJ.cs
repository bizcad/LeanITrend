using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;

namespace QuantConnect.Algorithm.CSharp
{

    public class ITrendAlgorithmJJ : QCAlgorithm
    {
        #region "Algorithm Globals"

        private DateTime _startDate = new DateTime(2015, 8, 11);
        private DateTime _endDate = new DateTime(2015, 8, 14);
        //private DateTime _startDate = new DateTime(2015, 5, 19);
        //private DateTime _endDate = new DateTime(2015, 10, 9);
            //private DateTime _startDate = new DateTime(2014, 9, 1);
            //private DateTime _endDate = new DateTime(2015, 9, 26);
        private decimal _portfolioAmount = 26000;
        //private decimal _transactionSize = 15000;

        #endregion "Algorithm Globals"

        #region Fields

        /* +-------------------------------------------------+
     * |Algorithm Control Panel                          |
     * +-------------------------------------------------+*/
        private static int ITrendPeriod = 7; // Instantaneous Trend period.
        private static decimal Tolerance = 0.001m; // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m; // Percentage tolerance before revert position.

        private static decimal maxLeverage = 1.00m; // Maximum Leverage.
        private decimal leverageBuffer = 0.00m; // Percentage of Leverage left unused.
        private int maxOperationQuantity = 250; // Maximum shares per operation.

        private decimal RngFac = 0.35m; // Percentage of the bar range used to estimate limit prices.

        private bool resetAtEndOfDay = true; // Reset the strategies at EOD.
        private bool noOvernight = true; // Close all positions before market close.
        /* +-------------------------------------------------+*/

        private static string[] Symbols = {"AAPL"};
        //{"AAPL", "ALXN", "AMGN", "AMZN", "CELG", "GOOGL", "IBB", "TSLA"};

        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, ITrendStrategy> Strategy = new Dictionary<string, ITrendStrategy>();

        // Dictionary used to store the Lists of OrderTickets object for each symbol.
        private Dictionary<string, List<OrderTicket>> Tickets = new Dictionary<string, List<OrderTicket>>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        // Dictionary used to store the last operation for each symbol.
        private Dictionary<string, OrderSignal> LastOrderSent = new Dictionary<string, OrderSignal>();

        private EquityExchange theMarket = new EquityExchange();

        #endregion Fields

        #region QCAlgorithm methods

        public override void Initialize()
        {
            SetStartDate(_startDate); //Set Start Date
            SetEndDate(_endDate); //Set End Date
            SetCash(_portfolioAmount); //Set Strategy Cash

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                Securities[symbol].TransactionModel = new ConstantFeeTransactionModel(1.00m);

                Strategy.Add(symbol, new ITrendStrategy(ITrendPeriod, Tolerance, RevertPCT));
                Tickets.Add(symbol, new List<OrderTicket>());
                // Equal portfolio shares for every stock.
                ShareSize.Add(symbol, (maxLeverage*(1 - leverageBuffer))/Symbols.Count());
                LastOrderSent.Add(symbol, OrderSignal.doNothing);
            }
        }

        public void OnData(TradeBars data)
        {
            bool isMarketAboutToClose;
            OrderSignal actualOrder = OrderSignal.doNothing;

            foreach (string symbol in Symbols)
            {
                // Update the ITrend indicator in the strategy object.
                Strategy[symbol].ITrend.Update(new IndicatorDataPoint(Time, (data[symbol].Close + data[symbol].Open)/2));

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
                    if (noOvernight && isMarketAboutToClose)
                    {
                        if (Strategy[symbol].Position == StockState.longPosition) actualOrder = OrderSignal.closeLong;
                        else if (Strategy[symbol].Position == StockState.shortPosition)
                            actualOrder = OrderSignal.closeShort;
                        else actualOrder = OrderSignal.doNothing;
                    }
                    else
                    {
                        // Now check if there is some signal and execute the strategy.
                        actualOrder = Strategy[symbol].CheckSignal(data[symbol].Close);
                    }
                    ExecuteStrategy(symbol, actualOrder, data);
                }
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

        #endregion QCAlgorithm methods

        #region Algorithm Methods

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
                    quantity = -2*Portfolio[symbol].Quantity;
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
        /// <param name="actualOrder">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(string symbol, OrderSignal actualOrder, TradeBars data)
        {
            int shares;
            decimal limitPrice = 0m;
            if (actualOrder != OrderSignal.doNothing)
            {
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
                                (data[symbol].Close - (data[symbol].High - data[symbol].Low)*RngFac));
                        }
                        else if (actualOrder == OrderSignal.goShort)
                        {
                            limitPrice = Math.Min(data[symbol].High,
                                (data[symbol].Close + (data[symbol].High - data[symbol].Low)*RngFac));
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
                        if (actualOrder == OrderSignal.revertToLong)
                            Strategy[symbol].Position = StockState.longPosition;
                        else if (actualOrder == OrderSignal.revertToShort)
                            Strategy[symbol].Position = StockState.shortPosition;
                        Strategy[symbol].EntryPrice = Tickets[symbol].Last().AverageFillPrice;
                        LastOrderSent[symbol] = actualOrder;
                        break;

                    default:
                        break;
                }
            }
        }
        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            foreach (string symbol in Symbols)
            {
                Debug(string.Format("\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n loss = {3}",
                    this.GetType().Name, symbol, Portfolio.TotalPortfolioValue, -50));
            }

            #region logging
            //foreach (string symbol in Symbols)
            //{
            //    string filename = string.Format("ITrendDebug_{0}.csv", symbol);
            //    string filePath = @"C:\Users\JJ\Desktop\MA y señales\ITrend Debug\" + filename;
            //    // JJ do not delete this line it locates my engine\bin\debug folder
            //    //  I just uncomment it when I run on my local machine
            //    filePath = AssemblyLocator.ExecutingDirectory() + filename;

            //    if (File.Exists(filePath)) File.Delete(filePath);
            //    File.AppendAllText(filePath, stockLogging[i].ToString());
            //    Debug(string.Format("\nSymbol Name: {0}, Ending Portfolio Value: {1} ", symbol, Portfolio[symbol].Profit));

            //}

            //SendOrderEventsToFile();
            //SendTradesToFile("trades.csv", _orderTransactionProcessor.Trades);
            //SendTradesToFile("simtrades.csv", _proformaProcessor.Trades);
            #endregion
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
        }

        #endregion Algorithm Methods
    }
}