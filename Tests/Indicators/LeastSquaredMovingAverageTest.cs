using NUnit.Framework;
using QuantConnect.Indicators;
using System;

namespace QuantConnect.Tests.Indicators
{
    /// <summary>
    /// Result tested vs. Python available at: http://tinyurl.com/o7redso
    /// </summary>
    [TestFixture]
    public class LeastSquaredMovingAverageTest
    {
        #region Array input

        // Real AAPL minute data rounded to 2 decimals.
        private decimal[] prices = new decimal[40]
        {
            125.99m, 125.91m, 125.75m, 125.62m, 125.54m, 125.45m, 125.47m,
            125.4m , 125.43m, 125.45m, 125.42m, 125.36m, 125.23m, 125.32m,
            125.26m, 125.31m, 125.41m, 125.5m , 125.51m, 125.41m, 125.54m,
            125.51m, 125.61m, 125.43m, 125.42m, 125.42m, 125.46m, 125.43m,
            125.4m , 125.35m, 125.3m , 125.28m, 125.21m, 125.37m, 125.32m,
            125.34m, 125.37m, 125.26m, 125.28m, 125.16m
        };

        #endregion Array input

        [Test]
        public void LSMAComputesCorrectly()
        {
            int LSMAPeriod = 20;
            LeastSquaredMovingAverage LSMA = new LeastSquaredMovingAverage(LSMAPeriod);
            DateTime time = DateTime.Now;

            #region Array input

            decimal[] expected = new decimal[40]
            {
                125.99m  , 125.91m  , 125.75m  , 125.62m  , 125.54m  , 125.45m  , 125.47m  , 125.4m   , 125.43m  , 125.45m  ,
                125.42m  , 125.36m  , 125.23m  , 125.32m  , 125.26m  , 125.31m  , 125.41m  , 125.5m   , 125.51m  , 125.41m  ,
                125.328m , 125.381m , 125.4423m, 125.4591m, 125.4689m, 125.4713m, 125.4836m, 125.4834m, 125.4803m, 125.4703m,
                125.4494m, 125.4206m, 125.3669m, 125.3521m, 125.3214m, 125.2986m, 125.2909m, 125.2723m, 125.2619m, 125.2224m,
            };

            #endregion Array input

            decimal[] actual = new decimal[prices.Length];

            for (int i = 0; i < prices.Length; i++)
            {
                LSMA.Update(new IndicatorDataPoint(time, prices[i]));
                decimal LSMAValue = Math.Round(LSMA.Current.Value, 4);
                actual[i] = LSMAValue;

                Console.WriteLine(string.Format("Bar : {0} | {1}, Is ready? {2}", i, LSMA.ToString(), LSMA.IsReady));
                time = time.AddMinutes(1);
            }
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void ResetsProperly()
        {
            int LSMAPeriod = 10;
            DateTime time = DateTime.Now;

            LeastSquaredMovingAverage LSMA = new LeastSquaredMovingAverage(LSMAPeriod);

            for (int i = 0; i < LSMAPeriod + 1; i++)
            {
                LSMA.Update(new IndicatorDataPoint(time, 1m));
                time.AddMinutes(1);
            }
            Assert.IsTrue(LSMA.IsReady, "LSMA ready");
            LSMA.Reset();
            TestHelper.AssertIndicatorIsInDefaultState(LSMA);
        }
    }
}