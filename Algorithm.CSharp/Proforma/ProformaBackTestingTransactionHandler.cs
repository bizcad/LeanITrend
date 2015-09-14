using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;

namespace QuantConnect.Lean.Engine
{
    public class ProformaBackTestingTransactionHandler 
    {
        private IBrokerage _brokerage;
        private IAlgorithm _algorithm;
        private ConcurrentDictionary<int, Order> _orders;
        private ConcurrentQueue<OrderRequest> _orderRequestQueue;
        private ConcurrentDictionary<int, OrderTicket> _orderTickets;
        public int OrdersCount { get; private set; }

        public ProformaBackTestingTransactionHandler(IAlgorithm algorithm)
        {
            _algorithm = algorithm;
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
        }

        public bool IsActive { get; private set; }
        public ConcurrentDictionary<int, Order> Orders { get; private set; }
//        public void Initialize(IAlgorithm algorithm, IBrokerage brokerage, IResultHandler resultHandler)
        public void Initialize(IAlgorithm algorithm, IBrokerage brokerage)
        {
            if (brokerage == null)
            {
                throw new ArgumentNullException("brokerage");
            }
            _brokerage = brokerage;

            IsActive = true;

            _algorithm = algorithm;
            _orders = new ConcurrentDictionary<int, Order>();
            _orderRequestQueue = new ConcurrentQueue<OrderRequest>();
            _orderTickets = new ConcurrentDictionary<int, OrderTicket>();

            
        }

        public void Run()
        {
            throw new NotImplementedException();
        }

        public void Exit()
        {
            throw new NotImplementedException();
        }

        public void ProcessSynchronousEvents()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Add an order to collection and return the unique order id or negative if an error.
        /// </summary>
        /// <param name="request">A request detailing the order to be submitted</param>
        /// <returns>New unique, increasing orderid</returns>
        public OrderTicket AddOrder(ProformaSubmitOrderRequest request)
        {
            request.SetResponse(OrderResponse.Success(request), OrderRequestStatus.Processing);
            var ticket = new OrderTicket(_algorithm.Transactions, request);
            _orderTickets.TryAdd(ticket.OrderId, ticket);

            // send the order to be processed after creating the ticket
            //_orderRequestQueue.Enqueue(request);
            return ticket;
        }

        public object CancelOrder(CancelOrderRequest request)
        {
            OrderTicket ticket;
            if (!_orderTickets.TryGetValue(request.OrderId, out ticket))
            {
                Log.Error("BrokerageTransactionHandler.CancelOrder(): Unable to locate ticket for order.");
                return OrderTicket.InvalidCancelOrderId(_algorithm.Transactions, request);
            }

            try
            {
                
                // if we couldn't set this request as the cancellation then another thread/someone
                // else is already doing it or it in fact has already been cancelled
                //if (!ticket.TrySetCancelRequest(request))
                //{
                //    // the ticket has already been cancelled
                //    request.SetResponse(OrderResponse.Error(request, OrderResponseErrorCode.InvalidRequest, "Cancellation is already in progress."));
                //    return ticket;
                //}

                //Error check
                var order = GetOrderById(request.OrderId);
                if (order == null)
                {
                    Log.Error("BrokerageTransactionHandler.CancelOrder(): Cannot find this id.");
                    request.SetResponse(OrderResponse.UnableToFindOrder(request));
                }
                else if (order.Status.IsClosed())
                {
                    Log.Error("BrokerageTransactionHandler.CancelOrder(): Order already " + order.Status);
                    request.SetResponse(OrderResponse.InvalidStatus(request, order));
                }
                else
                {
                    // send the request to be processed
                    request.SetResponse(OrderResponse.Success(request), OrderRequestStatus.Processing);
                    _orderTickets.TryRemove(ticket.OrderId, out ticket);
                    
                    //_orderRequestQueue.Enqueue(request);
                }
            }
            catch (Exception err)
            {
                Log.Error("TransactionManager.RemoveOrder(): " + err.Message);
                request.SetResponse(OrderResponse.Error(request, OrderResponseErrorCode.ProcessingError, err.Message));
            }

            return ticket;
        }
    }
}
