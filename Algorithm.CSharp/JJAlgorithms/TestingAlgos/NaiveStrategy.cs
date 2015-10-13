using QuantConnect.Algorithm.CSharp;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect
{
    public class NoStrategy : BaseStrategy
    {
        private string _symbol;
        private Indicator _trend;
        private Indicator _price;
        public OrderTicket limitEntry;
        public OrderTicket limitExit;

        public Indicator Price
        {
            get { return _price; }
        }

        public Indicator Trend
        {
            get { return _trend; }
        }

        public NoStrategy(string Symbol, Indicator Price, Indicator Trend)
        {
            _symbol = Symbol;
            _price = Price;
            _trend = Trend;
        }

        public override OrderSignal CheckSignal()
        {
            return OrderSignal.doNothing;
        }

        public void Reset()
        {
            _trend.Reset();
        }
    }
}