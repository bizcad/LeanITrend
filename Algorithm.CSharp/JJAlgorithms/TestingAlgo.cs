using System;
using System.Collections;
using System.Collections.Generic;

using System.Text;
using System.IO;

using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Orders;
using QuantConnect.Indicators;


namespace QuantConnect
{
    public partial class TestingAlgo : QCAlgorithm
    {
        string symbol = "IBM";
        int counter;
        
        CyclePeriod cyclePeriod;
                
        StringBuilder toFile = new StringBuilder();

        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 7);

            SetCash(25000);

            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
            cyclePeriod = new CyclePeriod("Period");
            counter = 0;
            
            // String to write the csv file
            toFile.AppendLine("Time, Counter, Signal, smooth, cycle, quadrature, deltaPhase, period, instPeriod, Wavelength, Period");
            
        }

        public void OnData(TradeBars data)
        {
            decimal signal = (decimal)sinewave(counter, WaveLength(counter), 50, 1, 0, 1);

            if (cyclePeriod.Update(idp(Time, signal)))
            {

                string newLine = string.Format("{0}, {1}, {2}, {3},{4},{5},{6},{7},{8},{9},{10},{11}",
                    Time, counter, signal,
                    cyclePeriod._smooth[0],
                    cyclePeriod._cycle[0],
                    cyclePeriod._quadrature[0],
                    cyclePeriod._deltaPhase[0],
                    cyclePeriod._period,
                    cyclePeriod._instPeriod,
                    WaveLength(counter), 
                    cyclePeriod.Current.Value
                    ,"");
                toFile.AppendLine(newLine); 
            }
            counter++;
        }

        private int WaveLength(int counter)
        {
            int waveLength = 30;
            if (counter <= 100) waveLength = 30;
            else if (counter > 100 && counter <= 300) waveLength = 60;
            else if (counter > 300 && counter <= 400) waveLength = 20;
            else if (counter > 400 && counter <= 600) waveLength = 40;
            else waveLength = 15;
            return waveLength;
        }

        private double sinewave(double t, double waveLength, double Vp, double fo, double Phase, double Vdc)
        {
            var pi = Math.PI;
            return Vp * Math.Sin(2 * pi * fo * t / waveLength + Phase * pi / 180) + Vdc;

        }

        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

        public override void OnEndOfAlgorithm()
        {
            string filePath = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\CyclePeriodResults.csv";
            //File.Create(filePath);
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.AppendAllText(filePath, toFile.ToString());
        }

    }
}