using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using QuantConnect.Indicators;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp
{
    public class OrderTransactionProcessor
    {

        public IList<OrderTransaction> TransactionHistory { get; set; }
        public IList<OrderTransaction> OpenTrades { get; set; }

        public IList<IPositionInventory> OpenPositions { get; set; }
        public IList<MatchedTrade> Trades { get; set; }

        public decimal LastTradeCommission { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal TotalFees { get; set; }
        public decimal TotalProfit { get; set; }
        //public SimpleMovingAverage smaWins { get; set; }
        private int tradeId = 0;


        public OrderTransactionProcessor()
        {
            TransactionHistory = new List<OrderTransaction>();
            OpenTrades = new List<OrderTransaction>();

            OpenPositions = new List<IPositionInventory>();
            Trades = new List<MatchedTrade>();
            //smaWins = new SimpleMovingAverage(50);

        }

        public void ProcessTransaction(OrderTransaction trans)
        {
            string comment;
            if (trans.OrderId == 8)
                comment = "";
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
                    // if the buytrans qty is greater than the selltrans qty, split the buy
                    if (trans.Quantity > Math.Abs(selltrans.Quantity))
                    {
                        #region "Trans is Buy and buy greater than sell"
                        var unitcost = Math.Abs(trans.Amount / trans.Quantity);

                        // split the (buy)trans to equalize with the selltrans quantity
                        l = CopyTransaction(trans);
                        l.Quantity = trans.Quantity + selltrans.Quantity; // sell quantity will be negative
                        l.Amount = unitcost * l.Quantity * -1;
                        l.Commission = 0;
                        l.Fees = 0;
                        l.Interest = 0;
                        l.Net = l.Amount;

                        buytrans = CopyTransaction(trans);
                        buytrans.Quantity = Math.Abs(selltrans.Quantity);
                        buytrans.Amount = unitcost * buytrans.Quantity * -1;
                        buytrans.Net = buytrans.Amount + buytrans.Commission + buytrans.Fees;

                        CreateTrade(buytrans, selltrans);
                        return ResolvePosition(position, l);

                        #endregion
                    }
                    else
                    {
                        #region "Trans is Buy and sell greater than buy"
                        var unitcost = Math.Abs(selltrans.Amount / selltrans.Quantity);
                        // Split the sell
                        l = CopyTransaction(selltrans);
                        l.Quantity = selltrans.Quantity + trans.Quantity; // sell quantity will be negative
                        l.Amount = unitcost * l.Quantity * -1;
                        l.Commission = 0;
                        l.Fees = 0;
                        l.Interest = 0;
                        l.Net = l.Amount;

                        // split the sell.  The Sell gets no ICF
                        selltrans.Quantity = selltrans.Quantity - l.Quantity;       // sell qty is negative
                        selltrans.Amount = unitcost * selltrans.Quantity * -1;
                        selltrans.Net = selltrans.Amount + selltrans.Commission + selltrans.Fees;

                        CreateTrade(trans, selltrans);
                        return ResolvePosition(position, l);

                        #endregion
                    }
                }
                else
                {
                    position.Add(trans);
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

                    Decimal unitcost = 0;
                    if (Math.Abs(trans.Quantity) > buytrans.Quantity)
                    {
                        #region "Trans is sell and sell is greater than buy"
                        unitcost = Math.Abs(trans.Amount / trans.Quantity);

                        // split the sell, buytrans keeps the IFC
                        l = CopyTransaction(trans);
                        l.Quantity = trans.Quantity + buytrans.Quantity;
                        l.Amount = unitcost * l.Quantity * -1;
                        l.Commission = 0;
                        l.Fees = 0;
                        l.Interest = 0;
                        l.Net = l.Amount;

                        selltrans = CopyTransaction(trans);
                        selltrans.Quantity = selltrans.Quantity - l.Quantity;
                        selltrans.Amount = unitcost * selltrans.Quantity * -1;
                        selltrans.Net = selltrans.Amount + selltrans.Commission + selltrans.Fees;

                        CreateTrade(buytrans, selltrans);
                        return ResolvePosition(position, l);


                        #endregion
                    }
                    else
                    {
                        #region "Trans is sell and buy is greater than sell"

                        unitcost = Math.Abs(buytrans.Amount / buytrans.Quantity);

                        // split the (buy)trans to equalize with the selltrans quantity
                        l = CopyTransaction(buytrans);
                        l.Quantity = buytrans.Quantity + trans.Quantity; // sell quantity will be negative
                        l.Amount = unitcost * l.Quantity * -1;
                        l.Commission = 0;
                        l.Fees = 0;
                        l.Interest = 0;
                        l.Net = l.Amount;

                        buytrans.Quantity = buytrans.Quantity - l.Quantity;
                        buytrans.Amount = unitcost * buytrans.Quantity * -1;
                        buytrans.Net = buytrans.Amount + buytrans.Commission + buytrans.Fees;

                        CreateTrade(buytrans, trans);
                        return ResolvePosition(position, l);

                        #endregion
                    }
                }
                else
                {
                    position.Add(trans);
                }
            }
            return l;

            throw new Exception("my bad");
        }

        private OrderTransaction CreateTrade(OrderTransaction buytrans, OrderTransaction selltrans)
        {
            var l = new OrderTransaction();
            if (buytrans == null && selltrans == null)
                return l;

            if (buytrans.Amount >= 0)
                throw new ArgumentException("Buy trans amount >= 0");
            if (selltrans.Amount <= 0)
                throw new ArgumentException("Sell trans amount <= 0");
            if (buytrans.Quantity <= 0)
                throw new ArgumentException("Buy quantity <= 0");
            if (selltrans.Quantity >= 0)
                throw new ArgumentException("Sell quantity >= 0");
            MatchedTrade trade = new MatchedTrade
            {
                Id = ++tradeId,
                Symbol = buytrans.Symbol,
                DescriptionOfProperty = string.Format("{0} {1}", buytrans.Quantity, buytrans.Symbol),
                DateAcquired = buytrans.TradeDate,
                DateSoldOrDisposed = selltrans.TradeDate,
                AdjustmentAmount = 0,
                ReportedToIrs = true,
                ReportedToMe = true,
                Brokerage = selltrans.Broker,
                BuyOrderId = buytrans.OrderId,
                SellOrderId = selltrans.OrderId
            };


            // Buy quantities are positive, sell quantities are negative
            // Buy Amount is negative, sell Amount is positive.
            // commission and fees are always negative
            if (Math.Abs(buytrans.Quantity) == Math.Abs(selltrans.Quantity))
            {
                trade.Quantity = buytrans.Quantity;
                trade.Proceeds = Math.Abs(selltrans.Net);
                trade.CostOrBasis = Math.Abs(buytrans.Net);

                //Long Term Short Term
                TimeSpan diff = trade.DateSoldOrDisposed.Subtract(trade.DateAcquired);
                if (diff.TotalDays > 365)
                    trade.LongTermGain = true;

                //if (trade.DateSoldOrDisposed.Year == 2014)
                TotalCommission += buytrans.Commission;
                TotalCommission += selltrans.Commission;
                LastTradeCommission = buytrans.Commission + selltrans.Commission;
                TotalProfit += trade.GainOrLoss;
                //if (Math.Abs(trade.GainOrLoss) > 1000)
                //    throw new Exception("Invalid gain or loss");
                trade.CumulativeProfit = TotalProfit;
                Trades.Add(trade);

                //if (trade.GainOrLoss > 0)
                //{
                //    smaWins.Update(new IndicatorDataPoint(trade.DateSoldOrDisposed, trade.GainOrLoss));
                //}

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
        private OrderTransaction CopyTransaction(OrderTransaction trans)
        {
            OrderTransaction l = new OrderTransaction();
            l.Symbol = trans.Symbol;
            l.Exchange = trans.Exchange;
            l.Broker = trans.Broker;
            l.Quantity = trans.Quantity;
            l.Price = trans.Price;
            l.ActionNameUS = trans.ActionNameUS;
            l.TradeDate = trans.TradeDate;
            l.SettledDate = trans.SettledDate;
            l.Interest = trans.Interest;
            l.Amount = trans.Amount;
            l.Commission = trans.Commission;
            l.Fees = trans.Fees;
            l.CUSIP = trans.CUSIP;
            l.Description = trans.Description;
            l.ActionId = trans.ActionId;
            l.TradeNumber = trans.TradeNumber;
            l.RecordType = trans.RecordType;
            l.TaxLotNumber = trans.TaxLotNumber;

            l.OrderType = trans.OrderType;
            l.OrderId = trans.OrderId;
            l.Direction = trans.Direction;


            return l;
        }
        public bool IsLong(Symbol symbol)
        {
            if (OpenPositions.Count > 0)
            {
                var position = OpenPositions.FirstOrDefault(b => b.Symbol == symbol);
                if (position != null && position.BuysCount() > 0)
                {
                    return true;
                }
            }
            return false;
        }
        public bool IsShort(Symbol symbol)
        {
            if (OpenPositions.Count > 0)
            {
                var position = OpenPositions.FirstOrDefault(b => b.Symbol == symbol);
                if (position != null && position.SellsCount() > 0)
                {
                    return true;
                }
            }
            return false;
        }

        internal int GetPosition(Symbol symbol)
        {
            var openPosition = OpenPositions.FirstOrDefault();
            if (openPosition != null && openPosition.GetBuysQuantity(symbol) > 0)
                return openPosition.GetBuysQuantity(symbol);

            if (openPosition != null && openPosition.GetSellsQuantity(symbol) < 0)
                return openPosition.GetSellsQuantity(symbol);

            return 0;
        }

        public decimal CalculateLastTradePandL(Symbol symbol)
        {
            try
            {
                return Trades.LastOrDefault(p => p.Symbol == symbol).GainOrLoss;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
            return 0;
        }

    }
}
