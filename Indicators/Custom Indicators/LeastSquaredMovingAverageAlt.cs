using System;
using System.Collections;
using System.Collections.Generic;
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
    public class LeastSquaredMovingAverageAlt : Indicator
    {
        /// <summary>
        /// Array representing the time.
        /// </summary>
        private double[] t;

        /// <summary>
        /// The indicator period.
        /// </summary>
        int _period;

        /// <summary>
        /// The series q
        /// </summary>
        Queue<double> seriesQ;

        /// <summary>
        /// Initializes a new instance of the <see cref="LeastSquaredMovingAverageAlt"/> class.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The number of data points to hold in the window</param>
        public LeastSquaredMovingAverageAlt(string name, int period)
            : base(name)
        {
            _period = period;
            t = Vector<double>.Build.Dense(period, i => i + 1).ToArray();
            seriesQ = new Queue<double>(_period);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeastSquaredMovingAverageAlt"/> class.
        /// </summary>
        /// <param name="period">The number of data points to hold in the window.</param>
        public LeastSquaredMovingAverageAlt(int period)
            : this("LSMA" + period, period) { }


        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady
        {
            get
            {
                return seriesQ.Count > _period;
            }
        }


        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>
        /// A new value for this indicator
        /// </returns>
        protected override decimal ComputeNextValue(IndicatorDataPoint input)
        {
            // Until the windows is ready, the indicator returns the input value.
            decimal output = input;

            seriesQ.Enqueue((double)input.Value);

            if (IsReady)
            {
                seriesQ.Dequeue();
                var series = seriesQ.ToArray();
                // Fit OLS
                Tuple<double, double> ols = Fit.Line(x: t, y: series);
                var alfa = (decimal)ols.Item1;
                var beta = (decimal)ols.Item2;
                // Make the projection.
                output = alfa + beta * (_period);
            }
            return output;
        }
    }
}