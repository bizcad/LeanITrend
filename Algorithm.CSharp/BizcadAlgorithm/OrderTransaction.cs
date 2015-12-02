using System;
using System.Runtime.Serialization;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    [DataContract]
    public class OrderTransaction 
    {
        [DataMember]
        public int Id { get; set; }
        [DataMember]
        public string Symbol { get; set; }
        [DataMember]
        public string Exchange { get; set; }
        [DataMember]
        public string Broker { get; set; }
        [DataMember]
        public int Quantity { get; set; }
        [DataMember]
        public decimal Price { get; set; }
        [DataMember]
        public string ActionNameUS { get; set; }
        [DataMember]
        public DateTime TradeDate { get; set; }
        [DataMember]
        public DateTime SettledDate { get; set; }
        [DataMember]
        public decimal Interest { get; set; }
        [DataMember]
        public decimal Amount { get; set; }
        [DataMember]
        public decimal Commission { get; set; }
        [DataMember]
        public decimal Fees { get; set; }
        [DataMember]
        public decimal Net { get; set; }
        [DataMember]
        public string CUSIP { get; set; }
        [DataMember]
        public string Description { get; set; }
        [DataMember]
        public int ActionId { get; set; }
        [DataMember]
        public int TradeNumber { get; set; }
        [DataMember]
        public string RecordType { get; set; }
        [DataMember]
        public string TaxLotNumber { get; set; }
        [DataMember]
        public OrderType OrderType { get; set; }
        [DataMember]
        public int OrderId { get; set; }
        [DataMember]
        public OrderDirection Direction { get; set; }


    }
}
