using NUnit.Framework;
using QuantConnect.Algorithm.CSharp.JJAlgorithms.DecycleInverseFisher;
using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Indicators;
using QuantConnect.Tests.Indicators;
using System;

namespace QuantConnect.Tests.DecycleInverseFisher
{
    [TestFixture]
    public class DecycleInverseFisherTest
    {
        [Test]
        public void GoLongAndClosePosition()
        {
            int _trendPeriod = 5;
            int _invFisherPeriod = 6;
            decimal _tolerance = 0.001m;
            decimal _threshold = 0.9m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            decimal[] prices = new decimal[15]
            {
                24.51m, 24.51m, 20.88m, 15m,  9.12m,
                 5.49m,  5.49m,  9.12m, 15m, 20.88m,
                24.51m, 24.51m, 20.88m, 15m,  9.12m,
            };

            OrderSignal[] expectedOrders = new OrderSignal[15]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.goLong   , OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.closeLong, OrderSignal.doNothing
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];

            DIFStrategy strategy = new DIFStrategy(_trendPeriod, _invFisherPeriod, _threshold, _tolerance);

            for (int i = 0; i < prices.Length; i++)
            {
                strategy.DecycleTrend.Update(new IndicatorDataPoint(time, prices[i]));
                strategy.InverseFisher.Update(strategy.DecycleTrend.Current);
                actualOrders[i] = strategy.CheckSignal();
                if (actualOrders[i] == OrderSignal.goLong) strategy.Position = StockState.longPosition;
                Console.WriteLine(i + "| Actual Order:" + actualOrders[i]);
                time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }
        
        [Test]
        public void GoShortAndClosePosition()
        {
            int _trendPeriod = 5;
            int _invFisherPeriod = 6;
            decimal _tolerance = 0.001m;
            decimal _threshold = 0.9m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            decimal[] prices = new decimal[15]
            {
                 5.49m,  5.49m,  9.12m, 15m, 20.88m,
                24.51m, 24.51m, 20.88m, 15m,  9.12m,
                 5.49m,  5.49m,  9.12m, 15m, 20.88m, 
            };

            OrderSignal[] expectedOrders = new OrderSignal[15]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing , OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.goShort   , OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.closeShort, OrderSignal.doNothing
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];

            DIFStrategy strategy = new DIFStrategy(_trendPeriod, _invFisherPeriod, _threshold, _tolerance);

            for (int i = 0; i < prices.Length; i++)
            {
                strategy.DecycleTrend.Update(new IndicatorDataPoint(time, prices[i]));
                strategy.InverseFisher.Update(strategy.DecycleTrend.Current);
                actualOrders[i] = strategy.CheckSignal();
                if (actualOrders[i] == OrderSignal.goShort) strategy.Position = StockState.shortPosition;
                Console.WriteLine(i + "| Actual Order:" + actualOrders[i]);
                time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }

        [Test]
        public void RealDataTest()
        {
            int _trendPeriod = 5;
            int _invFisherPeriod = 6;
            decimal _tolerance = 0.001m;
            decimal _threshold = 0.9m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            // Real AMZN minute data
            decimal[] prices = new decimal[30]
            {
                427.30m, 427.24m, 427.57m, 427.71m, 427.59m, 427.95m, 427.27m, 427.19m, 427.45m, 427.81m,
                427.62m, 427.66m, 427.67m, 427.62m, 427.67m, 427.20m, 426.89m, 427.27m, 427.46m, 427.31m,
                427.11m, 427.29m, 427.54m, 427.87m, 427.80m, 427.35m, 427.34m, 427.75m, 427.94m, 429.00m, 
            };

            OrderSignal[] expectedOrders = new OrderSignal[30]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.goLong   ,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.closeLong,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.goLong   , OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.closeLong, OrderSignal.goLong   , OrderSignal.doNothing, OrderSignal.doNothing
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];

            DIFStrategy strategy = new DIFStrategy(_trendPeriod, _invFisherPeriod, _threshold, _tolerance);

            for (int i = 0; i < prices.Length; i++)
            {
                strategy.DecycleTrend.Update(new IndicatorDataPoint(time, prices[i]));
                strategy.InverseFisher.Update(strategy.DecycleTrend.Current);
                actualOrders[i] = strategy.CheckSignal();

                if (actualOrders[i] == OrderSignal.goShort && strategy.Position == StockState.noInvested) strategy.Position = StockState.shortPosition;
                if (actualOrders[i] == OrderSignal.closeShort && strategy.Position == StockState.shortPosition) strategy.Position = StockState.noInvested;

                if (actualOrders[i] == OrderSignal.goLong && strategy.Position == StockState.noInvested) strategy.Position = StockState.longPosition;
                if (actualOrders[i] == OrderSignal.closeLong && strategy.Position == StockState.longPosition) strategy.Position = StockState.noInvested;

                Console.WriteLine(i + "| Actual Order:" + actualOrders[i]);
                Console.WriteLine(strategy.DecycleTrend.Current.ToString());
                Console.WriteLine(strategy.InverseFisher.Current.ToString() + "\n");
                time = time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }

        [Test]
        public void ToleranceTesting()
        {
            // This is the same GoLongAndClosePosition Test, only the tolerance is increased to avoid signals.
            int _trendPeriod = 5;
            int _invFisherPeriod = 6;
            decimal _tolerance = 2m;
            decimal _threshold = 0.9m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            decimal[] prices = new decimal[15]
            {
                24.51m, 24.51m, 20.88m, 15m,  9.12m,
                 5.49m,  5.49m,  9.12m, 15m, 20.88m,
                24.51m, 24.51m, 20.88m, 15m,  9.12m,
            };

            OrderSignal[] expectedOrders = new OrderSignal[15]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];

            DIFStrategy strategy = new DIFStrategy(_trendPeriod, _invFisherPeriod, _threshold, _tolerance);

            for (int i = 0; i < prices.Length; i++)
            {
                strategy.DecycleTrend.Update(new IndicatorDataPoint(time, prices[i]));
                strategy.InverseFisher.Update(strategy.DecycleTrend.Current);
                actualOrders[i] = strategy.CheckSignal();
                if (actualOrders[i] == OrderSignal.goLong) strategy.Position = StockState.longPosition;
                Console.WriteLine(i + "| Actual Order:" + actualOrders[i]);
                time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }

        [Test]
        public void ResetsProperly()
        {
            DateTime time = DateTime.Parse("2000-01-01");

            # region Arrays inputs
            decimal[] prices = new decimal[10]
            {
                100m, 110m, 120m, 130m, 140m, 150m, 160m, 170m, 180m, 190m
            };
            #endregion

            DIFStrategy strategy = new DIFStrategy(5, 5);

            for (int i = 0; i < prices.Length; i++)
            {
                strategy.DecycleTrend.Update(new IndicatorDataPoint(time, prices[i]));
                strategy.InverseFisher.Update(strategy.DecycleTrend.Current);
                strategy.CheckSignal();
                time.AddDays(1);
            }
            Assert.IsTrue(strategy.DecycleTrend.IsReady, "Decycle Trend Ready");
            Assert.IsTrue(strategy.InverseFisher.IsReady, "Decycle Inverse Fisher Ready");
            Assert.IsTrue(strategy.InvFisherRW.IsReady, "Inverse Fisher Window Ready");

            strategy.Reset();

            TestHelper.AssertIndicatorIsInDefaultState(strategy.DecycleTrend);
            TestHelper.AssertIndicatorIsInDefaultState(strategy.InverseFisher);
            Assert.IsFalse(strategy.InvFisherRW.IsReady, "Inverse Fisher Window was Reset");
        }

    }
}