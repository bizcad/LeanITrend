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

        public HighPassFilter hpf;
        public SuperSmoother sSmoother;
        public RollingWindow<IndicatorDataPoint> sSmootherWindow;

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
            sSmootherWindow = new RollingWindow<IndicatorDataPoint>(longPeriod + _correlationWidth);
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
            List<double> DTF;

            this.hpf.Update(input);
            this.sSmoother.Update(this.hpf.Current);
            this.sSmootherWindow.Add(this.sSmoother.Current);

            //if (!this.IsReady)
            //{
            //    dominantCycle = 0m;
            //}
            //else
            //{
            //    Correlations = EstimateCorrelations();
            //    DTF = EstimateDFT(Correlations);
            //}
            return 1m;
        }

        #endregion ComputeNextValue Method

        #region Auxiliar methods

        private List<double> EstimateDFT(List<double> Correlations)
        {
            List<double> sinePart = new List<double>();
            List<double> cosinePart = new List<double>();

            for (int period = _shortPeriod; period <= _longPeriod; period++)
            {
                double sinePartSum = 0;
                double cosinePartSum = 0;

                for (int n = 1; n <= _longPeriod; n++)
                {
                    sinePartSum += Correlations[period] * Math.Sin(2d * Math.PI / (double)period);
                    cosinePartSum += Correlations[period] * Math.Cos(2d * Math.PI / (double)period);
                }
                sinePart.Add(sinePartSum);
                cosinePart.Add(cosinePartSum);
            }

            var sinVector = Vector<double>.Build.DenseOfEnumerable(sinePart);
            return sinePart;
        }

        private List<double> EstimateCorrelations()
        {
            List<double> correlations = new List<double>();

            var currentSeries = from obs in sSmootherWindow.ToList().GetRange(0, _correlationWidth)
                                select (double)obs.Value;

            for (int lag = 1; lag <= _longPeriod; lag++)
            {
                var laggedSeries = from obs in sSmootherWindow.ToList().GetRange(lag, _correlationWidth)
                                   select (double)obs.Value;
                double pearson = Correlation.Pearson(currentSeries, laggedSeries);
                correlations.Add(pearson);
            }
            return correlations;
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