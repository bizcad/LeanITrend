using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp.BizcadAlgorithm
{
    public class MatchedTrade
    {
        public int Id { get; set; }
        public bool IsOpen { get; set; }
        public string Symbol { get; set; }
        public int Quantity { get; set; }
        public string DescriptionOfProperty { get; set; }
        public DateTime DateAcquired { get; set; }
        public DateTime DateSoldOrDisposed { get; set; }
        public decimal Proceeds { get; set; }
        public decimal CostOrBasis { get; set; }
        public string AdjustmentCode { get; set; }
        public decimal AdjustmentAmount { get; set; }

        public decimal GainOrLoss
        {
            get { return Proceeds - CostOrBasis + AdjustmentAmount; }
        }
        // For ScheduleD
        public bool ReportedToIrs { get; set; }  // Reported to IRS on 1099-B
        public bool ReportedToMe { get; set; }  // Reported to me on 1099-B
        public bool LongTermGain { get; set; }
        public int BuyOrderId { get; set; }
        public int SellOrderId { get; set; }
        public string Brokerage { get; set; }
        public decimal CumulativeProfit { get; set; }
    }
}
