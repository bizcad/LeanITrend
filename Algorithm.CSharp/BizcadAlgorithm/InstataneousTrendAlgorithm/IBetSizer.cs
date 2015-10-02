using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Interface for Portfolio Allocations
    /// </summary>
    /// <remarks>
    /// 
    ///  ToDo: Implement a Kelly interface
    /// 
    /// </remarks>
    public interface IBetSizer
    {
        decimal BetSize(Symbol symbol);
        decimal BetSize(Symbol symbol, decimal currentPrice, decimal transactionSize);
        decimal BetSize(Symbol symbol, decimal currentPrice, decimal transactionSize, SignalInfo signalInfo, OrderTransactionProcessor proformaProcessor);
    }
}
