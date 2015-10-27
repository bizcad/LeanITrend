using System;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// The Least Squares Moving Average (LSMA) first calculates a least squares regression line
    /// over the preceding time periods, and then projects it forward to the current period. In
    /// essence, it calculates what the value would be if the regression line continued.
    /// Source: https://rtmath.net/helpFinAnalysis/html/b3fab79c-f4b2-40fb-8709-fdba43cdb363.htm
    /// </summary>
    public class LeastSquaredMovingAverage : WindowIndicator<IndicatorDataPoint>
    {
        /// <summary>
        /// Array representing the time.
        /// </summary>
        private double[] t;

        /// <summary>
        /// Initializes a new instance of the <see cref="LeastSquaredMovingAverage"/> class.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The number of data points to hold in the window</param>
        public LeastSquaredMovingAverage(string name, int period)
            : base(name, period)
        {
            t = Vector<double>.Build.Dense(period, i => i + 1).ToArray();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeastSquaredMovingAverage"/> class.
        /// </summary>
        /// <param name="period">The number of data points to hold in the window.</param>
        public LeastSquaredMovingAverage(int period)
            : this("LSMA" + period, period) { }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="window"></param>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>
        /// A new value for this indicator
        /// </returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // Until the windows is ready, the indicator returns the input value.
            decimal output = input;
            if (IsReady)
            {
                // Sort the windows by time, convert the observations ton double and transform it to a double array
                double[] series = window
                    .OrderBy(i => i.Time)
                    .Select(i => Convert.ToDouble(i.Value))
                    .ToArray<double>();
                // Fit OLS
                Tuple<double, double> ols = Fit.Line(x: t, y: series);
                var alfa = (decimal)ols.Item1;
                var beta = (decimal)ols.Item2;
                // Make the projection.
                output = alfa + beta * (Period);
            }
            return output;
        }
    }
}