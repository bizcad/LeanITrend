using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Securities.Equity;

namespace QuantConnect.Algorithm.CSharp.JJAlgorithms.DecycleInverseFisher
{
    public class DecycleInverseFisherAlgorithm : QCAlgorithm
    {
        #region "Algorithm Globals"
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
        private Dictionary<string, DIFStrategy> Strategies = new Dictionary<string, DIFStrategy>();

        // Dictionary used to store the portfolio sharesize for each symbol.
        private Dictionary<string, decimal> ShareSize = new Dictionary<string, decimal>();

        private EquityExchange theMarket = new EquityExchange();
        #endregion

        #region QCAlgorithm overriden methods
        public override void Initialize()
        {
            SetStartDate(_startDate);               //Set Start Date
            SetEndDate(_endDate);                   //Set End Date
            SetCash(_portfolioAmount);              //Set Strategy Cash

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                Strategies.Add(symbol, new DIFStrategy(DecyclePeriod, InvFisherPeriod, Threshold, Tolerance));
                RegisterStrategy(symbol);
                ShareSize.Add(symbol, (maxLeverage * (1 - leverageBuffer)) / Symbols.Count());
            }
        }

        public void OnData(TradeBars data)
        {
            bool isMarketAboutToClose;
            OrderSignal actualOrder = OrderSignal.doNothing;

            foreach (string symbol in Symbols)
            {
                isMarketAboutToClose = !theMarket.DateTimeIsOpen(Time.AddMinutes(10));

                if (theMarket.DateTimeIsOpen(Time))
                {
                    if (noOvernight && isMarketAboutToClose)
                    {
                        if (Strategies[symbol].Position == StockState.longPosition) actualOrder = OrderSignal.closeLong;
                        else if (Strategies[symbol].Position == StockState.shortPosition) actualOrder = OrderSignal.closeShort;
                        else actualOrder = OrderSignal.doNothing;
                    }
                    else
                    {
                        // Now check if there is some signal and execute the strategy.
                        actualOrder = Strategies[symbol].CheckSignal();
                    }
                    ExecuteStrategy(symbol, actualOrder, data);
                }
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            string symbol = orderEvent.Symbol;
            int position = Portfolio[symbol].Quantity;
            
            if (position > 0) Strategies[symbol].Position = StockState.longPosition;
            else if (position < 0) Strategies[symbol].Position = StockState.shortPosition;
            else Strategies[symbol].Position = StockState.noInvested;
        }

        public override void OnEndOfDay()
        {
            if (resetAtEndOfDay)
            {
                foreach (string symbol in Symbols)
                {
                    Strategies[symbol].Reset();
                }
            }
        }
        
        #endregion

        #region Algorithm methods
        
        private void RegisterStrategy(string symbol)
        {
            var consolidator = new IdentityDataConsolidator<TradeBar>();
            SubscriptionManager.AddConsolidator(symbol, consolidator);
            consolidator.DataConsolidated += (sender, consolidated) =>
            {
                Strategies[symbol].DecycleTrend.Update(new IndicatorDataPoint(consolidated.Time, consolidated.Price));
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
