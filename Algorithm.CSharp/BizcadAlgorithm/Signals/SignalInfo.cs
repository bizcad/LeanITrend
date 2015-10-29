using System;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class SignalInfo
    {
        public int Id { get; set; }
        public Symbol Symbol { get; set; }
        public Type SignalType { get; set; }
        public OrderSignal Value { get; set; }
        public Boolean IsActive { get; set; }
        public OrderStatus Status { get; set; }
        public int TradeAttempts { get; set; }
        public string SignalJson { get; set; }
        public string InternalState { get; set; }
        public string Comment { get; set; }
        public string Name { get; set; }
        public decimal nTrig { get; set; }
        public RollingWindow<IndicatorDataPoint> Price;
        public InstantaneousTrend trend;
    }
}
