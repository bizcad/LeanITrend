using System;
using NUnit.Framework;
using QuantConnect.Indicators;
using QuantConnect.Tests.Indicators;
using QuantConnect.Algorithm.CSharp.ITrendAlgorithm;

namespace QuantConnect.Tests.ITrendAlgorithm
{
    [TestFixture]
    public class ItrendStrategyTest
    {
        [Test]
        public void ResetsProperly()
        {
            DateTime time = DateTime.Parse("2000-01-01");

            # region Arrays inputs
            decimal[] prices = new decimal[14]
            {
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m
            };
            #endregion

            ITrendStrategy strategy = new ITrendStrategy(7);

            for (int i = 0; i < prices.Length; i++)
            {
                strategy.ITrend.Update(new IndicatorDataPoint(time, prices[i]));
                time.AddDays(1);
            }
            Assert.IsTrue(strategy.ITrend.IsReady, "Instantaneous Trend Ready");
            //Assert.IsTrue(strategy.ITrendMomentum.IsReady, "Instantaneous Trend Momentum Ready");
            //Assert.IsTrue(strategy.MomentumWindow.IsReady, "Instantaneous Trend Momentum Window Ready");

            strategy.Reset();

            TestHelper.AssertIndicatorIsInDefaultState(strategy.ITrend);
            //TestHelper.AssertIndicatorIsInDefaultState(strategy.ITrendMomentum);
            //Assert.IsFalse(strategy.MomentumWindow.IsReady, "Instantaneous Trend Momentum Windows was Reset");
        }

        [Test]
        public void GoLong()
        {
            DateTime time = DateTime.Parse("2000-01-01");

            # region Arrays inputs
            decimal[] prices = new decimal[13]
            {
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                100m,
                95m,
                105m,
                105m,
                95m,
                95m,
                95m
            };

            OrderSignal[] expectedOrders = new OrderSignal[13]
            {
                OrderSignal.doNothing,
                OrderSignal.doNothing,
                OrderSignal.doNothing,
                OrderSignal.doNothing,
                OrderSignal.doNothing,
                OrderSignal.doNothing,
                OrderSignal.doNothing,
                OrderSignal.doNothing,
                OrderSignal.doNothing,
                OrderSignal.goLong,
                OrderSignal.doNothing,
                OrderSignal.closeLong,
                OrderSignal.doNothing
            };
            # endregion 

            OrderSignal[] actualOrders = new OrderSignal[10];

            ITrendStrategy strategy = new ITrendStrategy(7, tolerance:0m, revetPct:1.015m,
                checkRevertPosition: RevertPositionCheck.vsClosePrice);

            for (int i = 0; i < prices.Length; i++)
            {
                strategy.ITrend.Update(new IndicatorDataPoint(time, prices[i]));
                actualOrders[i] = strategy.CheckSignal(prices[i]);
                time.AddDays(1);
            }
            Assert.AreEqual(expectedOrders, actualOrders);
        }
    }
}