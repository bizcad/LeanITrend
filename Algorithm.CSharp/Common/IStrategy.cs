using System;

namespace QuantConnect.Algorithm.CSharp
{
    public interface IStrategy
    {      
        /// <summary>
        /// Checks the signal.
        /// </summary>
        /// <returns></returns>
        void CheckSignal();
    }

    public abstract class BaseStrategy : IStrategy
    {
        public StockState Position;

        public Nullable<decimal> EntryPrice;
        
        public OrderSignal ActualSignal;

        public abstract void CheckSignal();
    }
}