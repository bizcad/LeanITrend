using NUnit.Framework;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Indicators;
using QuantConnect.Tests.Indicators;
using System;

namespace QuantConnect.Tests
{
    [TestFixture]
    public class ItrendStrategyTest
    {
        [Test]
        public void GoLongAndClosePosition()
        {
            int _period = 7;
            decimal _tolerance = 0.00001m;
            decimal _revertPct = 1.015m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            decimal[] prices = new decimal[20]
            {
                100m, 100m, 100m, 100m, 100m, 
                100m, 99m, 98m, 97m, 96m, 95m, 94m, 93m, 92m, 91m,
                104m, 105m, 106m, 90m, 90m
            };

            OrderSignal[] expectedOrders = new OrderSignal[20]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.goLongLimit , OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.closeLong
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];
            Identity priceIdentity = new Identity("IdentityIndicator");
            ITrendStrategy strategy = new ITrendStrategy(priceIdentity, _period, _tolerance, _revertPct, RevertPositionCheck.vsClosePrice);

            for (int i = 0; i < prices.Length; i++)
            {
                priceIdentity.Update(new IndicatorDataPoint(time, prices[i]));
                //strategy.ITrend.Update(new IndicatorDataPoint(time, prices[i]));
                actualOrders[i] = strategy.ActualSignal;
                if (actualOrders[i] == OrderSignal.goLongLimit) strategy.Position = StockState.longPosition;
                Console.WriteLine(i + "| Actual Order:" + actualOrders[i]);
                time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }

        [Test]
        public void GoShortAndClosePosition()
        {
            int _period = 7;
            decimal _tolerance = 0.00001m;
            decimal _revertPct = 1.015m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            decimal[] prices = new decimal[20]
            {
                91m, 91m, 91m, 91m, 91m, 
                91m, 92m, 93m, 94m, 95m, 96m, 97m, 98m, 99m, 100m,
                85m, 84m, 83m, 100m, 100m,

            };

            OrderSignal[] expectedOrders = new OrderSignal[20]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.goShortLimit , OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.closeShort
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];
            Identity priceIdentity = new Identity("IdentityIndicator");
            ITrendStrategy strategy = new ITrendStrategy(priceIdentity, _period, _tolerance, _revertPct, RevertPositionCheck.vsClosePrice);

            for (int i = 0; i < prices.Length; i++)
            {
                priceIdentity.Update(new IndicatorDataPoint(time, prices[i]));
                actualOrders[i] = strategy.ActualSignal;
                if (actualOrders[i] == OrderSignal.goShortLimit) strategy.Position = StockState.shortPosition;
                Console.WriteLine(i + "| Actual Order:" + actualOrders[i]);
                time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }

        [Test]
        public void GoLongRevertAndClosePosition()
        {
            int _period = 7;
            decimal _tolerance = 0.00001m;
            decimal _revertPct = 1.015m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            decimal[] prices = new decimal[20]
            {
                100m, 100m, 100m, 100m, 100m,
                100m, 99m, 98m, 97m, 96m, 95m, 94m, 93m, 92m, 105m,
                100m, 95m, 90m, 100m, 100m
            };

            OrderSignal[] expectedOrders = new OrderSignal[20]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.goLongLimit,
                OrderSignal.revertToShort, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.closeShort
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];
            Identity priceIdentity = new Identity("IdentityIndicator"); 
            ITrendStrategy strategy = new ITrendStrategy(priceIdentity , _period, _tolerance, _revertPct, RevertPositionCheck.vsClosePrice);

            for (int i = 0; i < prices.Length; i++)
            {
                priceIdentity.Update(new IndicatorDataPoint(time, prices[i]));
                actualOrders[i] = strategy.ActualSignal;
                if (actualOrders[i] == OrderSignal.goLongLimit)
                {
                    strategy.Position = StockState.longPosition;
                    strategy.EntryPrice = prices[i];
                }
                if (actualOrders[i] == OrderSignal.revertToShort) strategy.Position = StockState.shortPosition;
                Console.WriteLine(i + "| Actual Order:" + actualOrders[i]);
                time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }

        [Test]
        public void GoShortRevertAndClosePosition()
        {
            int _period = 7;
            decimal _tolerance = 0.00001m;
            decimal _revertPct = 1.015m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            decimal[] prices = new decimal[20]
            {
                91m, 91m, 91m, 91m, 91m, 
                91m, 92m, 93m, 94m, 95m, 96m, 97m, 98m, 99m, 100m,
                80m, 102m, 104m, 90m, 100m
            };

            OrderSignal[] expectedOrders = new OrderSignal[20]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.goShortLimit, OrderSignal.revertToLong, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.closeLong
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];
            Identity priceIdentity = new Identity("IdentityIndicator"); 
            ITrendStrategy strategy = new ITrendStrategy(priceIdentity, _period, _tolerance, _revertPct, RevertPositionCheck.vsClosePrice);

            for (int i = 0; i < prices.Length; i++)
            {
                priceIdentity.Update(new IndicatorDataPoint(time, prices[i]));
                actualOrders[i] = strategy.ActualSignal;
                if (actualOrders[i] == OrderSignal.goShortLimit)
                {
                    strategy.Position = StockState.shortPosition;
                    strategy.EntryPrice = prices[i];
                }
                if (actualOrders[i] == OrderSignal.revertToLong) strategy.Position = StockState.longPosition;
                Console.WriteLine(i + "| Actual Order:" + actualOrders[i]);
                time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }

        [Test]
        public void TestingTolerance()
        {
            int _period = 7;
            decimal _tolerance = 0.15m;
            decimal _revertPct = 1.015m;
            DateTime time = DateTime.Now;

            # region Arrays inputs
            decimal[] prices = new decimal[20]
            {
                99m, 99m, 99m, 99m, 99m, 
                99m, 99.25m, 99.5m, 99.75m, 100m, 100.25m, 100.5m, 100.75m, 100m, 100m,
                100.1m, 100.2m, 100.3m, 100.4m, 100.5m,
            };

            OrderSignal[] expectedOrders = new OrderSignal[20]
            {
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing,
                OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.goShortLimit,
                OrderSignal.doNothing , OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing, OrderSignal.doNothing
            };
            # endregion

            OrderSignal[] actualOrders = new OrderSignal[expectedOrders.Length];
            Identity priceIdentity = new Identity("IdentityIndicator"); 
            ITrendStrategy strategy = new ITrendStrategy(priceIdentity, _period, _tolerance, _revertPct, RevertPositionCheck.vsClosePrice);

            for (int i = 0; i < prices.Length; i++)
            {
                priceIdentity.Update(new IndicatorDataPoint(time, prices[i]));
                actualOrders[i] = strategy.ActualSignal;
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
                100m, 100m, 100m, 100m, 100m, 100m, 100m, 100m, 100m, 100m
            };
            #endregion
            Identity priceIdentity = new Identity("IdentityIndicator"); 
            ITrendStrategy strategy = new ITrendStrategy(priceIdentity, 5);

            for (int i = 0; i < prices.Length; i++)
            {
                priceIdentity.Update(new IndicatorDataPoint(time, prices[i]));
                time.AddDays(1);
            }
            Assert.IsTrue(strategy.ITrend.IsReady, "Instantaneous Trend Ready");
            Assert.IsTrue(strategy.ITrendMomentum.IsReady, "Instantaneous Trend Momentum Ready");
            Assert.IsTrue(strategy.MomentumWindow.IsReady, "Instantaneous Trend Momentum Window Ready");

            strategy.Reset();

            TestHelper.AssertIndicatorIsInDefaultState(strategy.ITrend);
            TestHelper.AssertIndicatorIsInDefaultState(strategy.ITrendMomentum);
            Assert.IsFalse(strategy.MomentumWindow.IsReady, "Instantaneous Trend Momentum Windows was Reset");
        }

    }
}