namespace QuantConnect.Algorithm.CSharp
{
    public interface IPositionInventory
    {
        void Add(OrderTransaction transaction);
        OrderTransaction Remove(string direction);
        OrderTransaction RemoveBuy();
        OrderTransaction RemoveSell();
        int BuysCount();
        int SellsCount();   
        int GetBuysQuantity(Symbol symbol);
        int GetSellsQuantity(Symbol symbol);
        Symbol GetSymbol();

        Symbol Symbol { get; set; }
    }
}