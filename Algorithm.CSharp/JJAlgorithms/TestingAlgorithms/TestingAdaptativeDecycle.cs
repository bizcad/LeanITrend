using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using QuantConnect.Algorithm;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Orders;
using QuantConnect.Indicators;


namespace QuantConnect
{
    public partial class TestingAdaptativeDecycle : QCAlgorithm
    {
        #region Fields
        private static string symbol = "SPY";
        int counter;
        int period = 5;

        Decycle decycleAdaptive;
        Decycle decycle;
        AutocorrelogramPeriodogram AP;
                
        StringBuilder toFile = new StringBuilder();
        #endregion

        #region QCAlgorithm Methods
        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 10);

            SetCash(1);

            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
            int decyclePeriod = 10;
            decycle = new Decycle(decyclePeriod);
            decycleAdaptive = new Decycle(decyclePeriod);
            AP = new AutocorrelogramPeriodogram(30, 60, 15);

            toFile.AppendLine("Bar,Close,Decycle"+decyclePeriod+",AdaptativeDecycle,DominantCycle");

            counter = 0;
        }

        public void OnData(TradeBars data)
        {
            decycle.Update(new IndicatorDataPoint(Time, data[symbol].Value));
            AP.Update(new IndicatorDataPoint(Time, data[symbol].Value));
            decycleAdaptive.AdaptativePeriod = (AP.IsReady) ? (int)AP.Current.Value : 10;

            decycleAdaptive.Update(new IndicatorDataPoint(Time, data[symbol].Value));

            string newLine = string.Format("{0}, {1}, {2}, {3}, {4}", counter, data[symbol].Close, decycle.Current.Value, decycleAdaptive.Current.Value, AP.Current.Value);
            toFile.AppendLine(newLine);
            counter++;
        }

        
        public override void OnEndOfAlgorithm()
        {
            string fileName = @"AdaptativeDecycleAP.csv";
            string filePath = AssemblyLocator.ExecutingDirectory() + fileName;
            if (File.Exists(filePath)) File.Delete(filePath);
            File.AppendAllText(filePath, toFile.ToString());
        }
        #endregion

        #region Methods
        
        # endregion

    }
}