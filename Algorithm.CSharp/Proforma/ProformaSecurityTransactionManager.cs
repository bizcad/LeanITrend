using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class ProformaSecurityTransactionManager : SecurityTransactionManager
    {

        private int _orderId;
        private readonly SecurityManager _securities;
        private const decimal _minimumOrderSize = 0;
        private const int _minimumOrderQuantity = 1;

        private IOrderProcessor _orderProcessor;
        private Dictionary<DateTime, decimal> _transactionRecord;

        public ProformaSecurityTransactionManager(SecurityManager security) : base(security)
        {
        }

        public int OrdersCount { get; private set; }
        public Order GetOrderById(int orderId)
        {
            throw new NotImplementedException();
        }

        public Order GetOrderByBrokerageId(int brokerageId)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<OrderTicket> GetOrderTickets(Func<OrderTicket, bool> filter = null)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Order> GetOrders(Func<Order, bool> filter = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets the <see cref="IOrderProvider"/> used for fetching orders for the algorithm
        /// </summary>
        /// <param name="orderProvider">The <see cref="IOrderProvider"/> to be used to manage fetching orders</param>
        public void SetOrderProcessor(IOrderProcessor orderProvider)
        {
            _orderProcessor = orderProvider;
        }

        /// <summary>
        /// Returns true when the specified order is in a completed state
        /// </summary>
        private static bool Completed(Order order)
        {
            return order.Status == OrderStatus.Filled || order.Status == OrderStatus.Invalid || order.Status == OrderStatus.Canceled;
        }

        public OrderTicket AddOrder(ProformaSubmitOrderRequest request)
        {
            return ProcessRequest(request);
        }

        private OrderTicket ProcessRequest(ProformaSubmitOrderRequest request)
        {
            var submit = request as ProformaSubmitOrderRequest;
            if (submit != null)
            {
                submit.SetOrderId(GetIncrementOrderId());
            }
            return _orderProcessor.Process(request);
        }

        public new void WaitForOrder(int orderId)
        {
            // wait for the processor to finish processing his orders
            while (true)
            {
                var order = GetOrderById(orderId);
                if (order == null || !Completed(order))
                {
                    if (order != null && order.Type != OrderType.Market)
                    {
                        // can't wait for non-market orders to fill
                        return;
                    }
                    Thread.Sleep(1);
                }
                else
                {
                    break;
                }
            }

        }
    }
}
