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
using QuantConnect.Tests.Indicators;
using System;

namespace QuantConnect.Tests.Indicators
{
    [TestFixture]
    public class HighPassFilterTest
    {
        [Test]
        public void HighPassFilterComputesCorrectly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;
            decimal[] actualValues = new decimal[20];

            HighPassFilter hpf = new HighPassFilter(_period);

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
                0m, 0m, 0m, -0.1946m, -0.3266m, -0.4106m, -0.4506m, -0.4444m, -0.3945m, -0.3084m,                -0.1884m, -0.052m, 0.0889m, 0.224m, 0.3338m, 0.4121m, 0.4509m, 0.4445m, 0.3945m, 0.3084m
            };
            # endregion

            for (int i = 0; i < prices.Length; i++)
            {
                hpf.Update(new IndicatorDataPoint(time, prices[i]));
                actualValues[i] = Math.Round(hpf.Current.Value, 4);
                Console.WriteLine(actualValues[i]);
                time.AddMinutes(1);
            }
            Assert.AreEqual(expectedValues, actualValues, "Estimation HighPassFilter(5)");

        }

        [Test]
        public void ResetsProperly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;

            HighPassFilter hpf = new HighPassFilter(_period);

            for (int i = 0; i < 6; i++)
            {
                hpf.Update(new IndicatorDataPoint(time, 1m));
                time.AddMinutes(1);
            }
            Assert.IsTrue(hpf.IsReady, "SuperSmoother ready");
            hpf.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(hpf);
        }
    }
}