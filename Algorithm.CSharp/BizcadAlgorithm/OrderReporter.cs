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


        public OrderReporter(QCAlgorithm algorithm, ILogHandler transactionlog)
        {
            _algorithm = algorithm;
            _logHandler = transactionlog;
        }

        /// <summary>
        /// Logs the OrderEvent Transaction
        /// </summary>
        /// <param name="orderEvent">the OrderEvent being logged</param>
        public void ReportTransaction(OrderEvent orderEvent, OrderTicket ticket)
        {
            #region Scottrade

            string transmsg = string.Format("Order {0} on not found", orderEvent.OrderId);
            Order order = _algorithm.Transactions.GetOrderById(orderEvent.OrderId);
            decimal orderValue = ticket.QuantityFilled * ticket.AverageFillPrice;
            

            if (order != null)
            {
                var orderDateTime = order.Time;
                var orderFees = _algorithm.Securities[order.Symbol].TransactionModel.GetOrderFee(_algorithm.Securities[order.Symbol], order);
                int actionid = orderEvent.Direction.ToString() == "Buy" ? 1 : 13;
                transmsg = string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                    orderEvent.Symbol,
                    orderEvent.FillQuantity,
                    orderEvent.FillPrice,
                    orderEvent.Direction.ToString(),
                    order.Time,
                    order.Time.AddDays(4),
                    orderValue,
                    orderFees,
                    orderValue + orderFees,
                    "",
                    orderEvent.Direction + " share of " + orderEvent.Symbol + "at $" + order.Price.ToString(),
                    actionid,
                    order.Id,
                    "Trade",
                    "taxlot",
                    ""
                    );
                }
            _logHandler.Debug(transmsg);
            #endregion
        }
    }
}