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
            SetEndDate(2013, 10, 8);

            SetCash(25000);

            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
            cyclePeriod = new CyclePeriod("Period");
            counter = 0;
            
            // String to write the csv file
            toFile.AppendLine("Time, Counter, Signal, Wavelenght, Period");
            
        }

        public void OnData(TradeBars data)
        {
            decimal signal = (decimal)sinewave(counter, WaveLenght(counter), 50, 1, 0, 100);

            if (cyclePeriod.Update(idp(Time, signal)))
            {
                string newLine = string.Format("{0}, {1}, {2}, {3}, {4}", Time, counter, signal, WaveLenght(counter), cyclePeriod.Current.Value);
                toFile.AppendLine(newLine); 
            }
            counter++;
        }

        private int WaveLenght(int counter)
        {
            int wavelenght;
            if (counter <= 100) wavelenght = 30;
            else if (counter > 100 && counter <= 300) wavelenght = 60;
            else if (counter > 300 && counter <= 400) wavelenght = 20;
            else if (counter > 400 && counter <= 600) wavelenght = 40;
            else wavelenght = 15;
            return wavelenght;
        }

        private double sinewave(double t, double waveLenght, double Vp, double fo, double Phase, double Vdc)
        {
            var pi = Math.PI;
            return Vp * Math.Sin(2 * pi * fo * t / waveLenght + Phase * pi / 180) + Vdc;

        }

        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

        public override void OnEndOfAlgorithm()
        {
            string filePath = @"C:\Users\JJ\Desktop\MA y señales\DSP indicators\CyclePeriodResults.csv";
            //File.Create(filePath);
            File.AppendAllText(filePath, toFile.ToString());
        }

    }
}