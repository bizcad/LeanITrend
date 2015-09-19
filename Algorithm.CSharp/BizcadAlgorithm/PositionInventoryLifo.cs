using System.Collections.Concurrent;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class PositionInventoryLifo : IPositionInventory
    {
        public ConcurrentStack<OrderTransaction> Buys { get; set; }
        public ConcurrentStack<OrderTransaction> Sells { get; set; }
        public const string Buy = "Buy";
        public const string Sell = "Sell";
        public string Symbol { set; get; }

        public PositionInventoryLifo()
        {
            Buys = new ConcurrentStack<OrderTransaction>();
            Sells = new ConcurrentStack<OrderTransaction>();
        }
        public void Add(OrderTransaction transaction)
        {
            Symbol = transaction.Symbol;
            if (transaction.Direction == OrderDirection.Buy)
            {
                Buys.Push(transaction);
            }
            if (transaction.Direction == OrderDirection.Sell)
            {
                Sells.Push(transaction);
            }
        }

        public OrderTransaction Remove(string direction)
        {
            OrderTransaction transaction = null;
            if (direction.Contains(Buy))
                if (Buys.Count > 0)
                    Buys.TryPop(out transaction);

            if (direction.Contains(Sell))
                if (Sells.Count > 0)
                    Sells.TryPop(out transaction);
            return transaction;
        }

        public OrderTransaction RemoveBuy()
        {
            OrderTransaction transaction = null;
            if (Buys.Count > 0)
                Buys.TryPop(out transaction);
            return transaction;
        }

        public OrderTransaction RemoveSell()
        {
            OrderTransaction transaction = null;
            if (Sells.Count > 0)
                Sells.TryPop(out transaction);
            return transaction;
        }
        public int BuysCount()
        {
            return Buys.Count;
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
