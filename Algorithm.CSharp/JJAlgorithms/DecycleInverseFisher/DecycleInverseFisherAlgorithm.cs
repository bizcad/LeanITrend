using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;


namespace QuantConnect.Algorithm.CSharp.JJAlgorithms.DecycleInverseFisher
{
    public class DecycleInverseFisherAlgorithm : QCAlgorithm
    {
        #region Algorithm Globals
        private DateTime _startDate = new DateTime(2013, 10, 7);
        private DateTime _endDate = new DateTime(2013, 10, 11);
        private decimal _portfolioAmount = 25000;
        #endregion

        #region Fields

        /* +-------------------------------------------------+
     * |Algorithm Control Panel                          |
     * +-------------------------------------------------+*/
        private static int DecyclePeriod = 20;
        private static int InvFisherPeriod = 40;
        private static decimal Threshold = 0.9m;
        private static decimal Tolerance = 0.001m;

        private static decimal maxLeverage = 1m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.00m;         // Percentage of Leverage left unused.

        private bool resetAtEndOfDay = true;            // Reset the strategies at EOD.
        private bool noOvernight = true;                // Close all positions before market close.
        /* +-------------------------------------------------+*/

        private static string[] Symbols = { "AIG", "BAC", "IBM", "SPY" };

        // Dictionary used to store the ITrendStrategy object for each symbol.
        private Dictionary<string, DIFStrategy> Strategy = new Dictionary<string, DIFStrategy>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        private EquityExchange theMarket = new EquityExchange();
        #endregion

        #region Logging stuff - Defining

        public List<StringBuilder> stockLogging = new List<StringBuilder>();
        public StringBuilder portfolioLogging = new StringBuilder();
        private int barCounter = 0;

        #endregion Logging stuff - Defining

        #region QCAlgorithm overriden methods
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
                Strategy.Add(symbol, new DIFStrategy(DecyclePeriod, InvFisherPeriod, Threshold, Tolerance));
                RegisterStrategy(symbol);
                ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());

                #region Logging stuff - Initializing Stock Logging

                stockLogging.Add(new StringBuilder());
                stockLogging[i].AppendLine("Counter, Time, Close, Decycle, InvFisher, OrderSignal, StateFromStrategy, StateFromPorfolio");
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

                if (theMarket.DateTimeIsOpen(Time))
                {
                    if (noOvernight && isMarketAboutToClose)
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
                #region Logging stuff - Filling the data StockLogging
                //Counter, Time, Close, Decycle, InvFisher, OrderSignal, StateFromStrategy, StateFromPorfolio
                string newLine = string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                                               barCounter,
                                               Time,
                                               data[symbol].Close,
                                               Strategy[symbol].DecycleTrend.Current.Value,
                                               Strategy[symbol].InverseFisher.Current.Value,
                                               (actualOrder == OrderSignal.goLong || actualOrder == OrderSignal.closeShort) ? 1 :
                                               (actualOrder == OrderSignal.goShort || actualOrder == OrderSignal.closeLong) ? -1 : 0,
                                               Strategy[symbol].Position.ToString(),
                                               Portfolio[symbol].Quantity.ToString(),
                                               Portfolio.TotalPortfolioValue
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

            if (position > 0) Strategy[symbol].Position = StockState.longPosition;
            else if (position < 0) Strategy[symbol].Position = StockState.shortPosition;
            else Strategy[symbol].Position = StockState.noInvested;
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
                string filename = string.Format("LittleWing{0}.csv", symbol);
                // JJ do not delete this line it locates my engine\bin\debug folder
                //  I just uncomment it when I run on my local machine
                string filePath = AssemblyLocator.ExecutingDirectory() + filename;

                if (File.Exists(filePath)) File.Delete(filePath);
                File.AppendAllText(filePath, stockLogging[i].ToString());
                Debug(string.Format("\nSymbol Name: {0}, Ending Value: {1} ", symbol, Portfolio[symbol].Profit));

            }

            Debug(string.Format("\nAlgorithm Name: {0}\n Ending Portfolio Value: {1} ", this.GetType().Name, Portfolio.TotalPortfolioValue));

            #endregion Logging stuff - Saving the logs
        }

        #endregion

        #region Algorithm methods

        private void RegisterStrategy(string symbol)
        {
            var consolidator = new IdentityDataConsolidator<TradeBar>();
            SubscriptionManager.AddConsolidator(symbol, consolidator);
            consolidator.DataConsolidated += (sender, consolidated) =>
            {
                Strategy[symbol].DecycleTrend.Update(new IndicatorDataPoint(consolidated.Time, consolidated.Price));
            };
        }

        private void ExecuteStrategy(string symbol, OrderSignal actualOrder, TradeBars data)
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
                        int orderShares = shares.Value;
                        MarketOrder(symbol, orderShares);
                    }
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    Liquidate(symbol);
                    break;

                default: break;
            }
        }

        public int? PositionShares(string symbol, OrderSignal order)
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
                    quantity = operationQuantity;
                    break;

                default:
                    quantity = null;
                    break;
            }
            return quantity;
        }
        #endregion
    }
}
