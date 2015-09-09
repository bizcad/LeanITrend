using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QuantConnect.Algorithm.CSharp.BizcadAlgorithm;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class TransactionProcessor
    {

        public IList<OrderTransaction> TransactionHistory { get; set; }
        public IList<OrderTransaction> OpenTrades { get; set; }

        public IList<IPositionInventory> OpenPositions { get; set; }
        public IList<MatchedTrade> Trades { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalFees { get; set; }
        private int tradeId = 0;


        public TransactionProcessor()
        {
            TransactionHistory = new List<OrderTransaction>();
            OpenTrades = new List<OrderTransaction>();

            OpenPositions = new List<IPositionInventory>();
            Trades = new List<MatchedTrade>();

        }

        public void ProcessTransaction(OrderTransaction trans)
        {
            IPositionInventory openPosition = OpenPositions.FirstOrDefault(p => p.GetSymbol() == trans.Symbol);
            if (openPosition == null)
            {
                openPosition = OpenPosition(trans, PositionInventoryMethod.Lifo);
                
                OpenPositions.Add(openPosition);
            }
            else
            {
                OrderTransaction transaction = ResolvePosition(openPosition, trans);
                if (openPosition.BuysCount() == 0 && openPosition.SellsCount() == 0)
                {
                    OpenPositions.Remove(openPosition);
                }
            }
        }

        private OrderTransaction ResolvePosition(IPositionInventory position, OrderTransaction trans)
        {
            OrderTransaction buytrans = new OrderTransaction();
            OrderTransaction selltrans = new OrderTransaction();
            OrderTransaction l = new OrderTransaction();

            if (trans.Direction == OrderDirection.Buy)
            {
                if (position.SellsCount() > 0)
                {
                    selltrans = position.RemoveSell();
                    if (Math.Abs(trans.Quantity) == Math.Abs(selltrans.Quantity))
                    {
                        return CreateTrade(trans, selltrans);
                    }
                }
            }
            else
            {
                if (position.BuysCount() > 0)
                {
                    buytrans = position.RemoveBuy();
                    if (Math.Abs(trans.Quantity) == Math.Abs(buytrans.Quantity))
                    {
                        return CreateTrade(buytrans, trans);
                    }
                }
            }

        
            throw new Exception("my bad");
        }

        private OrderTransaction CreateTrade(OrderTransaction buytrans, OrderTransaction selltrans)
        {
            var l = new OrderTransaction();
            if (buytrans == null && selltrans == null)
                return l;

            MatchedTrade trade = new MatchedTrade
            {
                Id = ++tradeId,
                Symbol = buytrans.Symbol,
                DescriptionOfProperty = string.Format("{0} {1}", buytrans.Quantity, buytrans.Symbol),
                DateAcquired = buytrans.SettledDate,
                DateSoldOrDisposed = selltrans.SettledDate,
                AdjustmentAmount = 0,
                ReportedToIrs = true,
                ReportedToMe = true,
                Brokerage = selltrans.Broker,
                BuyOrderId = buytrans.TradeNumber,
                SellOrderId = selltrans.TradeNumber
            };

            // Buy quantities are positive, sell quantities are negative
            // Buy Amount is negative, sell Amount is positive.
            // commission and fees are always negative
            if (Math.Abs(buytrans.Quantity) == Math.Abs(selltrans.Quantity))
            {
                trade.Quantity = buytrans.Quantity;
                trade.Proceeds = Math.Abs(selltrans.Amount);
                trade.CostOrBasis = Math.Abs(buytrans.Amount);

                //Long Term Short Term
                TimeSpan diff = trade.DateSoldOrDisposed.Subtract(trade.DateAcquired);
                if (diff.TotalDays > 365)
                    trade.LongTermGain = true;

                //LogTrade(trade);
                //if (trade.DateSoldOrDisposed.Year == 2014)
                Trades.Add(trade);

                return l;
            }

            throw new InvalidDataException("buy qty not equal to sell qty");

        }

        public IPositionInventory OpenPosition(OrderTransaction trans, PositionInventoryMethod positionResolution)
        {
            IPositionInventory position;

            if (positionResolution == PositionInventoryMethod.Fifo)
                position = new PositionInventoryFifo();
            else
            {
                position = new PositionInventoryLifo();
            }
            position.Add(trans);
            return position;
        }


    }
}
