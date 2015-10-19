using QuantConnect.Orders;
using QuantConnect.Data;
using QuantConnect.Indicators;

using System;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp
{
    public enum MomersionState
    {
        Momentum,
        MeanRevertion,
        None
    }

    /// <summary>
    /// Algorithm that uses the Momersion indicator to turn on/off two strategies, a Momentum
    /// strategy (Cross EMA) and a Mean Reversion strategy (RSI).
    /// </summary>
    public class MomersionAlgorithm : QCAlgorithm
    {
        #region Fields

        // Some of the biggest market cap stocks in USA.
        public static string[] Symbols = { "AIG", "BAC", "IBM", "SPY" };

        /* { "AAPL", "AMZN", "FB", "GE", "JNJ", "JPM", "MSFT",
         *   "NVS", "PFE", "PG", "PTR", "TM", "VZ", "WFC" };*/

        // Flags if the market is open.
        private bool isMarketOpen;

        // Operation size.
        private decimal shareSize;

        // Dictionary used to store the active strategy for each symbol.
        private Dictionary<string, MomersionState> ActiveStrategy = new Dictionary<string, MomersionState>();

        // Dictionary used to store the orderID and the Strategy that sent the signal.
        private Dictionary<int, MomersionState> SenderStrategy = new Dictionary<int, MomersionState>();

        // Dictionary used to store the RSIStrategy object for each symbol.
        private Dictionary<string, RSIStrategy> MeanReversionStrategy = new Dictionary<string, RSIStrategy>();

        // Dictionary used to store the CrossEMAStrategy object for each symbol.
        private Dictionary<string, CrossEMAStrategy> MomentumStrategy = new Dictionary<string, CrossEMAStrategy>();

        // Dictionary used to store the Momersion indicator for each symbol.
        private Dictionary<string, MomersionIndicator> Momersion = new Dictionary<string, MomersionIndicator>();
        
        #endregion Fields

        #region QCAlgorithm overridden methods

        /// <summary>
        /// Initialize the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash

            foreach (var symbol in Symbols)
            {
                // Initialize fields.
                isMarketOpen = false;
                // Equally weighted, fully invested portfolio
                shareSize = 1m / Symbols.Length;

                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                // Define and register an Identity indicator with the price, this indicator will be
                // injected in the Strategy.
                Identity PriceIdentity = new Identity(symbol);
                RegisterIndicator(symbol, PriceIdentity, Resolution.Minute, Field.Close);

                // Define the Strategies for this symbol
                MomentumStrategy.Add(symbol, new CrossEMAStrategy(PriceIdentity));
                MeanReversionStrategy.Add(symbol, new RSIStrategy(PriceIdentity));

                Momersion.Add(symbol, new MomersionIndicator(15, 60).Of(PriceIdentity));
                // Once the Momersion indicator is ready, call a method to check the status of Momersion at every indicator's update.
                Momersion[symbol].Updated += (object sender, IndicatorDataPoint updated) =>
                    {
                        if (Momersion[symbol].IsReady) CheckMomersionState(symbol);
                    };
                
                ActiveStrategy.Add(symbol, MomersionState.None);
            }

            // Avoid to operate the first 15 minutes.
            Schedule.Event("MarketOpenSpan")
                .EveryDay()
                .At(9, 45)
                .Run(() =>
                {
                    isMarketOpen = true;
                    Log(string.Format("|||||||||| {0} Market Open ||||||||||", Time.DayOfWeek));
                });

            // Avoid to operate the last 5 minutes.
            Schedule.Event("MarketClose")
                .EveryDay()
                .At(15, 55)
                .Run(() =>
                {
                    isMarketOpen = false;
                    Log(string.Format("========== {0} Market Close ==========", Time.DayOfWeek));
                });
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            foreach (var symbol in Symbols)
            {
                if (isMarketOpen)
                {
                    var actualSignal = OrderSignal.doNothing;
                    var senderStrategy = MomersionState.None;

                    // Check for open positions independently of the strategy, looking for closing order signals
                    if (MomentumStrategy[symbol].Position != StockState.noInvested)
                    {
                        actualSignal = MomentumStrategy[symbol].ActualSignal;
                        senderStrategy = MomersionState.Momentum;
                    }
                    else if (MeanReversionStrategy[symbol].Position != StockState.noInvested)
                    {
                        actualSignal = MeanReversionStrategy[symbol].ActualSignal;
                        senderStrategy = MomersionState.MeanRevertion;
                    }
                    // If there isn't any open position, check for orders in the strategy actually active.
                    else
                    {
                        switch (ActiveStrategy[symbol])
                        {
                            case MomersionState.Momentum:
                                actualSignal = MomentumStrategy[symbol].ActualSignal;
                                break;

                            case MomersionState.MeanRevertion:
                                actualSignal = MeanReversionStrategy[symbol].ActualSignal;
                                break;

                            default:
                                break;
                        }
                        senderStrategy = ActiveStrategy[symbol];
                    }
                    // Finally, if there is some order, execute it.
                    if (actualSignal != OrderSignal.doNothing) ExecuteOrder(symbol, actualSignal, senderStrategy);
                }
            }
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the events</param>
        /// <remarks>
        /// This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects
        /// </remarks>
        public override void OnOrderEvent(Orders.OrderEvent orderEvent)
        {
            // Logging.
            var actualOrder = Transactions.GetOrderById(orderEvent.OrderId);
            Log(actualOrder.ToString());

            // Update the strategy object if the order status is filled.
            if (orderEvent.Status == OrderStatus.Filled)
            {
                string symbol = orderEvent.Symbol;
                int portfolioPosition = Portfolio[symbol].Quantity;
                var senderStrategy = SenderStrategy[orderEvent.OrderId];

                switch (senderStrategy)
                {
                    case MomersionState.Momentum:
                        if (portfolioPosition > 0) MomentumStrategy[symbol].Position = StockState.longPosition;
                        else if (portfolioPosition < 0) MomentumStrategy[symbol].Position = StockState.shortPosition;
                        else MomentumStrategy[symbol].Position = StockState.noInvested;
                        break;

                    case MomersionState.MeanRevertion:
                        if (portfolioPosition > 0) MeanReversionStrategy[symbol].Position = StockState.longPosition;
                        else if (portfolioPosition < 0) MeanReversionStrategy[symbol].Position = StockState.shortPosition;
                        else MeanReversionStrategy[symbol].Position = StockState.noInvested;
                        break;

                    default:
                        break;
                }
            }
            
        }

        #endregion QCAlgorithm overridden methods

        #region Algorithm methods

        /// <summary>
        /// Checks the state of the Momersion indicator and turn on the correspondent strategy for the symbol.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        private void CheckMomersionState(string symbol)
        {
            if (Momersion[symbol] > 50 &&
                ActiveStrategy[symbol] != MomersionState.Momentum)
            {
                ActiveStrategy[symbol] = MomersionState.Momentum;
                Log(string.Format("Momentum Strategy activated for {0}", symbol));
            }
            else if (Momersion[symbol] < 50 &&
                     ActiveStrategy[symbol] != MomersionState.MeanRevertion)
            {
                ActiveStrategy[symbol] = MomersionState.MeanRevertion;
                Log(string.Format("Mean Reversion Strategy activated for {0}", symbol));
            }
        }

        /// <summary>
        /// Estimates the shares quantity for the next operation. It returns the quantity SIGNED
        /// depending on the actualSignal i.e. positive if buy, negative if sell
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="actualSignal">The actual signal.</param>
        /// <returns></returns>
        private int? EstimatePosition(string symbol, OrderSignal actualSignal)
        {
            int marginAvailable;
            int quantity;
            int? operationQuantity = null;

            // Make the estimations only if the orders are to entry in the market.
            if (actualSignal == OrderSignal.goLong ||
                actualSignal == OrderSignal.goShort)
            {
                // Check the margin and the quantity to achieve target-percent holdings then choose the minimum.
                marginAvailable = (int)Math.Floor(Portfolio.MarginRemaining / Securities[symbol].Price);
                quantity = Math.Min(CalculateOrderQuantity(symbol, shareSize), marginAvailable);
                // Only assign a value to the operationQuantity if is bigger than a threshold.
                if (quantity > 10)
                {
                    // Make the quantity signed accord to the order.
                    operationQuantity = actualSignal == OrderSignal.goLong ? quantity : -quantity;
                }
            }
            return operationQuantity;
        }

        /// <summary>
        /// Executes the order.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        /// <param name="actualSignal">The actual signal.</param>
        private void ExecuteOrder(string symbol, OrderSignal actualSignal, MomersionState actualSenderStrategy)
        {
            int? shares;
            int newOrderID;

            switch (actualSignal)
            {
                case OrderSignal.goShort:
                case OrderSignal.goLong:
                    // Estimate the operation shares and submit an order only if the estimation returns not null
                    shares = EstimatePosition(symbol, actualSignal);
                    if (shares.HasValue)
                    {
                        newOrderID = Transactions.LastOrderId + 1;
                        SenderStrategy.Add(newOrderID, actualSenderStrategy);

                        MarketOrder(symbol, shares.Value);
                    }
                    break;

                case OrderSignal.closeShort:
                case OrderSignal.closeLong:
                    shares = Portfolio[symbol].Quantity;
                    newOrderID = Transactions.LastOrderId + 1;
                    SenderStrategy.Add(newOrderID, actualSenderStrategy);
                    
                    MarketOrder(symbol, -shares.Value);
                    break;

                default:
                    break;
            }
        }

        #endregion Algorithm methods
    }
}