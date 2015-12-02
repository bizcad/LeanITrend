using System;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using NUnit.Framework;

namespace QuantConnect.Tests.Indicators
{
    [TestFixture]
    public class WilliamsVixFixTests
    {
        private WilliamsVixFix wvf;
        [SetUp]
        public void Setup()
        {
            wvf = new WilliamsVixFix();
            //TestHelper.TestIndicator(wvf, "spy_WilliamsWVF.csv", "Williams VixFix");
        }

        [Test]
        public void Test()
        {
            DateTime date = new DateTime(2015,5,19,9,31,00);

            foreach (var data in TestHelper.GetTradeBarStream("spy_WilliamsWVF.csv", false))
            {
                wvf.Update(data);
                //System.Diagnostics.Debug.WriteLine(wvf.Current.Value);
            }
            Assert.IsTrue(wvf.Current.Value == 1.9321648621164084464915953800m);
        }
    }
}
