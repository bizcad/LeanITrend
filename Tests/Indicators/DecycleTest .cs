/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using NUnit.Framework;
using QuantConnect.Indicators;
using System;

namespace QuantConnect.Tests.Indicators
{
    [TestFixture]
    public class DecycleTest
    {
        [Test]
        public void InstantaneousTrendComputesCorrectly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;
            decimal[] actualValues = new decimal[20];

            Decycle dTrend = new Decycle(_period);

            # region Arrays inputs
            decimal[] prices = new decimal[20]
            {
                /*
                 * Formula:
                 * prices[i] = 10 * sin(2 * pi / 20 * i) + 15
                 * i = [0, 1, 2,..., 19]
                 */
                15m, 18.09m, 20.88m, 23.09m, 24.51m, 25m, 24.51m, 23.09m, 20.88m, 18.09m,
                15m, 11.91m, 9.12m, 6.91m, 5.49m, 5m, 5.49m, 6.91m, 9.12m, 11.91m
            };

            decimal[] expectedValues = new decimal[20]
            {
                // Estimated with Python:
                15m, 18.09m, 19.2641m, 21.554m, 23.4443m, 24.5474m, 24.7221m, 23.946m, 22.2956m, 19.9302m,
                17.0812m, 14.0293m, 11.0716m, 8.4991m, 6.5641m, 5.4539m, 5.2781m, 6.054m, 7.7044m, 10.0698m
            };
            # endregion

            for (int i = 0; i < prices.Length; i++)
            {
                dTrend.Update(new IndicatorDataPoint(time, prices[i]));
                actualValues[i] = Math.Round(dTrend.Current.Value, 4);
                Console.WriteLine(actualValues[i]);
                time.AddMinutes(1);
            }
            Assert.AreEqual(expectedValues, actualValues, "Estimation Decycle(5)");
        }

        [Test]
        public void ResetsProperly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;

            Decycle dTrend = new Decycle(_period);

            for (int i = 0; i < 6; i++)
            {
                dTrend.Update(new IndicatorDataPoint(time, 1m));
                time.AddMinutes(1);
            }
            Assert.IsTrue(dTrend.IsReady, "Decycle Trend ready");
            dTrend.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(dTrend);
        }
    }
}