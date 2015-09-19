using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using QuantConnect.Algorithm.CSharp;
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
        public void MatchedAndUnmatchedTransactions()
        {
            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "transactions.csv";
            // This part of the test is just to look at the JsonConvert
            //string txt;
            //using (StreamReader sr = new StreamReader(pathname))
            //{
            //    txt = sr.ReadToEnd();
            //    sr.Close();
            //}
            //int index = txt.IndexOf("\r\n", System.StringComparison.Ordinal);
            //string titlesremoved = txt.Substring(index + 2);
            
            int counter = 0;
            List<OrderTransaction> list = new List<OrderTransaction>();

            OrderTransactionProcessor processor = new OrderTransactionProcessor();

            using (StreamReader sr = new StreamReader(pathname))
            {
                string line = sr.ReadLine();    // read the header but do not count it.
                
                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    if (line != null && line.Contains("Symbol")) continue;
                    Assert.IsNotNull(line);
                    counter++;
                    OrderTransaction t = new OrderTransaction();
                    CsvSerializer.Deserialize(",",line,ref t,false);
                    list.Add(t);
                    processor.ProcessTransaction(t);
                }
                sr.Close();
            }
            var csv = CsvSerializer.Serialize(",", processor.Trades, true);
            using (StreamWriter sw = new StreamWriter(path + "Trades.csv"))
            {
                foreach (var s in csv)
                {
                    sw.WriteLine(s);
                }
                sw.Flush();
                sw.Close();
            }
            var x = JsonConvert.SerializeObject(list);
            Assert.IsTrue(counter == list.Count);
            Assert.IsTrue(processor.TotalProfit == 52.75m);
            Assert.IsTrue(processor.TotalCommission == -26m);
        }



    
    }
}
