using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Algorithm.CSharp.BizcadAlgorithm;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Data.Market;
using QuantConnect.Orders;

namespace QuantConnect.Tests.TransactionProcessingTests
{
    [TestFixture]
    public class TransactionProcessorTests
    {
        private OrderTransactionProcessor p;
        [SetUp]
        public void Setup()
        {
            p = new OrderTransactionProcessor();
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
        public void OpenPositionOpensABuyPositionPosition()
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
            OrderTransactionProcessor processor = new OrderTransactionProcessor();
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
            trans.Amount = trans.Price * trans.Quantity + 1m;
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
        [Test]
        public void UnMatchBuyTransactionsCreateTradeAndPushesRemainder()
        {
            // Add a buy to inventory
            OrderTransaction trans = new OrderTransaction();
            trans.TradeDate = new DateTime(2015, 9, 4, 9, 40, 00, DateTimeKind.Local);
            trans.Direction = OrderDirection.Buy;
            trans.Quantity = 100;
            trans.Price = 110.00m;
            trans.Symbol = "AAPL";
            trans.Commission = 1m;
            trans.Fees = .2m;
            trans.Amount = trans.Price * trans.Quantity + +trans.Commission + trans.Fees;
            trans.OrderType = OrderType.Limit;
            trans.Broker = "IB";
            trans.TradeNumber = 1;
            trans.Description = "Buy Trans which will be split";
            p.ProcessTransaction(trans);
            Assert.IsTrue(p.OpenPositions.Count > 0);

            // Remove part of the sell from inventory
            OrderTransaction selltrans = new OrderTransaction();
            selltrans.TradeDate = new DateTime(2015, 9, 4, 9, 45, 00, DateTimeKind.Local);
            selltrans.Direction = OrderDirection.Sell;
            selltrans.Symbol = "AAPL";
            selltrans.Quantity = -20;
            selltrans.Price = 120.00m;
            selltrans.Commission = 1m;
            selltrans.Fees = .2m;
            selltrans.Amount = selltrans.Price * selltrans.Quantity + selltrans.Commission + selltrans.Fees;
            selltrans.OrderType = OrderType.Market;
            selltrans.Broker = "IB";
            selltrans.TradeNumber = 2;
            selltrans.Description = "Sell Trans which will taken from buy";
            p.ProcessTransaction(selltrans);
            Assert.IsTrue(p.Trades.Count > 0);
            Assert.IsTrue(p.OpenPositions.Count > 0);
            MatchedTrade t = p.Trades.FirstOrDefault();
            Assert.IsNotNull(t);
            Assert.IsTrue(t.Quantity == Math.Abs(selltrans.Quantity));
            IPositionInventory inv = p.OpenPositions.FirstOrDefault();
            Assert.IsTrue(inv != null && inv.BuysCount() == 1);
            OrderTransaction x = inv.RemoveBuy();
            Assert.IsTrue(x.Quantity == 80);
        }

        [Test]
        public void MatchedAndUnmatchedTransactions()
        {
            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "transactionsTest.csv";
            int counter = 0;
            List<OrderTransaction> list = new List<OrderTransaction>();

            OrderTransactionProcessor processor = new OrderTransactionProcessor();

            using (StreamReader sr = new StreamReader(pathname))
            {
                string line = sr.ReadLine();    // read the header but do not count it.
                
                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    Assert.IsNotNull(line);
                    counter++;
                    OrderTransaction t = new OrderTransaction();
                    ObjectToCsv.FromCsv(",",line,ref t,false);
                    list.Add(t);
                    processor.ProcessTransaction(t);
                }
                sr.Close();
            }
            var csv = ObjectToCsv.ToCsv(",", processor.Trades, true);
            using (StreamWriter sw = new StreamWriter(path + "Trades.csv"))
            {
                foreach (var s in csv)
                {
                    sw.WriteLine(s);
                }
                sw.Flush();
                sw.Close();
            }

            Assert.IsTrue(counter == list.Count);
            Assert.IsTrue(processor.TotalProfit == 52.75m);
            Assert.IsTrue(processor.TotalCommission == -26m);
        }



    
    }
}
