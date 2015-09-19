using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class ProformaOrderTicket
    {
        #region Commented
        //private readonly object _orderEventsLock = new object();
        //private readonly object _updateRequestsLock = new object();
        //private readonly object _setCancelRequestLock = new object();

        //public ProformaOrder _order { get; set; }
        //private OrderStatus? _orderStatusOverride;
        //private CancelOrderRequest _cancelRequest;
        //private OrderType _orderType { get; set; }
        //public decimal LimitPrice { get; set; }
        //public decimal StopPrice { get; set; }
        

        //private int _quantityFilled;
        //private decimal _averageFillPrice;

        //private readonly int _orderId;
        //private readonly List<OrderEvent> _orderEvents;
        //private readonly SubmitOrderRequest _submitRequest;
        //private readonly List<UpdateOrderRequest> _updateRequests;
        //private SecurityTransactionManager _transactionManager;
        #endregion
        public int OrderId { get; set; }
        public OrderStatus Status { get; set; }
        public string Symbol { get; set; }
        public SecurityType Security_Type { get; set; }
        public int Quantity { get; set; }
        public decimal AverageFillPrice { get; set; }
        public int QuantityFilled { get; set; }
        public DateTime TicketTime { get; set; }
        public OrderType TickeOrderType { get; set; }
        public string Tag { get; set; }
        public string ErrorMessage { get; set; }
        public decimal LimitPrice { get; set; }
        public decimal StopPrice { get; set; }


        /// <summary>
        /// Gets a list of all order events for this ticket
        /// </summary>
        //public IReadOnlyList<OrderEvent> OrderEvents
        //{
        //    get
        //    {
        //        lock (_orderEventsLock)
        //        {
        //            return _orderEvents.ToList();
        //        }
        //    }
        //}

        
        #region Commented
        ///// <summary>
        ///// Adds an order event to this ticket
        ///// </summary>
        ///// <param name="orderEvent">The order event to be added</param>
        //internal void AddOrderEvent(OrderEvent orderEvent)
        //{
        //    lock (_orderEventsLock)
        //    {
        //        _orderEvents.Add(orderEvent);
        //        if (orderEvent.FillQuantity != 0)
        //        {
        //            // keep running totals of quantity filled and the average fill price so we
        //            // don't need to compute these on demand
        //            _quantityFilled += orderEvent.FillQuantity;
        //            var quantityWeightedFillPrice = _orderEvents.Where(x => x.Status.IsFill()).Sum(x => x.FillQuantity * x.FillPrice);
        //            _averageFillPrice = quantityWeightedFillPrice / _quantityFilled;
        //        }
        //    }
        //}

        ///// <summary>
        ///// Updates the internal order object with the current state
        ///// </summary>
        ///// <param name="order">The order</param>
        //internal void SetOrder(ProformaOrder order)
        //{
        //    if (_order != null && _order.Id != order.Id)
        //    {
        //        throw new ArgumentException("Order id mismatch");
        //    }

        //    _order = order;
        //}

        ///// <summary>
        ///// Adds a new <see cref="UpdateOrderRequest"/> to this ticket.
        ///// </summary>
        ///// <param name="request">The recently processed <see cref="UpdateOrderRequest"/></param>
        //internal void AddUpdateRequest(UpdateOrderRequest request)
        //{
        //    if (request.OrderId != OrderId)
        //    {
        //        throw new ArgumentException("Received UpdateOrderRequest for incorrect order id.");
        //    }

        //    lock (_updateRequestsLock)
        //    {
        //        _updateRequests.Add(request);
        //    }
        //}

        ///// <summary>
        ///// Sets the <see cref="CancelOrderRequest"/> for this ticket. This can only be performed once.
        ///// </summary>
        ///// <remarks>
        ///// This method is thread safe.
        ///// </remarks>
        ///// <param name="request">The <see cref="CancelOrderRequest"/> that canceled this ticket.</param>
        ///// <returns>False if the the CancelRequest has already been set, true if this call set it</returns>
        //internal bool TrySetCancelRequest(CancelOrderRequest request)
        //{
        //    if (request.OrderId != OrderId)
        //    {
        //        throw new ArgumentException("Received CancelOrderRequest for incorrect order id.");
        //    }
        //    lock (_setCancelRequestLock)
        //    {
        //        if (_cancelRequest != null)
        //        {
        //            return false;
        //        }
        //        _cancelRequest = request;
        //    }
        //    return true;
        //}

        ///// <summary>
        ///// Creates a new <see cref="OrderTicket"/> that represents trying to cancel an order for which no ticket exists
        ///// </summary>
        //public static ProformaOrderTicket InvalidCancelOrderId(SecurityTransactionManager transactionManager, CancelOrderRequest request)
        //{
        //    var submit = new SubmitOrderRequest(OrderType.Market, SecurityType.Base, string.Empty, 0, 0, 0, DateTime.MaxValue, string.Empty);
        //    submit.SetResponse(OrderResponse.UnableToFindOrder(request));
        //    var ticket = new ProformaOrderTicket(transactionManager, submit);
        //    request.SetResponse(OrderResponse.UnableToFindOrder(request));
        //    //ticket.TrySetCancelRequest(request);
        //    ticket._orderStatusOverride = OrderStatus.Invalid;
        //    return ticket;
        //}

        ///// <summary>
        ///// Creates a new <see cref="OrderTicket"/> tht represents trying to update an order for which no ticket exists
        ///// </summary>
        //public static ProformaOrderTicket InvalidUpdateOrderId(SecurityTransactionManager transactionManager, UpdateOrderRequest request)
        //{
        //    var submit = new SubmitOrderRequest(OrderType.Market, SecurityType.Base, string.Empty, 0, 0, 0, DateTime.MaxValue, string.Empty);
        //    submit.SetResponse(OrderResponse.UnableToFindOrder(request));
        //    var ticket = new ProformaOrderTicket(transactionManager, submit);
        //    request.SetResponse(OrderResponse.UnableToFindOrder(request));
        //    //ticket.AddUpdateRequest(request);
        //    ticket._orderStatusOverride = OrderStatus.Invalid;
        //    return ticket;
        //}

        ///// <summary>
        ///// Creates a new <see cref="OrderTicket"/> that represents trying to submit a new order that had errors embodied in the <paramref name="response"/>
        ///// </summary>
        //public static ProformaOrderTicket InvalidSubmitRequest(SecurityTransactionManager transactionManager, SubmitOrderRequest request, OrderResponse response)
        //{
        //    request.SetResponse(response);
        //    return new ProformaOrderTicket(transactionManager, request) { _orderStatusOverride = OrderStatus.Invalid };
        //}

        ///// <summary>
        ///// Returns a string that represents the current object.
        ///// </summary>
        ///// <returns>
        ///// A string that represents the current object.
        ///// </returns>
        ///// <filterpriority>2</filterpriority>
        //public override string ToString()
        //{
        //    return "";

        //}

        //private int ResponseCount()
        //{
        //    return (_submitRequest.Response == OrderResponse.Unprocessed ? 0 : 1)
        //         + (_cancelRequest == null || _cancelRequest.Response == OrderResponse.Unprocessed ? 0 : 1)
        //         + _updateRequests.Count(x => x.Response != OrderResponse.Unprocessed);
        //}

        //private int RequestCount()
        //{
        //    return 1 + _updateRequests.Count + (_cancelRequest == null ? 0 : 1);
        //}

        ///// <summary>
        ///// This is provided for API backward compatibility and will resolve to the order ID, except during
        ///// an error, where it will return the integer value of the <see cref="OrderResponseErrorCode"/> from
        ///// the most recent response
        ///// </summary>
        //public static implicit operator int(ProformaOrderTicket ticket)
        //{
        //    var response = ticket.GetMostRecentOrderResponse();
        //    if (response != null && response.IsError)
        //    {
        //        return (int)response.ErrorCode;
        //    }
        //    return ticket.OrderId;
        //}


        //private static decimal AccessOrder<T>(ProformaOrderTicket ticket, OrderField field, Func<T, decimal> orderSelector, Func<SubmitOrderRequest, decimal> requestSelector)
        //    where T : ProformaOrder
        //{
        //    var order = ticket._order;
        //    if (order == null)
        //    {
        //        return requestSelector(ticket._submitRequest);
        //    }
        //    var typedOrder = order as T;
        //    if (typedOrder != null)
        //    {
        //        return orderSelector(typedOrder);
        //    }
        //    throw new ArgumentException(string.Format("Unable to access property {0} on order of type {1}", field, order.Type));
        //}
        #endregion
    }
}
