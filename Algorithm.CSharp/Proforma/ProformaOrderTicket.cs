using System;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class ProformaOrderTicket
    {
        public int OrderId { get; set; }
        public OrderStatus Status { get; set; }
        public string Symbol { get; set; }
        public SecurityType Security_Type { get; set; }
        public int Quantity { get; set; }
        public decimal AverageFillPrice { get; set; }
        public int QuantityFilled { get; set; }
        public DateTime TicketTime { get; set; }
        public OrderType TicketOrderType { get; set; }
        public string Tag { get; set; }
        public string ErrorMessage { get; set; }
        public decimal LimitPrice { get; set; }
        public decimal StopPrice { get; set; }
        public OrderDirection Direction { get; set; }
        public string Source { get; set; }
    }
}
