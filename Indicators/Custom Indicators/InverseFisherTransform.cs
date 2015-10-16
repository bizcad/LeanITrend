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
    public class InverseFisherTransform : Indicator
    {
        SimpleMovingAverage mean;
        StandardDeviation sd;

        public InverseFisherTransform(string name, int period)
            : base(name)
        {
            mean = new SimpleMovingAverage(period);
            sd = new StandardDeviation(period);
        }

        public InverseFisherTransform(int period)
            : this("InvFish_" + period, period)
        {
        }

        public override bool IsReady
        {
            get { return mean.IsReady && sd.IsReady; }
        }

        public override void Reset()
        {
            base.Reset();
            mean.Reset();
            sd.Reset();
        }

        protected override decimal ComputeNextValue(IndicatorDataPoint input)
        {
            double ifish = 0d;

            mean.Update(input);
            sd.Update(input);

            if (mean.IsReady && sd.IsReady)
            {
                double normalized = (double)(4 * (input - mean) / sd);
                ifish = (Math.Exp(2 * normalized) - 1) / (Math.Exp(2 * normalized) + 1);
            }
            return (decimal)ifish;
        }
    }
}

