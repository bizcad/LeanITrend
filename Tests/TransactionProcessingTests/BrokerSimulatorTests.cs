using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Orders;

namespace QuantConnect.Tests.TransactionProcessingTests
{
    [TestFixture]
    public class BrokerSimulatorTests
    {
        public QCAlgorithm algorithm;
        public BrokerSimulator sim;
        private string symbol = "AAPL";
        [SetUp]
        public void Setup()
        {
            algorithm = new QuantConnect.Algorithm.CSharp.MultiITAlgorithm();
            sim = new BrokerSimulator(algorithm);
            algorithm.AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                       
        }

        //[Test]
        //public void CreatesRequest()
        //{
        //    Assert.IsTrue(algorithm.Securities.ContainsKey(symbol));
        //    var security = algorithm.Securities[symbol];
        //    // Date is 1998
        //    ProformaSubmitOrderRequest request = sim.CreateSubmitOrderRequest(OrderType.Market, security, 100, "Test of request");
        //    Assert.IsNotNull(request);
        //    Assert.IsTrue(request.Quantity == 100m);
        //}
        [Test]
        public void CheckOrderList()
        {
            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "BrokerSimulatorTestData.csv";
            List<TradeBar> list = new List<TradeBar>();
            using (StreamReader sr = new StreamReader(pathname.Replace(".json", ".csv")))
            {
                string line = sr.ReadLine();    // read the header but do not count it.

                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    Assert.IsNotNull(line);

                    TradeBar t = new TradeBar();
                    CsvSerializer.Deserialize(",", line, ref t, false);
                    list.Add(t);
                }
                sr.Close();
            }
        }
        [Test]
        public void AddsMarketOrder()
        {
            var security = algorithm.Securities[symbol];

            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "BrokerSimulatorTestData.json";
            List<TradeBar> list;
            using (StreamReader sr = new StreamReader(pathname))
            {
                list = JsonConvert.DeserializeObject<List<TradeBar>>(sr.ReadToEnd());
                sr.Close();
            }
            var tb = list.FirstOrDefault();


            if (tb != null)
            {

                var csv = CsvSerializer.Serialize<TradeBar>(",", list, true);
                using (StreamWriter swr =new  StreamWriter( pathname.Replace(".json",".csv")))
                {
                    foreach (var item in csv)
                    {
                        swr.WriteLine(item);
                    }
                    swr.Flush();
                    swr.Close();
                }


                List<TradeBar> list2 = new List<TradeBar>();
                using (StreamReader sr = new StreamReader(pathname.Replace(".json", ".csv")))
                {
                    string line = sr.ReadLine();    // read the header but do not count it.

                    while (!sr.EndOfStream)
                    {
                        line = sr.ReadLine();
                        Assert.IsNotNull(line);

                        TradeBar t = new TradeBar();
                        CsvSerializer.Deserialize(",", line, ref t, false);
                        list2.Add(t);
                    }
                    sr.Close();
                }


                security.SetMarketPrice(tb);
                TradeBars bars = new TradeBars(tb.EndTime);
                bars.Add(security.Symbol, tb);
                sim.PricesWindow.Add(bars);
            }

            var ticket = sim.MarketOrder(security.Symbol, 100, "Proforma Market Order");
            Assert.IsNotNull(ticket);
            Assert.IsTrue(sim.GetTicketCount() == 1);
            Assert.IsTrue(ticket.Status == OrderStatus.Filled);
            Assert.IsTrue(ticket.QuantityFilled == 100);
            var tickets = sim._orderTickets;

        }
        [Test]
        public void AddsLimitOrder()
        {
            var security = algorithm.Securities[symbol];

            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "BrokerSimulatorTestData.json";
            List<TradeBar> list;
            using (StreamReader sr = new StreamReader(pathname))
            {
                list = JsonConvert.DeserializeObject<List<TradeBar>>(sr.ReadToEnd());
                sr.Close();
            }
            var tb = list.FirstOrDefault();

            if (tb != null)
            {
                security.SetMarketPrice(tb);
                TradeBars bars = new TradeBars(tb.EndTime);
                bars.Add(security.Symbol, tb);
                sim.PricesWindow.Add(bars);
            }

            var ticket = sim.LimitOrder(security.Symbol, 100, security.Price * .95m, "Limit Order");
            Assert.IsTrue(sim.GetTicketCount() == 1);
            Assert.IsTrue(ticket.Status == OrderStatus.Submitted);
            Assert.IsTrue(ticket.QuantityFilled == 0);

        }
        
        [Test]
        public void AddsStopOrder()
        {
            var security = algorithm.Securities[symbol];

            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "BrokerSimulatorTestData.json";
            List<TradeBar> list;
            using (StreamReader sr = new StreamReader(pathname))
            {
                list = JsonConvert.DeserializeObject<List<TradeBar>>(sr.ReadToEnd());
                sr.Close();
            }
            var tb = list.FirstOrDefault();

            if (tb != null)
            {
                security.SetMarketPrice(tb);
                TradeBars bars = new TradeBars(tb.EndTime);
                bars.Add(security.Symbol, tb);
                sim.PricesWindow.Add(bars);
            }

            var ticket = sim.StopMarketOrder(security.Symbol, 100, security.Price * .95m, "Stop Market Order");
            Assert.IsTrue(sim.GetTicketCount() == 1);
            Assert.IsTrue(ticket.Status == OrderStatus.Submitted);
            Assert.IsTrue(ticket.QuantityFilled == 0);

            ticket = sim.StopLimitOrder(security.Symbol, 100, security.Price * .95m, security.Price * 1.05m, "Stop Limit Order");
            Assert.IsTrue(sim.GetTicketCount() == 2);
            Assert.IsTrue(ticket.Status == OrderStatus.Submitted);
            Assert.IsTrue(ticket.QuantityFilled == 0);
            

        }

        [Test]
        public void AddsMarketOnOpenOrder()
        {
            var security = algorithm.Securities[symbol];

            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "BrokerSimulatorTestData.json";
            List<TradeBar> list;
            using (StreamReader sr = new StreamReader(pathname))
            {
                list = JsonConvert.DeserializeObject<List<TradeBar>>(sr.ReadToEnd());
                sr.Close();
            }
            var tb = list.FirstOrDefault();

            if (tb != null)
            {
                security.SetMarketPrice(tb);
                TradeBars bars = new TradeBars(tb.EndTime);
                bars.Add(security.Symbol, tb);
                sim.PricesWindow.Add(bars);
            }

            var ticket = sim.MarketOnOpenOrder(security.Symbol, 100, "Stop Limit Order");
            Assert.IsTrue(sim.GetTicketCount() == 1);
            Assert.IsTrue(ticket.Status == OrderStatus.Submitted);
            Assert.IsTrue(ticket.QuantityFilled == 0);

        }
        [Test]
        public void AddsMarketOnCloseOrder()
        {
            var security = algorithm.Securities[symbol];

            string path = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\";
            string pathname = path + "BrokerSimulatorTestData.json";
            List<TradeBar> list;
            using (StreamReader sr = new StreamReader(pathname))
            {
                list = JsonConvert.DeserializeObject<List<TradeBar>>(sr.ReadToEnd());
                sr.Close();
            }
            var tb = list.FirstOrDefault();

            if (tb != null)
            {
                security.SetMarketPrice(tb);
                TradeBars bars = new TradeBars(tb.EndTime);
                bars.Add(security.Symbol, tb);
                sim.PricesWindow.Add(bars);
            }

            var ticket = sim.MarketOnCloseOrder(security.Symbol, 100, "Stop Market Order");
            Assert.IsTrue(sim.GetTicketCount() == 1);
            Assert.IsTrue(ticket.Status == OrderStatus.Submitted);
            Assert.IsTrue(ticket.QuantityFilled == 0);

        }

        [Test]
        public void UpdatesTicketField()
        {
            
        }
    }
}
