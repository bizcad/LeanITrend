using System;

namespace QuantConnect.Algorithm.CSharp
{
    public interface IStrategy
    {      
        /// <summary>
        /// Checks the for signals.
        /// </summary>
        /// <returns></returns>
        void CheckSignal();
    }

    public abstract class BaseStrategy : IStrategy
    {
        /// <summary>
        /// Indicates what is the actual investing status for the strategy.
        /// </summary>
        public StockState Position;

        /// <summary>
        /// In case the strategy has an position taken, this is the entry price. Null otherwise.
        /// </summary>
        public Nullable<decimal> EntryPrice;

        /// <summary>
        /// The actual signal.
        /// </summary>
        public OrderSignal ActualSignal;

        /// <summary>
        /// Checks the for signals.
        /// </summary>
        public abstract void CheckSignal();
    }
}