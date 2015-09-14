using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class ProformaOrderProcessor : IOrderProcessor
    {
        public int OrdersCount { get; private set; }
        private IAlgorithm _algorithm;
        private ProformaBackTestingTransactionHandler _transactionHandler;
        public ProformaOrderProcessor(IAlgorithm algorithm, ProformaBackTestingTransactionHandler transactionHandler)
        {
            _algorithm = algorithm;
            _transactionHandler = transactionHandler;
        }
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

        public ManualResetEventSlim ProcessingCompletedEvent { get; private set; }
        public OrderTicket Process(OrderRequest request)
        {
            throw new NotImplementedException();
            switch (request.OrderRequestType)
            {
                case OrderRequestType.Submit:
                    return AddOrder((ProformaSubmitOrderRequest)request);

                case OrderRequestType.Update:
                //    return UpdateOrder((UpdateOrderRequest)request);

                case OrderRequestType.Cancel:
                    return CancelOrder((CancelOrderRequest)request);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private OrderTicket CancelOrder(CancelOrderRequest request)
        {
            throw new NotImplementedException();
            //var ticket = _transactionHandler.CancelOrder(request);

        }

        private OrderTicket AddOrder(ProformaSubmitOrderRequest request)
        {
            request.SetResponse(OrderResponse.Success(request), OrderRequestStatus.Processing);
            var ticket = _transactionHandler.AddOrder(request);

            //// send the order to be processed after creating the ticket  No don't that is the whole idea.
            //_orderRequestQueue.Enqueue(request);
            return ticket;
            
        }
    }
}
