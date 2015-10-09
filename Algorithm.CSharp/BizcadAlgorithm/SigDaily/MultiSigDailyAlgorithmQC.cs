using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class MultiSigDailyAlgorithmQc : QCAlgorithm
    {
        private int LiveSignalIndex = 8;

        #region "Variables"

        private DateTime _startDate = new DateTime(2015, 1, 11);
        private DateTime _endDate = new DateTime(2015, 10, 4);
        private decimal _portfolioAmount = 10000;
        private decimal _transactionSize = 15000;
        //+----------------------------------------------------------------------------------------+
        //  Algorithm Control Panel                         
        // +---------------------------------------------------------------------------------------+
        private static int ITrendPeriod = 7; // Instantaneous Trend period.
        private static decimal Tolerance = 0.000m; // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m; // Percentage tolerance before revert position.

        private static decimal maxLeverage = 1m; // Maximum Leverage.
        private decimal leverageBuffer = 0.00m; // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500; // Maximum shares per operation.

        private decimal RngFac = 0.35m; // Percentage of the bar range used to estimate limit prices.

        private bool resetAtEndOfDay = true; // Reset the strategies at EOD.
        private bool noOvernight = true; // Close all positions before market close.
        // +---------------------------------------------------------------------------------------+

        private Symbol symbol = new Symbol("AAPL");
        //private string symbol = "AAPL";

        private int barcount = 0;

        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;
        private List<OrderTicket> _ticketsQueue;

        #region lists

        private List<SignalInfo> signalInfos = new List<SignalInfo>();

        #endregion

        private string comment;

        #endregion

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {

            //Initialize dates
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, symbol, Resolution.Daily);

            // Indicators
            Price = new RollingWindow<IndicatorDataPoint>(14); // The price history
            trendHistory = new RollingWindow<IndicatorDataPoint>(14);
            // ITrend
            trend = new InstantaneousTrend(7);
            _ticketsQueue = new List<OrderTicket>();

            #region lists

            signalInfos.Add(new SignalInfo
            {
                Id = 8,
                IsActive = false,
                SignalJson = string.Empty,
                Value = OrderSignal.doNothing,
                InternalState = string.Empty,
                SignalType = typeof(Sig8)
            });

            foreach (SignalInfo s in signalInfos)
            {
                s.IsActive = false;
                if (s.Id == LiveSignalIndex)
                {
                    s.IsActive = true;
                }
            }

            #endregion


            // for use with Tradier. Default is IB.
            //var security = Securities[symbol];
            //security.TransactionModel = new ConstantFeeTransactionModel(1.0m);

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            barcount++;

            // Add the history for the bar
            var time = this.Time;
            Price.Add(idp(time, (data[symbol].Close + data[symbol].Open) / 2));

            // Update the indicators
            trend.Update(idp(time, Price[0].Value));
            trendHistory.Add(CalculateNewTrendHistoryValue(barcount, time, Price, trend));
            GetOrderSignals(data);

            // Execute only the selected strategy with it's orderSignal
            foreach (SignalInfo signal in signalInfos)
            {
                if (signal.Value != OrderSignal.doNothing)
                {
                    if (signal.IsActive)
                        ExecuteStrategy(symbol, signal, data);
                }
            }
        }


        /// <summary>
        /// Run the strategy associated with this algorithm
        /// </summary>
        /// <param name="data">TradeBars - the data received by the OnData event</param>
        private void GetOrderSignals(TradeBars data)
        {
            #region "GetOrderSignals Execution"

            List<ProformaOrderTicket> handledTickets = HandleTickets();

            
            #region lists

            foreach (SignalInfo info in signalInfos)
            {
                var id = info.Id;
                info.Value = OrderSignal.doNothing;
                Type t = info.SignalType;
                var sig = Activator.CreateInstance(t) as ISigSerializable;
                if (sig != null)
                {
                    sig.symbol = symbol;
                    if (barcount > 1)
                    {
                        sig.Deserialize(info.SignalJson);
                    }
                    sig.Barcount = barcount; // for debugging

                    // Todo: handle partial fills.

                    decimal entryPrice = sig.nEntryPrice;

                    // set the properties from the handled ticket.
                    var handledTicket = handledTickets.FirstOrDefault(h => System.Convert.ToInt32(h.Tag) == info.Id);
                    if (handledTicket != null)
                    {
                        // sig.orderFilled defaults to true in the three constructors
                        sig.orderFilled = handledTicket.Status == OrderStatus.Filled;
                        if (Portfolio[symbol].HoldStock && handledTicket.Status == OrderStatus.Filled)
                        {
                            // Remember sig.nEntryPrice is carried forward as is xOver
                            entryPrice = handledTicket.AverageFillPrice;
                        }

                        // Remember we are only removing from the _ticketsQueue where the info.Id is matched
                        OrderTicket orderTicket = _ticketsQueue.FirstOrDefault(z => z.OrderId == handledTicket.OrderId);
                        _ticketsQueue.Remove(orderTicket);
                    }

                    sig.IsLong = Portfolio[symbol].IsLong;
                    sig.IsShort = Portfolio[symbol].IsShort;
                    sig.nEntryPrice = entryPrice;

                    Dictionary<string, string> paramlist = new Dictionary<string, string>
                    {
                        {"symbol", symbol.ToString()},
                        {"Barcount", barcount.ToString(CultureInfo.InvariantCulture)},
                        {"nEntryPrice", entryPrice.ToString(CultureInfo.InvariantCulture)},
                        {"IsLong", Portfolio[symbol].IsLong.ToString()},
                        {"IsShort", Portfolio[symbol].IsShort.ToString()},
                        {"trend", trend.Current.Value.ToString(CultureInfo.InvariantCulture)}
                    };

                    info.Value = sig.CheckSignal(data, paramlist, out comment);
                    info.Comment = comment;
                }

                if (barcount >= 0)
                {
                    string json = sig.Serialize();
                    info.SignalJson = json;
                }
                info.InternalState = sig.ToCsv() + "," + sig.GetInternalStateFields().ToString();
            }
            #endregion  // lists
            #endregion  // execution
        }

        #region "Event Processiong"
        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            Debug(string.Format("\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n UseSig = {3}", this.GetType().Name, symbol, Portfolio.TotalPortfolioValue, LiveSignalIndex));
        }

        #endregion


        #region "Methods"
        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="actualOrder">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(Symbol symbol, SignalInfo actualOrder, TradeBars data)
        {
            decimal limitPrice = 0m;
            int shares = PositionShares(symbol, actualOrder);
            ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
            OrderTicket ticket;
            switch (actualOrder.Value)
            {
                case OrderSignal.goLongLimit:
                    // Define the limit price.
                    limitPrice = priceCalculator.Calculate(data[symbol], actualOrder, RngFac);
                    ticket = LimitOrder(symbol, shares, limitPrice, actualOrder.Id.ToString(CultureInfo.InvariantCulture));

                    _ticketsQueue.Add(ticket);

                    break;

                case OrderSignal.goShortLimit:
                    limitPrice = priceCalculator.Calculate(data[symbol], actualOrder, RngFac);
                    ticket = LimitOrder(symbol, shares, limitPrice, actualOrder.Id.ToString(CultureInfo.InvariantCulture));
                    _ticketsQueue.Add(ticket);
                    break;

                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    ticket = MarketOrder(symbol, shares, false, actualOrder.Id.ToString(CultureInfo.InvariantCulture));
                    _ticketsQueue.Add(ticket);
                    break;
            }
        }


        /// <summary>
        /// This function is called prior to calling the GetOrderSignals to check to see if a ticket was filled
        /// If the ticket did not fill within one bar, cancel it and assume the market moved away from the limit order
        /// 
        /// </summary>

        private List<ProformaOrderTicket> HandleTickets()
        {
            List<ProformaOrderTicket> proformaOrderTickets = new List<ProformaOrderTicket>();

            foreach (OrderTicket queuedTicket in _ticketsQueue)
            {
                ProformaOrderTicket proformaLiveTicket = new ProformaOrderTicket();

                // Check the ticket against the Transactions version of the ticket
                OrderTicket liveticket = Transactions.GetOrderTickets(t => t.OrderId == queuedTicket.OrderId).FirstOrDefault();

                if (liveticket != null)
                {
                    proformaLiveTicket.Status = liveticket.Status;
                    proformaLiveTicket.OrderId = liveticket.OrderId;
                    proformaLiveTicket.Symbol = liveticket.Symbol;
                    proformaLiveTicket.Source = liveticket.Tag;
                    proformaLiveTicket.TicketTime = liveticket.Time;
                    proformaLiveTicket.Security_Type = liveticket.SecurityType;
                    proformaLiveTicket.Tag = liveticket.Tag;
                    proformaLiveTicket.TicketOrderType = liveticket.OrderType;
                    proformaLiveTicket.Direction = liveticket.Quantity > 0
                        ? OrderDirection.Buy
                        : OrderDirection.Sell;

                    if (liveticket.Status == OrderStatus.Submitted)
                    {
                        liveticket.Cancel();
                        proformaLiveTicket.Status = OrderStatus.Canceled;
                        proformaLiveTicket.QuantityFilled = 0; // they are probably already 0
                        proformaLiveTicket.AverageFillPrice = 0;
                    }
                    // ToDo:  Handle partial tickets
                    if (liveticket.Status == OrderStatus.Filled)
                    {
                        proformaLiveTicket.QuantityFilled = (int)liveticket.QuantityFilled;
                        proformaLiveTicket.AverageFillPrice = liveticket.AverageFillPrice;
                    }
                }
                else
                {
                    proformaLiveTicket.ErrorMessage =
                        string.Format("Ticket with Id {0} could not be found in Transactions.", queuedTicket.OrderId);


                }
                proformaOrderTickets.Add(proformaLiveTicket);
            }
            return proformaOrderTickets;
        }

        private decimal GetBetSize(Symbol symbol, SignalInfo signalInfo)
        {
            // *********************************
            //  ToDo: Kelly Goes here in a custom bet sizer
            //  This implementation uses the same as the original algo
            //    and just refactors it out to a class.
            // *********************************
            IBetSizer allocator = new InstantTrendBetSizer(this);
            //if (!signalInfo.IsActive)
            //    return allocator.BetSize(symbol, Price[0].Value, _transactionSize, signalInfo, _proformaProcessor);
            return allocator.BetSize(symbol, Price[0].Value, _transactionSize, signalInfo);
        }

        /// <summary>
        /// Estimate number of shares, given a kind of operation.
        /// </summary>
        /// <param name="symbol">The symbol to operate.</param>
        /// <param name="order">The kind of order.</param>
        /// <returns>The signed number of shares given the operation.</returns>
        public int PositionShares(Symbol symbol, SignalInfo signalInfo)
        {
            int quantity = 0;
            int operationQuantity;

            decimal targetSize = GetBetSize(symbol, signalInfo);
            switch (signalInfo.Value)
            {
                case OrderSignal.goLongLimit:
                case OrderSignal.goLong:
                    //operationQuantity = CalculateOrderQuantity(symbol, targetSize);     // let the algo decide on order quantity
                    operationQuantity = (int)targetSize;
                    quantity = Math.Min(maxOperationQuantity, operationQuantity);
                    break;

                case OrderSignal.goShortLimit:
                case OrderSignal.goShort:
                    operationQuantity = (int)targetSize;
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

            if (quantity == 0)
                System.Diagnostics.Debug.WriteLine("x");
            return quantity;
        }


        private IndicatorDataPoint CalculateNewTrendHistoryValue(int barcount, DateTime time, RollingWindow<IndicatorDataPoint> price, InstantaneousTrend tr)
        {
            if (!trendHistory.IsReady)
                return idp(time, price[0].Value);
            return (idp(time, tr.Current.Value)); //add last iteration value for the cycle
        }

        #endregion

        /// <summary>
        /// Convenience function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from 
        /// new IndicatorDataPoint(this.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

    }

}
