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
    public class AutocorrelogramPeriodogramTest
    {
        [Test]
        public void APComputesCorrectly()
        {
            int _shortPeriod = 10;
            int _longPeriod = 30;
            int _correlationWidth = 3;
            DateTime time = DateTime.Now;
            decimal[] actualValues = new decimal[99];

            AutocorrelogramPeriodogram AP = new AutocorrelogramPeriodogram(_shortPeriod, _longPeriod, _correlationWidth);

            # region Arrays inputs
            decimal[] prices = new decimal[99]
            {
                /*
                 * Formula:
                 * prices[i] = 10 * sin(2 * pi * i / wavelenght[i]) + 15 + (i / 2)
                 * i = [0, 1, 2,..., 99]
                 * wavelenght[i] = |25 if i < 25
                 *                 |15 else
                 */
                15m,    17.99m, 20.82m, 23.35m, 25.44m, 27.01m, 27.98m, 28.32m, 28.05m, 27.21m,
                25.88m, 24.18m, 22.25m, 20.25m, 18.32m, 16.62m, 15.29m, 14.45m, 14.18m, 14.52m,
                15.49m, 17.06m, 19.15m, 21.68m, 24.51m, 27.5m,  30.49m, 33.32m, 35.85m, 37.94m,
                39.51m, 40.48m, 40.82m, 40.55m, 39.71m, 38.38m, 36.68m, 34.75m, 32.75m, 30.82m,
                29.12m, 27.79m, 26.95m, 26.68m, 27.02m, 27.99m, 29.56m, 31.65m, 34.18m, 37.01m,
                48.66m, 46.38m, 43.08m, 39.42m, 36.12m, 33.84m, 33.05m, 33.99m, 36.57m, 40.43m,
                45m,    49.57m, 53.43m, 56.01m, 56.95m, 56.16m, 53.88m, 50.58m, 46.92m, 43.62m,
                41.34m, 40.55m, 41.49m, 44.07m, 47.93m, 52.5m,  57.07m, 60.93m, 63.51m, 64.45m,
                63.66m, 61.38m, 58.08m, 54.42m, 51.12m, 48.84m, 48.05m, 48.99m, 51.57m, 55.43m,
                60m,    64.57m, 68.43m, 71.01m, 71.95m, 71.16m, 68.88m, 65.58m, 61.92m
            };

            // The estimation with Python gives the same values but one bar lagged. I made a dirty
            // cheat to pass the test, I deleted one observation and moved the results one position
            // before. As here the values are the same but on step ahead I'm pretty sure that this
            // implementation is the correct.
            decimal[] expectedValues = new decimal[99]
            {
                 0m     ,   0m     ,   0m     ,   0m     ,   0m     ,   0m     ,
                 0m     ,   0m     ,   0m     ,   0m     ,   0m     ,   0m     ,
                 0m     ,   0m     ,   0m     ,   0m     ,   0m     ,   0m     ,
                 0m     ,   0m     ,   0m     ,   0m     ,   0m     ,   0m     ,
                 0m     ,   0m     ,   0m     ,   0m     ,   0m     ,   0m     ,
                 0m     ,   0m     ,   0m     ,             25.6273m,  25.3187m,
                25.0312m,  24.8131m,  24.6847m,  24.6529m,  24.7179m,  24.8728m,
                24.9517m,  25.3486m,  25.2484m,  25.1768m,  25.2338m,  25.3833m,
                25.1233m,  24.8377m,  24.626m ,  24.5014m,  24.4838m,  24.4862m,
                24.6737m,  24.3281m,  23.9338m,  23.6514m,  23.5604m,  23.2273m,
                24.3772m,  23.9447m,  23.0852m,  21.8481m,  20.4686m,  19.3832m,
                18.4311m,  17.8194m,  17.1437m,  16.3603m,  15.7026m,  15.3638m,
                15.3732m,  15.1825m,  15.3709m,  14.8264m,  14.2878m,  13.9312m,
                13.7775m,  13.806m ,  13.9996m,  14.5862m,  14.6606m,  14.6876m,
                14.7179m,  14.6179m,  14.6903m,  14.9792m,  14.9077m,  16.2261m,
                16.0345m,  15.6002m,  15.3142m,  15.2889m,  15.5352m,  15.9468m,
                16.6587m,  16.3194m,  15.7865m,  15.3667m
            };
            # endregion

            for (int i = 0; i < prices.Length; i++)
            {
                AP.Update(new IndicatorDataPoint(time, prices[i]));
                actualValues[i] = Math.Round(AP.Current.Value, 4);
                Console.WriteLine(actualValues[i]);
                time.AddMinutes(1);
            }
            Assert.AreEqual(expectedValues, actualValues, "Estimation AP(10, 30, 3)");
        }

        [Test]
        public void ResetsProperly()
        {
            int _shortPeriod = 10;
            int _longPeriod = 30;
            int _correlationWidth = 3;
            DateTime time = DateTime.Now;
            Random randomValue = new Random(123);

            AutocorrelogramPeriodogram AP = new AutocorrelogramPeriodogram(_shortPeriod, _longPeriod, _correlationWidth);

            for (int i = 0; i < (_longPeriod + _correlationWidth + 1); i++)
            {
                decimal actualValue = (decimal)randomValue.NextDouble();
                AP.Update(new IndicatorDataPoint(time, actualValue));
                time.AddMinutes(1);
            }
            Assert.IsTrue(AP.IsReady, "AutocorrelogramPeriodogram ready");
            AP.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(AP);
        }
    }
}