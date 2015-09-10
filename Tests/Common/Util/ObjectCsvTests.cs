using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Algorithm.CSharp.Common;

namespace QuantConnect.Tests.Common.Util
{
    [TestFixture]
    public class ObjectCsvTests
    {
        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void ConvertsACsvLineToObjectAndBack()
        {
            OrderTransaction t = new OrderTransaction();
            string csv =
                @"0,AAPL,,IB,135,109.93,Buy,9/2/2015 1:41:00 PM,9/6/2015 1:41:00 PM,0,-14840.55,-1,0,-14841.55,CUSIP,Buy 135 shares of AAPL at $109.93,1,0,Trade,,Limit,1,Buy";
            ObjectToCsv.FromCsv<OrderTransaction>(",", csv, ref t, false);
            Assert.IsTrue(t.Symbol == "AAPL");

            // the ObjectToCsv call needs an IEnumerable, so convert to a list
            List<OrderTransaction> list = new List<OrderTransaction>();
            list.Add(t);
            
            var csvout = ObjectToCsv.ToCsv(",", list, false);
            var newcsv = csvout.FirstOrDefault();
            Assert.IsTrue(System.String.Compare(csv, newcsv, System.StringComparison.Ordinal) == 0);

        }
    }
}
