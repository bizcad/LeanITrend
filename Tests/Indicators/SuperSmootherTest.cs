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

//using QuantConnect.Tests.Indicators;
using System;

namespace QuantConnect.Tests.Indicators
{
    [TestFixture]
    public class SuperSmootherTest
    {
        [Test]
        public void SuperSmootherComputesCorrectly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;
            decimal[] actualValues = new decimal[20];

            SuperSmoother sSmoother = new SuperSmoother(_period);

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
                15m, 18.09m, 19.5201m, 21.3652m, 23.261m, 24.5534m, 24.9032m, 24.2448m, 22.6636m, 20.3287m,
                17.4727m, 14.3764m, 11.3411m, 8.6643m, 6.6086m, 5.374m, 5.0812m, 5.7594m, 7.3412m, 9.6731m
            };
            # endregion

            for (int i = 0; i < prices.Length; i++)
            {
                sSmoother.Update(new IndicatorDataPoint(time, prices[i]));
                actualValues[i] = Math.Round(sSmoother.Current.Value, 4);
                Console.WriteLine(actualValues[i]);
                time.AddMinutes(1);
            }
            Assert.AreEqual(expectedValues, actualValues, "Estimation SuperSmoother(5)");
        }

        [Test]
        public void ResetsProperly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;

            SuperSmoother sSmoother = new SuperSmoother(_period);

            for (int i = 0; i < 6; i++)
            {
                sSmoother.Update(new IndicatorDataPoint(time, 1m));
                time.AddMinutes(1);
            }
            Assert.IsTrue(sSmoother.IsReady, "SuperSmoother ready");
            sSmoother.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(sSmoother);
        }
    }
}