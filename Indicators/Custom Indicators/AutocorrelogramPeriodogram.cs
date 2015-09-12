using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Indicators
{
    public class AutocorrelogramPeriodogram : WindowIndicator<IndicatorDataPoint>
    {
        #region Fields

        private readonly int _shortPeriod;
        private readonly int _longPeriod;
        private readonly int _bandwidth;
        private readonly int _correlationWidth;
        private double maxPower = 1d;        

        public HighPassFilter hpf;
        public SuperSmoother sSmoother;
        public RollingWindow<double> sSmootherWindow;
        public Vector<double> R = null;
        

        #endregion Fields

        #region Constructors

        public AutocorrelogramPeriodogram(string name, int shortPeriod, int longPeriod, int correlationWidth)
            : base(name, correlationWidth)
        {
            _shortPeriod = shortPeriod;
            _longPeriod = longPeriod;
            _bandwidth = longPeriod - shortPeriod;
            _correlationWidth = correlationWidth;

            hpf = new HighPassFilter(longPeriod);
            sSmoother = new SuperSmoother(shortPeriod);
            sSmootherWindow = new RollingWindow<double>(longPeriod + _correlationWidth);

            R = Vector<double>.Build.Dense(_bandwidth + 1, 1d);
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="period">int - the number of periods in the indicator warmup</param>
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
        }

        #endregion Override Methods

        #region ComputeNextValue Method

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
                Correlations = EstimateCorrelations();
                DTF = EstimateDFT(Correlations);
                dominantCycle = EstimateDominantCycle(DTF);
            }
            return dominantCycle;
        }

        #endregion ComputeNextValue Method

        #region Auxiliar methods

        private List<double> EstimateCorrelations()
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

        private Vector<double> EstimateDFT(List<double> Correlations)
        {
            Vector<double> sinePart = Vector<Double>.Build.Dense(_bandwidth + 1);
            Vector<double> cosinePart = Vector<Double>.Build.Dense(_bandwidth + 1);
            int period = _shortPeriod;

            for (int idx = 0; idx <= _bandwidth; idx++)
            {
                double sinePartSum = 0;
                double cosinePartSum = 0;
                
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

        private decimal EstimateDominantCycle(Vector<double> DTF)
        {
            Vector<double> power;
            Vector<double> periods = Vector<double>.Build.Dense(DTF.Count, i => i + _shortPeriod);

            R = 0.8d * DTF + 0.2d * R;
            maxPower *= 0.9995d;
            maxPower = Math.Max(R.Maximum(), maxPower);
            power = (R / maxPower).PointwiseMultiply(R / maxPower);

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