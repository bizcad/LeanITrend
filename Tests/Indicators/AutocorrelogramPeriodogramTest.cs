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
        public void SuperSmootherAndHighPassFilterComputesCorrectly()
        {
            int _shortPeriod = 10;
            int _longPeriod = 30;
            int _correlationWidth = 3;
            DateTime time = DateTime.Now;
            decimal[] actualValues = new decimal[100];

            AutocorrelogramPeriodogram AP = new AutocorrelogramPeriodogram(_shortPeriod, _longPeriod, _correlationWidth);

            # region Arrays inputs
            decimal[] prices = new decimal[100]
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
                60m,    64.57m, 68.43m, 71.01m, 71.95m, 71.16m, 68.88m, 65.58m, 61.92m, 58.62m
            };

            decimal[] expectedValues = new decimal[100]
            {
                // Estimated with Python:
                  0m     ,  0m     ,  0m     , -0.0311m, -0.1631m, -0.4609m, -0.9609m, -1.6642m, -2.5367m, -3.5167m,
                 -4.5266m, -5.4816m, -6.2968m, -6.8956m, -7.2176m, -7.2236m, -6.8953m, -6.2362m, -5.2726m, -4.0535m,
                 -2.6455m, -1.1271m,  0.4138m,  1.8876m,  3.209m ,  4.3005m,  5.0981m,  5.555m ,  5.6452m,  5.3649m,
                  4.7346m,  3.797m ,  2.6124m,  1.257m , -0.1824m, -1.6155m, -2.9523m, -4.108m , -5.0079m, -5.5946m,
                 -5.8324m, -5.706m , -5.2224m, -4.4106m, -3.3223m, -2.0266m, -0.6043m,  0.8547m,  2.2589m,  3.5211m,
                  5.46m  ,  7.2647m,  6.9729m,  4.6815m,  1.3682m, -1.852m , -4.1171m, -4.9649m, -4.3351m, -2.4986m,
                  0.0556m,  2.7398m,  4.9805m,  6.3188m,  6.4837m,  5.427m ,  3.3259m,  0.5467m, -2.4239m, -5.064m ,
                 -6.9081m, -7.6295m, -7.0954m, -5.3904m, -2.8035m,  0.224m ,  3.1751m,  5.544m ,  6.9255m,  7.0856m,
                  5.9997m,  3.8582m,  1.0345m, -1.9813m, -4.6657m, -6.5524m, -7.314m , -6.8175m, -5.1472m, -2.592m ,
                  0.4071m,  3.3329m,  5.6795m,  7.0413m,  7.1844m,  6.0838m,  3.9295m,  1.0949m, -1.9303m, -4.6226m
            };
            # endregion

            for (int i = 0; i < prices.Length; i++)
            {
                AP.Update(new IndicatorDataPoint(time, prices[i]));
                actualValues[i] = Math.Round(AP.sSmoother.Current.Value, 4);
                Console.WriteLine(actualValues[i]);
                time.AddMinutes(1);
            }
            Assert.AreEqual(expectedValues, actualValues, "Estimation HighPassFilter(5)");
        }

        [Test]
        public void ResetsProperly()
        {
            int _shortPeriod = 10;
            int _longPeriod = 30;
            int _correlationWidth = 3;
            DateTime time = DateTime.Now;

            AutocorrelogramPeriodogram AP = new AutocorrelogramPeriodogram(_shortPeriod, _longPeriod, _correlationWidth);

            for (int i = 0; i < (_longPeriod + _correlationWidth + 1); i++)
            {
                AP.Update(new IndicatorDataPoint(time, 1m));
                time.AddMinutes(1);
            }
            Assert.IsTrue(AP.IsReady, "AutocorrelogramPeriodogram ready");
            AP.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(AP);
        }
    }
}