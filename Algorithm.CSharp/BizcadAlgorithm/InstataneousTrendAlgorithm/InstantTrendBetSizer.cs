using System;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Allocates a given cash size
    /// </summary>
    /// <remarks>
    /// 
    ///  ToDo: Kelly Goes here
    /// 
    /// </remarks>
    public class InstantTrendBetSizer : IBetSizer
    {
        private QCAlgorithm _algorithm;


        public InstantTrendBetSizer(QCAlgorithm algorithm)
        {

            this._algorithm = algorithm;
        }

        public decimal BetSize(Symbol symbol)
        {
            throw new NotImplementedException("Cannot make bet size from just a symbol. Use BetSize(Symbol symbol, decimal currentPrice, decimal transactionSize)");
        }

        public decimal BetSize(Symbol symbol, decimal currentPrice, decimal transactionSize)
        {
            return (int)(transactionSize / currentPrice);
        }

        public decimal BetSize(Symbol symbol, decimal currentPrice, decimal transactionSize, SignalInfo signalInfo)
        {
            decimal betsize = _algorithm.Portfolio[symbol].Invested ? Math.Abs(_algorithm.Portfolio[symbol].Quantity) : Math.Abs(transactionSize / currentPrice);
            if (betsize <= 10)
                betsize = 100;
            return betsize;
        }

        /// <summary>
        /// Calculates the bet size for this turn
        /// </summary>
        /// <param name="symbol">Symbol - the symbol to size the bet for</param>
        /// <param name="currentPrice">The current price of the security</param>
        /// <param name="transactionSize">The transaction size from the algorithm</param>
        /// <param name="signalInfo"></param>
        /// <param name="proformaProcessor"></param>
        /// <returns></returns>
        public decimal BetSize(Symbol symbol, decimal currentPrice, decimal transactionSize, SignalInfo signalInfo, OrderTransactionProcessor proformaProcessor)
        {
            decimal betsize = _algorithm.Portfolio[symbol].Invested ? Math.Abs(_algorithm.Portfolio[symbol].Quantity) : Math.Abs(transactionSize / currentPrice);
            if (betsize <= 10)
                betsize = 100;
            return betsize;
        }
    }
}
