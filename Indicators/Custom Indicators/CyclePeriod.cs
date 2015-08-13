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
using System;
using System.Linq;
using System.Collections.Generic;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Cybernetics chapter 9
    /// </summary>
    public class CyclePeriod : WindowIndicator<IndicatorDataPoint>
    {
        // the alpha for the formula
        private readonly decimal _alpha = 0.07m;

        /// <summary>
        /// 
        /// </summary>
        public RollingWindow<decimal> _smooth;
        /// <summary>
        /// 
        /// </summary>
        public RollingWindow<decimal> _cycle;
        /// <summary>
        /// 
        /// </summary>
        public RollingWindow<decimal> _quadrature;
        /// <summary>
        /// 
        /// </summary>
        public RollingWindow<decimal> _deltaPhase;

        /// <summary>
        /// 
        /// </summary>
        public ExponentialMovingAverage _instPeriod;
        /// <summary>
        /// 
        /// </summary>
        public ExponentialMovingAverage _period;

        private readonly decimal pi = (decimal)Math.PI;

        private decimal _A;
        private decimal _B;
        private decimal _quadCoeffA;
        private decimal _quadCoeffB;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="period"></param>
        public CyclePeriod(string name, int period=3)
            : base(name, period)
        {
            // Initialize the differents window needed
            _smooth = new RollingWindow<decimal>(3);
            _cycle = new RollingWindow<decimal>(6);
            _quadrature = new RollingWindow<decimal>(2);
            _deltaPhase = new RollingWindow<decimal>(5);

            // Initialize the EMA needed
            _instPeriod = new ExponentialMovingAverage(2, new decimal(0.33));
            _period = new ExponentialMovingAverage(10, new decimal(0.15));  // weight = 80%

            _A = (decimal)Math.Pow(1 - (double)_alpha / 2, 2);
            _B = 1 - _alpha;

            _quadCoeffA = new decimal(0.0962);
            _quadCoeffB = new decimal(0.5769);
        }

        /// <summary>
        ///     Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady
        {
            get { return _period.IsReady; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="window"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            decimal hfp;
            decimal instPeriod;
            decimal quadrature;
            decimal currentInPhase;
            decimal previousInPhase;
            decimal deltaPhase;
            decimal medianDeltaPhase;
            decimal DC;
 
            // for convenience
            var time = input.Time;

            if (window.IsReady)
            {
                _smooth.Add((window[0].Value + 2 * window[1].Value + 2 * window[2].Value + window.MostRecentlyRemoved) / 6);
                
                if (!_cycle.IsReady)
                {
                    // If cycle isn't ready then fill it with a FIR.
                    _cycle.Add((window[0].Value - 2 * window[1].Value + window[2].Value) / 4);
                }
                else
                {
                    // Once is ready, calculate the cycle.
                    hfp = _A * (_smooth[0] - 2 * _smooth[1] + _smooth[2]) + 2 * _B * _cycle[0] - (_B * _B) * _cycle[1];
                    _cycle.Add(hfp);
                    
                    // Quadrature calculation.
                    instPeriod = 0.5m + 0.08m * _instPeriod.Current.Value;
                    quadrature = (_quadCoeffA * _cycle[0] + _quadCoeffB * _cycle[2] -
                                  _quadCoeffB * _cycle[4] - _quadCoeffA * _cycle.MostRecentlyRemoved) * instPeriod;
                    _quadrature.Add(quadrature);

                    currentInPhase = _cycle[3];
                    previousInPhase = _cycle[4];

                    // DeltaPhase calculation.
                    if (_quadrature.IsReady && (_quadrature[0] != 0 && _quadrature[1] != 0))
                    {
                        deltaPhase = (currentInPhase / _quadrature[0] - previousInPhase / _quadrature[1]) /
                                     (1 - currentInPhase * previousInPhase / (_quadrature[0] * _quadrature[1]));
                    }
                    else
                    {
                        deltaPhase = 1;
                    }

                    if (deltaPhase < 0.1m) _deltaPhase.Add(0.1m);       // Minimun frequency (longest cycle) 63 bars
                    else if (deltaPhase > 1.1m) _deltaPhase.Add(1.1m);
                    else _deltaPhase.Add(deltaPhase);

                    if (_deltaPhase.IsReady) medianDeltaPhase = Median(_deltaPhase);
                    else medianDeltaPhase = 0;

                    if (medianDeltaPhase == 0) DC = 15m;
                    else DC = 2 * pi / medianDeltaPhase + 0.5m;
                    
                    _instPeriod.Update(idp(time, DC));
                    _period.Update(idp(time, _instPeriod.Current));
                }
            }

            return _period.Current;
        }

        private decimal Median(RollingWindow<decimal> deltaPhase)
        {
            int k;
            decimal median;

            int obs = deltaPhase.Count;
            bool even = obs % 2 == 0;

            decimal[] array = deltaPhase.OrderBy(x => x).ToArray();
                       
            if (even)
            {
                k = obs / 2;
                median = (array[k] + array[k + 1]) / 2;
            }
            else
            {
                k = (obs + 1) / 2;
                median = array[k];
            }
            return median;
        }

        /// <summary>
        /// Factory function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from 
        /// new IndicatorDataPoint(data.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }
    }
}
