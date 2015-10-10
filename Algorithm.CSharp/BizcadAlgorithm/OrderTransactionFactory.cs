using System;
using System.Linq;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Orders;
using QuantConnect.Securities.Equity;

namespace QuantConnect.Algorithm
{
    /// <summary>
    /// Creates an OrderTransaction
    /// </summary>
    public class OrderTransactionFactory
    {
        private QCAlgorithm _algorithm;

        public OrderTransactionFactory(QCAlgorithm algorithm)
        {
            _algorithm = algorithm;
        }

        /// <summary>
        /// Logs the OrderEvent Transaction
        /// </summary>
        /// <param name="orderEvent">the OrderEvent being logged</param>
        /// <param name="includeHeader">Includes the field names</param>
        public OrderTransaction Create(OrderEvent orderEvent, OrderTicket ticket, bool includeHeader = true)
        {
            var security = _algorithm.Securities[ticket.Symbol];

            Order order = _algorithm.Transactions.GetOrderById(orderEvent.OrderId);
            OrderTransaction t = new OrderTransaction();

            // According to Scottrade a Buy is a negative amount (funds flow from my account to the seller's)
            //  However the Quantity filled is a negative number for Sell/Short and a positive for Buy/Long
            //  So multiply by -1 to give order value the correct sign
            decimal orderValue = -1 * ticket.QuantityFilled * ticket.AverageFillPrice;

            if (order != null)
            {
                var orderDateTime = _algorithm.Time;
                DateTime settleDate = orderDateTime.AddDays(orderDateTime.DayOfWeek < DayOfWeek.Wednesday ? 3 : 5);

                // Order Fees are a cost and negative to my account, therefore a negative number
                var orderFees = security.TransactionModel.GetOrderFee(security, order) * -1;

                #region "Create OrderTransaction"

                t.ActionId = orderEvent.Direction.ToString() == "Buy" ? 1 : 13;
                t.ActionNameUS = orderEvent.Direction.ToString();
                t.Amount = orderValue;
                t.Broker = "IB";
                t.CUSIP = "CUSIP";
                t.Commission = orderFees;
                t.Description = string.Format("{0} {1} shares of {2} at ${3}", orderEvent.Direction, ticket.Quantity, orderEvent.Symbol, order.Price);
                t.Direction = orderEvent.Direction;
                t.Exchange = "";
                t.Fees = 0;  // need to calculate based upon difference in Portfolio[symbol].HoldingsValue between buy and sell
                t.Id = 0;
                t.Interest = 0;
                t.Net = orderValue + orderFees;
                t.OrderId = order.Id;
                t.OrderType = ticket.OrderType;
                t.Price = ticket.AverageFillPrice;
                t.Quantity = ticket.Quantity;
                t.RecordType = "Trade";
                t.SettledDate = settleDate;
                t.Symbol = ticket.Symbol;
                t.TaxLotNumber = String.Empty;
                t.TradeDate = orderDateTime;
                t.TradeNumber = 0;
                #endregion
            }
            return t;
        }
        /// <summary>
        /// Logs the OrderEvent Transaction
        /// </summary>
        /// <param name="orderEvent">the OrderEvent being logged</param>
        /// <param name="includeHeader">Includes the field names</param>
        //public OrderTransaction Create(BrokerSimulator sim, ProformaOrderTicket ticket)
        //{
        //    //var security = _algorithm.Securities[ticket.Symbol];

        //    ProformaOrderTicket orderTicket = sim.GetTicketByOrderId(ticket.OrderId);
        //    OrderTransaction t = new OrderTransaction();

        //    // According to Scottrade a Buy is a negative amount (funds flow from my account to the seller's)
        //    //  However the Quantity filled is a negative number for Sell/Short and a positive for Buy/Long
        //    //  So multiply by -1 to give order value the correct sign
        //    decimal orderValue = -1 * ticket.QuantityFilled * ticket.AverageFillPrice;

        //    if (orderTicket != null)
        //    {
        //        // Order Fees are a cost and negative to my account, therefore a negative number
        //        // ***************************************************
        //        // Get the IB fee.  
        //        //   Use ConstantFeeTransactionModel for Tradier
        //        //   and ForexTransactionModel for Forex
        //        // ***************************************************
        //        EquityTransactionModel transactionModel = new EquityTransactionModel();
        //        var orderFees = sim.GetOrderFee(ticket) * -1;
        //        var orderDateTime = orderTicket.TicketTime;

        //        #region "Create OrderTransaction"
        //        t.Direction = ticket.Direction;

        //        t.ActionId = ticket.Direction == OrderDirection.Buy  ? 1 : 13;
        //        t.ActionNameUS = ticket.Direction == OrderDirection.Buy ? "Buy" : "Sell";
        //        t.Amount = orderValue;
        //        t.Broker = "IB";
        //        t.CUSIP = "CUSIP";
        //        t.Commission = orderFees;
        //        t.Description = string.Format("{0} {1} shares of {2} at ${3}", t.Direction, ticket.Quantity, ticket.Symbol, ticket.AverageFillPrice);
        //        t.Exchange = "";
        //        t.Fees = 0;  // need to calculate based upon difference in Portfolio[symbol].HoldingsValue between buy and sell
        //        t.Id = 0;
        //        t.Interest = 0;
        //        t.Net = orderValue + orderFees;
        //        t.OrderId = ticket.OrderId;
        //        t.OrderType = ticket.TicketOrderType;
        //        t.Price = ticket.AverageFillPrice;
        //        t.Quantity = ticket.Quantity;
        //        t.RecordType = "Trade";
        //        t.SettledDate = ticket.TicketTime.AddDays(4);
        //        t.Symbol = ticket.Symbol;
        //        t.TaxLotNumber = ticket.Source;
        //        t.TradeDate = ticket.TicketTime;
        //        t.TradeNumber = 0;
        //        #endregion
        //    }
        //    return t;
        //}

    }
}