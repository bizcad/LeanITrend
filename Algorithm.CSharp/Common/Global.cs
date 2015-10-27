namespace QuantConnect.Algorithm.CSharp
{
    public enum StockState
    {
        shortPosition,  // The Portfolio has short position in this bar.
        longPosition,   // The Portfolio has long position in this bar.
        noInvested,     // The Portfolio hasn't any position in this bar.
        orderSent       // An order has been sent in this same bar, skip analysis.
    };

    public enum OrderSignal
    {
        goShort, goLong,                // Entry to the market orders.
        goShortLimit, goLongLimit,      // Entry with limit order.
        closeShort, closeLong,          // Exit from the market orders.
        revertToShort, revertToLong,    // Reverse a position when in the wrong side of the trade.
        doNothing
    };

    public enum RevertPositionCheck
    {
        vsTrigger,
        vsClosePrice,
    }

    public enum PositionInventoryMethod
    {
        Lifo, Fifo
    }
}
