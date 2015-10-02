using QuantConnect.Data.Market;

namespace QuantConnect.Algorithm.CSharp
{
    public interface ILimitPriceCalculator
    {
        decimal Calculate(TradeBar tradeBar, SignalInfo signalInfo, decimal rangeFactor);
        decimal Calculate(TradeBar tradeBar, OrderSignal signal, decimal rangeFactor);
    }
}
