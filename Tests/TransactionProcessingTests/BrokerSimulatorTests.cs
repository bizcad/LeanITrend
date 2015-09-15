using System;
using System.Collections.Generic;
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

        [Test]
        public void CreatesRequest()
        {
            Assert.IsTrue(algorithm.Securities.ContainsKey(symbol));
            var security = algorithm.Securities[symbol];
            // Date is 1998
            ProformaSubmitOrderRequest request = sim.CreateSubmitOrderRequest(OrderType.Market, security, 100, "Test of request");
            Assert.IsNotNull(request);
            Assert.IsTrue(request.Quantity == 100m);
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
                security.SetMarketPrice(tb);
                TradeBars bars = new TradeBars(tb.EndTime);
                bars.Add(security.Symbol,tb);
                sim.PricesWindow.Add(bars);
            }
            
            
            var ticket = sim.MarketOrder(security.Symbol, 100, false, "Proforma Market Order");
            Assert.IsNotNull(ticket);
            Assert.IsTrue(sim.GetTicketCount() > 0);
            Assert.IsTrue(ticket.OrderStatus == OrderStatus.Filled);
            Assert.IsTrue(ticket.QuantityFilled == 100);
        }

        [Test]
        public void UpdatesTicketField()
        {
            
        }
    }
}
