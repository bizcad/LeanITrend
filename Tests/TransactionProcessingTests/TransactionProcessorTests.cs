using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Algorithm.CSharp.BizcadAlgorithm;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Orders;

namespace QuantConnect.Tests.TransactionProcessingTests
{
    [TestFixture]
    public class TransactionProcessorTests
    {
        private TransactionProcessor p;
        [SetUp]
        public void Setup()
        {
            p = new TransactionProcessor();
        }

        [Test]
        public void InstanciationSucceeds()
        {

            Assert.IsNotNull(p);
            Assert.IsNotNull(p.OpenPositions);
        }

        [Test]
        public void TransactionHistoryAddsOK()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Direction = OrderDirection.Buy;
            p.TransactionHistory.Add(trans);
            Assert.IsTrue(p.TransactionHistory.Count > 0);
        }

        [Test]
        public void OpenTradesAddsOk()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Direction = OrderDirection.Buy;
            p.OpenTrades.Add(trans);
            Assert.IsTrue(p.OpenTrades.Count > 0);
        }

        [Test]
        public void OpenPositionOpensABuyPositionAndSellPosition()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Direction = OrderDirection.Buy;
            trans.Symbol = "AAPL";
            p.ProcessTransaction(trans);
            Assert.IsTrue(p.OpenPositions.Count > 0);
            
        }

        [Test]
        public void NoOpenPositionsReturnNull()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Symbol = "AAPL";
            IPositionInventory openPosition = p.OpenPositions.FirstOrDefault(s => s.GetSymbol() == trans.Symbol);
            Assert.IsNull(openPosition);
        }

        [Test]
        public void CanOpenPosition()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Symbol = "AAPL";
            trans.Direction = OrderDirection.Buy;
            TransactionProcessor processor = new TransactionProcessor();
            IPositionInventory openPosition = processor.OpenPosition(trans, PositionInventoryMethod.Fifo);
            processor.OpenPositions.Add(openPosition);
            Assert.IsTrue(processor.OpenPositions.Count > 0);
        }

        [Test]
        public void MatchingTransactionsCreateTrade()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.TradeDate = new DateTime(2015, 9, 4, 9, 40, 00, DateTimeKind.Local);
            trans.Direction = OrderDirection.Buy;
            trans.Quantity = 100;
            trans.Price = 110.00m;
            trans.Symbol = "AAPL";
            trans.Commission = 1m;
            trans.Amount = trans.Price*trans.Quantity + 1m;
            trans.OrderType = OrderType.Limit;
            trans.Broker = "IB";
            trans.TradeNumber = 1;
            p.ProcessTransaction(trans);
            Assert.IsTrue(p.OpenPositions.Count > 0);

            trans = new OrderTransaction();
            trans.TradeDate = new DateTime(2015, 9, 4, 9, 45, 00, DateTimeKind.Local);
            trans.Direction = OrderDirection.Sell;
            trans.Symbol = "AAPL";
            trans.Quantity = -100;
            trans.Price = 120.00m;
            trans.Commission = 1m;
            trans.Amount = trans.Price * trans.Quantity + 1m;
            trans.OrderType = OrderType.Market;
            trans.Broker = "IB";
            trans.TradeNumber = 2;
            p.ProcessTransaction(trans);
            Assert.IsTrue(p.Trades.Count > 0);
            Assert.IsTrue(p.OpenPositions.Count == 0);
           
        }
    }
}
