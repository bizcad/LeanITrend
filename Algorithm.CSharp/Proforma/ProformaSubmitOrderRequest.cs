using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class ProformaSubmitOrderRequest : SubmitOrderRequest
    {
        public SecurityType SecurityType { get; set; }
        public string Symbol { get; set; }
        public OrderType OrderType { get; set; }
        public int Quantity { get; set; }
        public decimal LimitPrice { get; set; }
        public decimal StopPrice { get; set; }
        public OrderStatus OrderStatus { get; set; }

        public ProformaSubmitOrderRequest(OrderType orderType, SecurityType securityType, string symbol, int quantity, decimal stopPrice, decimal limitPrice, DateTime time, string tag) : base(orderType, securityType, symbol, quantity, stopPrice, limitPrice, time, tag)
        {
            SecurityType = securityType;
            Symbol = symbol.ToUpper();
            OrderType = orderType;
            Quantity = quantity;
            LimitPrice = limitPrice;
            StopPrice = stopPrice;
        }
        /// <summary>
        /// Sets the <see cref="OrderRequest.OrderId"/>
        /// </summary>
        /// <param name="orderId">The order id of the generated order</param>
        public void SetOrderId(int orderId)
        {
            OrderId = orderId;
        }
    }


}
