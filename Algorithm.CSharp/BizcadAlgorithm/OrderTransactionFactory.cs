using System;
using System.Linq;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Orders;

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
                var orderDateTime = order.Time;
                // Order Fees are a cost and negative to my account, therefore a negative number
                var orderFees = security.TransactionModel.GetOrderFee(security, order) * -1;

                #region "Create OrderTransaction"

                var x = ticket.OrderEvents.FirstOrDefault();
                t.ActionId = x.Direction.ToString() == "Buy" ? 1 : 13;
                t.ActionId = orderEvent.Direction.ToString() == "Buy" ? 1 : 13;
                t.ActionNameUS = order.Direction.ToString();
                t.Amount = orderValue;
                t.Broker = "IB";
                t.CUSIP = "CUSIP";
                t.Commission = orderFees;
                t.Description = string.Format("{0} {1} shares of {2} at ${3}", orderEvent.Direction, ticket.Quantity, orderEvent.Symbol, order.Price);
                t.Direction = orderEvent.Direction;
                t.Direction = x.Direction;
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
                t.SettledDate = ticket.Time.AddDays(4);
                t.Symbol = security.Symbol;
                t.TaxLotNumber = String.Empty;
                t.TradeDate = ticket.Time;
                t.TradeNumber = 0;
                #endregion
            }
            return t;
        }
    }
}