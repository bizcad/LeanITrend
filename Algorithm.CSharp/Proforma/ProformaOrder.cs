using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class ProformaOrder : Order
    {
        public int OrderId;
        public OrderType orderType = Orders.OrderType.Market;
        public decimal CurrentMarketPrice { get; set; }
        public decimal LimitPrice { get; set; }
        public new int Quantity { get; set; }
        public OrderStatus OrderStatus { get; set; }
        //private QuantConnect.Orders.Order order;
        ///// <summary>
        ///// Specify to update the limit price of the order
        ///// </summary>
        //public decimal? LimitPrice { get; set; }

        ///// <summary>
        ///// Specify to update the stop price of the order
        ///// </summary>
        //public decimal? StopPrice { get; set; }

        ///// <summary>
        ///// Specify to update the order's tag
        ///// </summary>
        public new string Tag { get; set; }

        /// <summary>
        /// Stop price for this stop market order.
        /// </summary>
        public decimal StopPrice;

        public override OrderType Type
        {
            get { return orderType; }
        }

        public override decimal Value
        {
            get
            {
                decimal value = 0;
                switch (orderType)
                {
                    case OrderType.Market:
                        return Quantity * CurrentMarketPrice;
                    case OrderType.Limit:
                    case OrderType.StopLimit:
                        return Quantity * LimitPrice;
                    case OrderType.MarketOnClose:
                        return Math.Abs(Quantity)* Price;
                        
                    case OrderType.MarketOnOpen:
                        return Math.Abs(Quantity)* Price;
                    
                        
                    case OrderType.StopMarket:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();

                }
                throw new ArgumentOutOfRangeException();
            }
        }

        /// </summary>
        /// <param name="symbol">Symbol asset we're seeking to trade</param>
        /// <param name="type">Type of the security order</param>
        /// <param name="quantity">Quantity of the asset we're seeking to trade</param>
        /// <param name="time">Time the order was placed</param>
        /// <param name="tag">User defined data tag for this order</param>
        public ProformaOrder(ProformaSubmitOrderRequest request) : base (request.Symbol, request.SecurityType, request.Quantity, request.Time, request.Tag)
        {
            
            OrderId = request.OrderId;
            Quantity = request.Quantity;
            StopPrice = request.StopPrice;
            orderType = request.OrderType;
            LimitPrice = request.LimitPrice;
            OrderStatus = request.OrderStatus;
            this.Tag = request.Tag;
        }



        public override decimal GetValue(decimal currentMarketPrice)
        {
            return Quantity * currentMarketPrice;
        }

        public override Order Clone()
        {
            throw new NotImplementedException();
        }

        public QuantConnect.Orders.Order CreateOrder(ProformaSubmitOrderRequest request)
        {
            var order = QuantConnect.Orders.Order.CreateOrder((SubmitOrderRequest)request);
            return order;
        }

        public int GetOrderId()
        {
            return OrderId;
        }

    }
}
