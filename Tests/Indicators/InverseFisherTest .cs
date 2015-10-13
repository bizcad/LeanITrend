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
    public class InverseFisherTest
    {
        # region Arrays inputs
        decimal[] prices = new decimal[20]
            {
                /*
                 * Formula:
                 * prices[i] = 10 * sin(2 * pi / 10 * i) + 15
                 * i = [0, 1, 2,..., 19]
                 */
                15m, 20.88m, 24.51m, 24.51m, 20.88m, 15m, 9.12m, 5.49m, 5.49m, 9.12m,
                15m, 20.88m, 24.51m, 24.51m, 20.88m, 15m, 9.12m, 5.49m, 5.49m, 9.12m
            };

        decimal[] expectedValues = new decimal[20]
            {
                // Estimated with Python: 
                 0m       ,  0m       ,  0m       ,  0m       ,  0m       , -0.999943m,
                -0.999999m, -0.999988m, -0.999635m, -0.850784m,  0.999943m,  0.999999m,
                 0.999988m,  0.999635m,  0.850784m, -0.999943m, -0.999999m, -0.999988m,
                -0.999635m, -0.850784m
            };
        # endregion

        [Test]
        public void InverseFisherComputesCorrectly()
        {

            int _period = 6;
            DateTime time = DateTime.Now;
            decimal[] actualValues = new decimal[20];

            InverseFisherTransform InvFisher = new InverseFisherTransform(_period);

            for (int i = 0; i < prices.Length; i++)
            {
                InvFisher.Update(new IndicatorDataPoint(time, prices[i]));
                actualValues[i] = Math.Round(InvFisher.Current.Value, 6);
                Console.WriteLine(actualValues[i]);
                time.AddMinutes(1);
            }
            Assert.AreEqual(expectedValues, actualValues, "Estimation Inverse Fisher(6)");
        }

        [Test]
        public void ResetsProperly()
        {
            int _period = 5;
            DateTime time = DateTime.Now;

            InverseFisherTransform InvFisher = new InverseFisherTransform(_period);

            for (int i = 0; i < 6; i++)
            {
                InvFisher.Update(new IndicatorDataPoint(time, prices[i]));
                time.AddMinutes(1);
            }
            Assert.IsTrue(InvFisher.IsReady, "Instantaneous Trend ready");
            InvFisher.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(InvFisher);
        }
    }
}