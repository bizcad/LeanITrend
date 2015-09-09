using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class OrderTransaction 
    {
        public int Id { get; set; }
        public string Symbol { get; set; }
        public string Exchange { get; set; }
        public string Broker { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public string ActionNameUS { get; set; }
        public DateTime TradeDate { get; set; }
        public DateTime SettledDate { get; set; }
        public decimal Interest { get; set; }
        public decimal Amount { get; set; }
        public decimal Commission { get; set; }
        public decimal Fees { get; set; }
        public string CUSIP { get; set; }
        public string Description { get; set; }
        public int ActionId { get; set; }
        public int TradeNumber { get; set; }
        public string RecordType { get; set; }
        public string TaxLotNumber { get; set; }

        public OrderType OrderType { get; set; }
        public int OrderId { get; set; }
        public OrderDirection Direction { get; set; }


    }
}
