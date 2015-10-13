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
    public partial class PSARTestingAlgo : QCAlgorithm
    {
        #region Fields
        private static string symbol = "SPY";

        List<ParabolicStopAndReverse> PSARList = new List<ParabolicStopAndReverse>();

        public decimal testerAcum;
        public int counter;

        #endregion

        #region QCAlgorithm Methods
        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 9);

            SetCash(250000);

            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

            PSARList.Add(new ParabolicStopAndReverse());
            PSARList.Add(new ParabolicStopAndReverse(afStart: 0.1m, afIncrement: 0.1m, afMax: 0.2m));
            PSARList.Add(new ParabolicStopAndReverse(afStart: 0.001m, afIncrement: 0.001m, afMax: 0.1m));
            
            RegisterIndicator(symbol, PSARList[0], Resolution.Minute);
            RegisterIndicator(symbol, PSARList[1], Resolution.Minute);
            RegisterIndicator(symbol, PSARList[2], Resolution.Minute);

            testerAcum = 0m;
            counter = 0;
        }

        public void OnData(TradeBars data)
        {
            if (PSARList[0].IsReady &&
                PSARList[1].IsReady &&
                PSARList[2].IsReady)
            {
                decimal test = (PSARList[1].Current.Value / PSARList[0].Current.Value == 1m) &&
                               (PSARList[2].Current.Value / PSARList[0].Current.Value == 1m) ? 1m : 0m;
                
                testerAcum = testerAcum + test;
                counter++;
            }
        }


        public override void OnEndOfAlgorithm()
        {
            string logMenssage = (testerAcum / counter == 1m) ?
                string.Format("Bug found: {0}, {1} and {2} are equal.", PSARList[0].Name, PSARList[1].Name, PSARList[2].Name) : "Nothing to see here";

            Debug(logMenssage);
        }
        #endregion
    }
}