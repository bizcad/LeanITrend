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
    /// Rocket Sceince chapter 7 Homodyne Discriminator dominant cycle period measurement
    /// </summary>
    public class CyclePeriodHomodyme : WindowIndicator<IndicatorDataPoint>
    {
        // the alpha for the formula
        private readonly decimal _alpha = 0.07m;

        /// <summary>
        /// Price Smoother
        /// </summary>
        public RollingWindow<decimal> _smooth;
        /// <summary>
        /// Price Detrend
        /// </summary>
        public RollingWindow<decimal> _cycle;
        /// <summary>
        /// Detrend
        /// </summary>
        public RollingWindow<decimal> _Quadrature;
        /// <summary>
        /// InPhase
        /// </summary>
        public RollingWindow<decimal> _InPhase;
        /// <summary>
        /// _Quadrature advanced 90 degrees
        /// </summary>
        public RollingWindow<decimal> _Q2;
        /// <summary>
        /// _InPhase advanced 90 degrees
        /// </summary>
        public RollingWindow<decimal> _I2;
        /// <summary>
        /// Not used
        /// </summary>
        public RollingWindow<decimal> _deltaPhase;
        /// <summary>
        /// Real portion of Homodyne
        /// </summary>
        public RollingWindow<decimal> _re;
        /// <summary>
        /// Imaginary portion of Homodyne
        /// </summary>
        public RollingWindow<decimal> _im;
        /// <summary>
        /// The calculated Period
        /// </summary>
        public RollingWindow<double> _Period;

        public ExponentialMovingAverage _SmoothPeriod;
        public ExponentialMovingAverage _period;

        private readonly decimal pi = (decimal)Math.PI;

        private decimal _A;
        private decimal _B;
        private decimal _quadCoeffA;
        private decimal _quadCoeffB;

        public CyclePeriodHomodyme(string name, int period = 3)
            : base(name, period)
        {
            // Initialize the differents window needed
            _smooth = new RollingWindow<decimal>(6);
            _cycle = new RollingWindow<decimal>(6);
            _Quadrature = new RollingWindow<decimal>(6);
            _InPhase = new RollingWindow<decimal>(6);
            _Q2 = new RollingWindow<decimal>(6);
            _I2 = new RollingWindow<decimal>(6);
            //_deltaPhase = new RollingWindow<decimal>(5);
            _re = new RollingWindow<decimal>(2);
            _im = new RollingWindow<decimal>(2);
            _Period = new RollingWindow<double>(period);


            // Initialize the EMA needed
            _SmoothPeriod = new ExponentialMovingAverage(2, new decimal(0.33));
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
            get { return _Period.IsReady; }
        }

        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            decimal hfp;
            //decimal instPeriod;
            //decimal quadrature;
            //decimal currentInPhase;
            //decimal previousInPhase;
            //decimal deltaPhase;
            //decimal medianDeltaPhase;
            //decimal DC;

            // for convenience
            var time = input.Time;
            // and assume an 8 bar Period
            _Period.Add(8.0);

            if (!window.IsReady)
            {
                _smooth.Add(window[0].Value);
            }
            else
            {
                _smooth.Add((window[0].Value + 2 * window[1].Value + 2 * window[2].Value + window.MostRecentlyRemoved) / 6);

                if (!_cycle.IsReady)
                {
                    // If cycle isn't ready then fill it with a FIR.
                    _cycle.Add((window[0].Value - 2 * window[1].Value + window[2].Value) / 4);
                    _Quadrature.Add(_cycle[0]);
                    _InPhase.Add(_cycle[0]);
                    _Q2.Add(_cycle[0]);
                    _I2.Add(_cycle[0]);
                    _re.Add(_cycle[0]);
                    _im.Add(_cycle[0]);
                }
                else
                {
                    // Once is ready, calculate the cycle.
                    //hfp = _A * (_smooth[0] - 2 * _smooth[1] + _smooth[2]) + 2 * _B * _cycle[0] - (_B * _B) * _cycle[1];
                    _cycle.Add(Detrend(_smooth));

                    // Detrend and InPhase calculation.
                    //instPeriod = (decimal)(.075 * _Period[1] + .54);
                    _Quadrature.Add(Detrend(_cycle));
                    _InPhase.Add(_cycle[3]);

                    // Advance the phase of InPhase and Detrend by 90 degrees
                    var jI = Detrend(_InPhase);
                    var jQ = Detrend(_Quadrature);

                    // Phasor addition for 3 bar averaging
                    _I2.Add(_InPhase[0] - jQ);
                    _Q2.Add(_Quadrature[0] + jI);
                    
                    // Smooth he I and Q components before applying the discriminator
                    _I2[0] = .2m * _I2[0] + .8m * _I2[1];
                    _Q2[0] = .2m * _Q2[0] + .8m * _Q2[1];

                    // Homodyne Discriminator
                    _re.Add(_I2[0] * _I2[1] + _Q2[0] * _Q2[1]);
                    _im.Add(_I2[0] * _Q2[1] - _Q2[1] * _I2[1]);
                    double re = (double)_re[0];
                    double im = (double)_im[0];
                    if (_re.Count > 1)
                    {
                        re = (double) (.2m*_re[0] + .8m*_re[1]);
                        im = (double) (.2m*_im[0] + .8m*_im[1]);
                    }

                    if (im != 0.0 && re != 0.0)
                    {
                        _Period[0] = 360/Math.Atan(im/re);
                    }
                    if (_Period[0] > 1.5*_Period[1])
                    {
                        _Period[0] = 1.5*_Period[1];
                    }
                    if (_Period[0] < 0.67*_Period[1])
                    {
                        _Period[0] = 0.67*_Period[1];
                    }
                    if (_Period[0] < 6.0)
                    {
                        _Period[0] = 6.0;
                    }
                    if (_Period[0] > 50.0)
                    {
                        _Period[0] = 50.0;
                    }
                }
                _Period[0] = .2 * _Period[0] + .8 * _Period[1];             // the .2 alpha EMA of itself
            }
            
            _SmoothPeriod.Update(idp(time, (decimal)_Period[0]));   // the .33 alpha EMA of the Period
            return _SmoothPeriod.Current.Value;
        }

        private decimal Detrend(RollingWindow<decimal> sourceWindow)
        {
            return (_quadCoeffA * sourceWindow[0] + _quadCoeffB * sourceWindow[2] -  _quadCoeffB * sourceWindow[4] - _quadCoeffA * sourceWindow.MostRecentlyRemoved) 
                 * (decimal)(.075 * _Period[1] + .54);
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
