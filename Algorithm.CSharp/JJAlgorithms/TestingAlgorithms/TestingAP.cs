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
    public partial class TestingAP : QCAlgorithm
    {
        #region Fields
        private static string[] Symbols = {"SPY"};
        
        int _shortPeriod = 10;
        int _longPeriod = 30;
        int _correlationWidth = 3;
        AutocorrelogramPeriodogram AP;

        int barCounter = 0;
        #endregion

        #region Array inputs
        decimal[] prices = new decimal[100]
            {
                /*
                 * Formula:
                 * prices[i] = 10 * sin(2 * pi * i / wavelenght[i]) + 15 + (i / 2)
                 * i = [0, 1, 2,..., 99]
                 * wavelenght[i] = |25 if i < 25
                 *                 |15 else
                 */
                15m,    17.99m, 20.82m, 23.35m, 25.44m, 27.01m, 27.98m, 28.32m, 28.05m, 27.21m,
                25.88m, 24.18m, 22.25m, 20.25m, 18.32m, 16.62m, 15.29m, 14.45m, 14.18m, 14.52m,
                15.49m, 17.06m, 19.15m, 21.68m, 24.51m, 27.5m,  30.49m, 33.32m, 35.85m, 37.94m,
                39.51m, 40.48m, 40.82m, 40.55m, 39.71m, 38.38m, 36.68m, 34.75m, 32.75m, 30.82m,
                29.12m, 27.79m, 26.95m, 26.68m, 27.02m, 27.99m, 29.56m, 31.65m, 34.18m, 37.01m,
                48.66m, 46.38m, 43.08m, 39.42m, 36.12m, 33.84m, 33.05m, 33.99m, 36.57m, 40.43m,
                45m,    49.57m, 53.43m, 56.01m, 56.95m, 56.16m, 53.88m, 50.58m, 46.92m, 43.62m,
                41.34m, 40.55m, 41.49m, 44.07m, 47.93m, 52.5m,  57.07m, 60.93m, 63.51m, 64.45m,
                63.66m, 61.38m, 58.08m, 54.42m, 51.12m, 48.84m, 48.05m, 48.99m, 51.57m, 55.43m,
                60m,    64.57m, 68.43m, 71.01m, 71.95m, 71.16m, 68.88m, 65.58m, 61.92m, 58.62m
            };
        #endregion

        #region QCAlgorithm Methods
        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 7);
            SetCash(250000);
            foreach (var symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute); 
            }

            AP = new AutocorrelogramPeriodogram(_shortPeriod, _longPeriod, _correlationWidth);
        }

        public void OnData(TradeBars data)
        {
            AP.Update(new IndicatorDataPoint(Time, prices[barCounter]));
            Console.WriteLine(string.Format("Bar: {0} | AP is ready? {1} | DC: {2}", barCounter, AP.IsReady, AP.Current.Value));
            barCounter = (barCounter < 100) ? barCounter + 1 : 99;
        }
        #endregion
    }
}