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
 *
*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// </summary>
    public class InverseFisherTransform : WindowIndicator<IndicatorDataPoint>
    {

        private RollingWindow<IndicatorDataPoint> series;
        
        /// <summary>
        /// A Fisher Transform of Prices
        /// </summary>
        /// <param name="name">string - the name of the indicator</param>
        /// <param name="period">The number of periods for the indicator</param>
        public InverseFisherTransform(string name, int period)
            : base(name, period)
        {
            series = new RollingWindow<IndicatorDataPoint>(period);
        }

        /// <summary>
        ///     Initializes a new instance of the FisherTransform class with the default name and period
        /// </summary>
        /// <param name="period">The period of the WMA</param>
        public InverseFisherTransform(int period)
            : this("Fish_" + period, period)
        {
        }
        /// <summary>
        /// Computes the next value in the transform. 
        /// value1 is a function used to normalize price withing the last _period day range.
        /// value1 is centered on its midpoint and then doubled so that value1 wil swing between -1 and +1.  
        /// value1 is also smoothed with an exponential moving average whose alpha is 0.33.  
        /// 
        /// Since the smoothing may allow value1 to exceed the _period day price range, limits are introduced to 
        /// preclude the transform from blowing up by having an input larger than unity.
        /// </summary>
        /// <param name="window">The IReadOnlyWindow of Indicator Data Points for the history of this indicator</param>
        /// <param name="input">IndicatorDataPoint - the time and value of the next price</param>
        /// <returns></returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            double ifish = 0d;

            if (this.IsReady)
            {
                double mean = (double)window.Average(t => t.Value);
                double sqrAvg = (double)window.Average(t => t.Value * t.Value);
                double sd = Math.Pow((sqrAvg - Math.Pow(mean, 2)), 0.5d);

                double normalized = 4 * ((double)input.Value - mean) / sd;

                ifish = (Math.Exp(2 * normalized) - 1) / (Math.Exp(2 * normalized) + 1);
            }

            Current = new IndicatorDataPoint(input.Time, (decimal)ifish);
            return this.Current;
        }
    }
}
