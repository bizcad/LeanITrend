using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Algorithm.CSharp.BizcadAlgorithm;
using QuantConnect.Orders;

namespace QuantConnect.Tests.TransactionProcessingTests
{
    [TestFixture]
    public class InventoryPositionTests
    {
        public const string Buy = "Buy";
        public const string Sell = "Sell";
        private PositionInventoryFifo fifo;
        private PositionInventoryLifo lifo;

        [SetUp]
        public void Setup()
        {
            lifo = new PositionInventoryLifo();
            fifo = new PositionInventoryFifo();
        }

        [Test]
        public void FifoReturnsNullWhenBuysAreEmpty()
        {
            var trans = lifo.Remove(Buy);
            Assert.IsNull(trans);
        }
        [Test]
        public void FifoReturnsNullWhenSellsAreEmpty()
        {
            var trans = lifo.Remove(Sell);
            Assert.IsNull(trans);
        }
        [Test]
        public void FifoAddsBuy()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Direction = OrderDirection.Buy;
            fifo.Add(trans);
            Assert.IsTrue(fifo.Buys.Count == 1);
            fifo.Remove(Buy);
            Assert.IsTrue(fifo.Buys.Count == 0);
        }
        [Test]
        public void FifoAddsSell()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Direction = OrderDirection.Sell;
            fifo.Add(trans);
            Assert.IsTrue(fifo.Sells.Count == 1);
            fifo.Remove(Sell);
            Assert.IsTrue(fifo.Sells.Count == 0);
        }

        [Test]
        public void LifoReturnsNullWhenBuysAreEmpty()
        {
            var trans = lifo.Remove(Buy);
            Assert.IsNull(trans);
        }
        [Test]
        public void LifoReturnsNullWhenSellsAreEmpty()
        {
            var trans = lifo.Remove(Sell);
            Assert.IsNull(trans);
        }
        [Test]
        public void LifoAddsSell()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Direction = OrderDirection.Sell;
            lifo.Add(trans);
            Assert.IsTrue(lifo.Sells.Count == 1);
            lifo.Remove(Sell);
            Assert.IsTrue(lifo.Sells.Count == 0);
        }
        [Test]
        public void LifoAddsBuy()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Direction = OrderDirection.Buy;
            lifo.Add(trans);
            Assert.IsTrue(lifo.BuysCount() == 1);
            lifo.Remove(Buy);
            Assert.IsTrue(lifo.BuysCount() == 0);
        }
        [Test]
        public void InterfaceOk()
        {
            OrderTransaction trans = new OrderTransaction();
            trans.Direction = OrderDirection.Buy;
            IPositionInventory lifo = new PositionInventoryLifo();
            lifo.Add(trans);
            var count = lifo.BuysCount();
            Assert.IsTrue(lifo.BuysCount() == 1);
            trans = lifo.RemoveBuy();
            Assert.IsNotNull(trans);
            Assert.IsTrue(lifo.BuysCount() == 0);

        }

    }
}
