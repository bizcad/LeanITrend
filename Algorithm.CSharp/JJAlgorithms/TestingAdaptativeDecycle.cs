using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using QuantConnect.Algorithm;
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

        Decycle decycle;
        AdaptativeDecycle adaptativeDecycle;
                
        StringBuilder toFile = new StringBuilder();
        #endregion

        #region QCAlgorithm Methods
        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 8);

            SetCash(250000);

            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute); 

            decycle = new Decycle(10);
            adaptativeDecycle = new AdaptativeDecycle();

            toFile.AppendLine("Bar, Close, Decycle(10), AdaptativeDecycle, period");

            counter = 0;
        }

        public void OnData(TradeBars data)
        {
            decycle.Update(new IndicatorDataPoint(Time, data[symbol].Value));
            adaptativeDecycle.Update(new IndicatorDataPoint(Time, data[symbol].Value));
            if (counter % 50 == 0)
            {
                period += 10;
                adaptativeDecycle.AdaptativePeriod = period;
            }
            string newLine = string.Format("{0}, {1}, {2}, {3}, {4}", counter, data[symbol].Close, decycle.Current.Value,
                adaptativeDecycle.Current.Value, period);
            toFile.AppendLine(newLine);
            counter++;
        }

        
        public override void OnEndOfAlgorithm()
        {
            string filePath = @"C:\Users\JJ\Desktop\MA y señales\ITrend Debug\AdaptativeDecycle.csv";
            //File.Create(filePath);
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.AppendAllText(filePath, toFile.ToString());
        }
        #endregion

        #region Methods
        
        # endregion

    }
}