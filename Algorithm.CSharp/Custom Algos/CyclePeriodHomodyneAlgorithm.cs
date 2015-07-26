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
    public partial class CyclePeriodHomodyneAlgorithm : QCAlgorithm
    {
        string symbol = "IBM";
        int counter;

        CyclePeriodHomodyme cph;
                
        StringBuilder toFile = new StringBuilder();

        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 7);

            SetCash(25000);

            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
            cph = new CyclePeriodHomodyme("CyclePeriodHomodyme");
            counter = 0;
            
            // String to write the csv file
            toFile.AppendLine("Time, Counter, Signal, smooth, cycle, quadrature, InPhase, Q2, I2, Real, Imaginary, SmoothPeriod, Period, Wavelength");
            
        }

        public void OnData(TradeBars data)
        {
            decimal signal = (decimal)sinewave(counter, WaveLength(counter), 50, 1, 0, 1);
            
            if (counter == 111)
                System.Threading.Thread.Sleep(100);
            if (cph.Update(idp(Time, signal)))
            {

                string newLine = string.Format("{0}, {1}, {2}, {3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                    Time, counter, signal,
                    cph._smooth[0],
                    cph._cycle[0],
                    cph._Quadrature[0],
                    cph._InPhase[0],
                    cph._Q2[0],
                    cph._I2[0],
                    cph._re[0],
                    cph._im[0],
                    cph._SmoothPeriod.Current.Value,
                    cph.Current.Value,
                    WaveLength(counter)
                    ,""
                    , "");
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
            string filePath = @"C:\Users\Nick\Documents\Visual Studio 2013\Projects\LeanITrend\Engine\bin\Debug\CyclePeriodHomodyneResults.csv";
            //File.Create(filePath);
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.AppendAllText(filePath, toFile.ToString());
        }

    }
}