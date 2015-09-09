using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Algorithm.CSharp.BizcadAlgorithm;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class PositionInventoryFifo : IPositionInventory
    {
        public ConcurrentQueue<OrderTransaction> Buys { get; set; }
        public ConcurrentQueue<OrderTransaction> Sells { get; set; }
        public const string Buy = "Buy";
        public const string Sell = "Sell";
        public string Symbol { get; set; }

        public PositionInventoryFifo()
        {
            this.Buys = new ConcurrentQueue<OrderTransaction>();
            this.Sells = new ConcurrentQueue<OrderTransaction>();
        }

        public void Add(OrderTransaction transaction)
        {
            Symbol = transaction.Symbol;
            if (transaction.Direction == OrderDirection.Buy)
            {
                Buys.Enqueue(transaction);
            }
            if (transaction.Direction == OrderDirection.Sell)
            {
                Sells.Enqueue(transaction);
            }
        }

        public OrderTransaction Remove(string queueName)
        {
            OrderTransaction transaction = null;
            if (queueName.Contains(Buy))
                Buys.TryDequeue(out transaction);
            if (queueName.Contains(Sell))
                Sells.TryDequeue(out transaction);
            return transaction;
        }
        public OrderTransaction RemoveBuy()
        {
            OrderTransaction transaction = null;
            Buys.TryDequeue(out transaction);
            return transaction;
        }
        public OrderTransaction RemoveSell()
        {
            OrderTransaction transaction = null;
            Sells.TryDequeue(out transaction);
            return transaction;
        }

        public int BuysCount()
        {
            return Buy.Count();
        }

        public int SellsCount()
        {
            return Sells.Count;
        }

        public string GetSymbol()
        {
            return Symbol;
        }
    }
}
