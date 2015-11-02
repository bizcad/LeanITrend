using NUnit.Framework;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Indicators;
using System;
using System.Collections.Generic;

namespace QuantConnect.Tests
{
    [TestFixture]
    public class MSAlgorithmTest
    {
        [Test]
        public void MSAFullTest()
        {
            #region Fields

            int idx = 0;
            int day = 0;
            DateTime Time = DateTime.Today;
            DateTime testTime = Time;

            double amplitude = 1;
            int shift = 100;
            int waveLength = 45;
            IndicatorDataPoint ssObs;

            Indicator smoothedSeries = new Identity("SmoothedSeries");
            MSAStrategy strategy = new MSAStrategy(smoothedSeries, 2, 3);

            #endregion Fields

            #region Warming up

            Console.WriteLine("Warming up");

            do
            {
                ssObs = new IndicatorDataPoint(testTime, SineWave(idx, amplitude * idx, waveLength, shift));
                smoothedSeries.Update(ssObs);
                testTime = testTime.AddMinutes(1);
                idx++;
                if (idx > 59)
                {
                    day++;
                    testTime = Time.AddDays(day);

                    idx = 0;

                    switch (day)
                    {
                        case 1:
                            amplitude = 2;
                            shift = 150;
                            waveLength = 15;
                            break;

                        case 2:
                            amplitude = 1.5;
                            shift = 100;
                            waveLength = 20;
                            break;

                        case 3:
                            amplitude = 3;
                            shift = 180;
                            waveLength = 12;
                            break;

                        default:
                            break;
                    }
                    Console.WriteLine("New day: " + day);
                }
            } while (!strategy.IsReady);

            Console.WriteLine("Strategy ready!\nStarting signal test.");

            #endregion Warming up

            #region Testing signals

            var expectedSignals = new Dictionary<int, OrderSignal>();
            expectedSignals.Add(34, OrderSignal.goLong);
            expectedSignals.Add(40, OrderSignal.goShort);
            expectedSignals.Add(46, OrderSignal.goLong);
            expectedSignals.Add(52, OrderSignal.goShort);
            expectedSignals.Add(58, OrderSignal.goLong);

            for (int i = 0; i < 60; i++)
            {
                ssObs = new IndicatorDataPoint(testTime, SineWave(i, amplitude * i, waveLength, shift));
                smoothedSeries.Update(ssObs);

                Console.WriteLine(string.Format("{0}\t|\t{1}\t|\t{2}",
                    i,
                    ssObs.Value.SmartRounding(),
                    strategy.ActualSignal
                    ));

                if (expectedSignals.ContainsKey(i))
                {
                    Assert.AreEqual(expectedSignals[i], strategy.ActualSignal, string.Format("Bar {0} test.", i));
                }
                else
                {
                    Assert.AreEqual(OrderSignal.doNothing, strategy.ActualSignal, string.Format("Bar {0} test.", i));
                }

                testTime = testTime.AddMinutes(1);
            }

            #endregion Testing signals
        }

        public decimal SineWave(double step, double amplitude, double waveLength, double shift)
        {
            double obs = amplitude * Math.Sin(2 * Math.PI / waveLength * step) + shift;
            return (decimal)obs;
        }
    }
}