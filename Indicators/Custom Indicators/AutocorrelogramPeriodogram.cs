using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Indicator to measure the dominant cycle period.
    /// --> Ref: Cycle Analytics chapter 8.
    /// </summary>
    public class AutocorrelogramPeriodogram : WindowIndicator<IndicatorDataPoint>
    {
        #region Fields

        private readonly int _shortPeriod;
        private readonly int _longPeriod;
        private readonly int _bandwidth;
        private readonly int _correlationWidth;
        private double _decayFactor;
        private double _maxPower = 1d;
        private Vector<double> R = null;
        private HighPassFilter hpf;
        private SuperSmoother sSmoother;
        private RollingWindow<double> sSmootherWindow;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AutocorrelogramPeriodogram"/> class.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="shortPeriod">The period of the low pass filter cut off frequency.</param>
        /// <param name="longPeriod">The period of the high pass filter cut off frequency.</param>
        /// <param name="correlationWidth">Number of pair observations used to estimate the autocorrelation coefficients.</param>
        public AutocorrelogramPeriodogram(string name, int shortPeriod, int longPeriod, int correlationWidth)
            : base(name, correlationWidth)
        {
            _shortPeriod = shortPeriod;
            _longPeriod = longPeriod;
            _bandwidth = longPeriod - shortPeriod;
            _correlationWidth = correlationWidth;
            _decayFactor = EstimateDecayFactor(_shortPeriod, _longPeriod);

            hpf = new HighPassFilter(longPeriod);
            sSmoother = new SuperSmoother(shortPeriod);
            sSmootherWindow = new RollingWindow<double>(longPeriod + _correlationWidth);

            R = Vector<double>.Build.Dense(_bandwidth + 1, 1d);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AutocorrelogramPeriodogram"/> class.
        /// </summary>
        /// <param name="shortPeriod">The period of the low pass filter cut off frequency.</param>
        /// <param name="longPeriod">The period of the high pass filter cut off frequency.</param>
        /// <param name="correlationWidth">Number of pair observations used to estimate the autocorrelation coefficients.</param>
        public AutocorrelogramPeriodogram(int shortPeriod, int longPeriod, int correlationWidth)
            : this("AP" + correlationWidth, shortPeriod, longPeriod, correlationWidth)
        {
        }

        #endregion Constructors

        #region Override Methods

        public override bool IsReady
        {
            get
            {
                return sSmootherWindow.IsReady;
            }
        }

        public override void Reset()
        {
            base.Reset();
            hpf.Reset();
            sSmoother.Reset();
            sSmootherWindow.Reset();
            R = Vector<double>.Build.Dense(_bandwidth + 1, 1d);
        }

        #endregion Override Methods

        #region ComputeNextValue Method

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            decimal dominantCycle;
            List<double> Correlations;
            Vector<double> DTF;

            hpf.Update(input);
            sSmoother.Update(hpf.Current);
            sSmootherWindow.Add((double)sSmoother.Current.Value);

            if (!this.IsReady)
            {
                dominantCycle = 0m;
            }
            else
            {
                Correlations = EstimateAutocorrelations();
                DTF = EstimateDFT(Correlations);
                dominantCycle = EstimateDominantCycle(DTF);
            }
            return dominantCycle;
        }

        #endregion ComputeNextValue Method

        #region Auxiliar methods

        /// <summary>
        /// Estimates the Automatic Gain Control decay factor.
        /// --> Ref: Cycle Analytics page 55
        /// </summary>
        /// <param name="bandwidth">The bandwidth.</param>
        /// <returns></returns>
        private double EstimateDecayFactor(int shorPeriod, int longPeriod)
        {
            double bandwidth = (double)((longPeriod - shorPeriod) / 2);
            double ratio = Math.Pow(10d, -1.5d / 20d);
            return Math.Pow(ratio, 1d / bandwidth);
        }

        /// <summary>
        /// Estimates the autocorrelations.
        /// </summary>
        /// <returns></returns>
        private List<double> EstimateAutocorrelations()
        {
            List<double> correlations = new List<double>();

            var currentSeries = sSmootherWindow.ToList().GetRange(0, _correlationWidth);

            for (int lag = 1; lag <= _longPeriod; lag++)
            {
                var laggedSeries = sSmootherWindow.ToList().GetRange(lag, _correlationWidth);

                double pearson = Correlation.Pearson(currentSeries, laggedSeries);
                correlations.Add(pearson);
            }
            return correlations;
        }

        /// <summary>
        /// Estimates a custom DFT from the autocorrelations.
        /// </summary>
        /// <param name="Correlations">The autocorrelation.</param>
        /// <returns></returns>
        private Vector<double> EstimateDFT(List<double> Correlations)
        {
            Vector<double> sinePart = Vector<Double>.Build.Dense(_bandwidth + 1);
            Vector<double> cosinePart = Vector<Double>.Build.Dense(_bandwidth + 1);
            int period = _shortPeriod;

            for (int idx = 0; idx <= _bandwidth; idx++)
            {
                double sinePartSum = 0;
                double cosinePartSum = 0;
                // Add all the sine and cosine components for each autocorrelation lag.
                for (int lag = 0; lag < _longPeriod; lag++)
                {
                    sinePartSum += Correlations[lag] * Math.Sin(2d * Math.PI * (lag + 1) / period);
                    cosinePartSum += Correlations[lag] * Math.Cos(2d * Math.PI * (lag + 1) / period);
                }
                period++;

                sinePart[idx] = sinePartSum;
                cosinePart[idx] = cosinePartSum;
            }
            sinePart.PointwiseMultiply(sinePart, sinePart);
            cosinePart.PointwiseMultiply(cosinePart, cosinePart);
            return sinePart + cosinePart;
        }

        /// <summary>
        /// Estimates the dominant cycle.
        /// </summary>
        /// <param name="DTF">The DTF.</param>
        /// <returns></returns>
        private decimal EstimateDominantCycle(Vector<double> DTF)
        {
            Vector<double> power;
            Vector<double> periods = Vector<double>.Build.Dense(DTF.Count, i => i + _shortPeriod);

            R = 0.8d * DTF + 0.2d * R;
            _maxPower *= _decayFactor;
            _maxPower = Math.Max(R.Maximum(), _maxPower);
            power = (R / _maxPower).PointwiseMultiply(R / _maxPower);

            return (decimal)(power.DotProduct(periods) / power.Sum()); ;
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

        #endregion Auxiliar methods
    }
}