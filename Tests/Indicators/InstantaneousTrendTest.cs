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
    public class InstantaneousTrendTest
    {
        [Test]
        public void InstantaneousTrendComputesCorrectly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;
            decimal[] actualValues = new decimal[20];

            InstantaneousTrend iTrend = new InstantaneousTrend(_period);

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
                // Estimated with Python: http://tinyurl.com/nbt4ud3
                15m, 18.09m, 18.015m, 20.735m, 22.8925m, 24.2775m, 24.755m, 24.3836m, 23.0445m, 20.8039m,
                17.8648m, 14.5236m, 11.1232m, 8.0166m, 5.5265m, 3.911m, 3.3413m, 3.8832m, 5.4906m, 8.0133m
            };
            # endregion

            for (int i = 0; i < prices.Length; i++)
            {
                iTrend.Update(new IndicatorDataPoint(time, prices[i]));
                actualValues[i] = Math.Round(iTrend.Current.Value, 4);
                time.AddMinutes(1);
            }
            Assert.AreEqual(expectedValues, actualValues, "Estimation ITrend(5)");
        }

        [Test]
        public void ResetsProperly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;

            InstantaneousTrend iTrend = new InstantaneousTrend(_period);

            for (int i = 0; i < 6; i++)
            {
                iTrend.Update(new IndicatorDataPoint(time, 1m));
                time.AddMinutes(1);
            }
            Assert.IsTrue(iTrend.IsReady, "Instantaneous Trend ready");
            iTrend.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(iTrend);
        }
    }
}