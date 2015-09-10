using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Logging;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.Examples
{
    /// <summary>
    /// Logs an order Event after it is filled.
    /// </summary>
    public class OrderReporter
    {
        private QCAlgorithm _algorithm;
        private ILogHandler _logHandler;


        public OrderReporter(QCAlgorithm algorithm, ILogHandler logHandler)
        {
            _algorithm = algorithm;
            _logHandler = logHandler;
        }

        /// <summary>
        /// Logs the OrderEvent Transaction
        /// </summary>
        /// <param name="orderEvent">the OrderEvent being logged</param>
        /// <param name="includeHeader">Includes the field names</param>
        public OrderTransaction ReportTransaction(OrderEvent orderEvent, OrderTicket ticket, bool includeHeader = true)
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
                
                t.ActionId = orderEvent.Direction.ToString() == "Buy" ? 1 : 13;
                t.ActionNameUS = order.Direction.ToString();
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
                t.SettledDate = ticket.Time.AddDays(4);
                t.Symbol = security.Symbol;
                t.TaxLotNumber = String.Empty;
                t.TradeDate = ticket.Time;
                t.TradeNumber = 0;
                List<OrderTransaction> transactions = new List<OrderTransaction>();
                transactions.Add(t);

                var transmsgs = ObjectToCsv.ToCsv<OrderTransaction>(",", transactions, includeHeader).FirstOrDefault();
                //foreach(var transmsg in transmsgs)
                //    _logHandler.Debug(transmsg);
                //transmsg = string.Format(
                //    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                //    ticket.Symbol,
                //    ticket.QuantityFilled,
                //    ticket.AverageFillPrice,
                //    orderEvent.Direction.ToString(),
                //    ticket.Time,
                //    ticket.Time.AddDays(4),
                //    orderValue,
                //    orderFees,
                //    orderValue + orderFees,   // Order fees are deducted from the order 
                //    "",
                //    orderEvent.Direction + " share of " + orderEvent.Symbol + "at $" + order.Price.ToString(),
                //    actionid,
                //    order.Id,
                //    "Trade",
                //    "taxlot",
                //    ""
                //    );
                }
            
            #endregion

            return t;
        }
    }
}