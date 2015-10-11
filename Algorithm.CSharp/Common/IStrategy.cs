using System;

namespace QuantConnect.Algorithm.CSharp
{
    public interface IStrategy
    {
        /// <summary>
        /// Checks the signal.
        /// </summary>
        /// <returns></returns>
        OrderSignal CheckSignal();
    }

    public abstract class BaseStrategy : IStrategy
    {
        public StockState Position;

        public Nullable<decimal> EntryPrice;

        public abstract OrderSignal CheckSignal();
    }
}