using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using QuantConnect.Algorithm;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Orders;
using QuantConnect.Indicators;


namespace QuantConnect
{
    public partial class TestingLSMA : QCAlgorithm
    {
        #region Fields
        private static string symbol = "SPY";
        LeastSquaredMovingAverage LSMA;
        #endregion

        #region QCAlgorithm Methods
        public override void Initialize()
        {
            SetStartDate(2010, 01, 1);
            SetEndDate(2012, 12, 31);

            SetCash(100000);

            AddSecurity(SecurityType.Equity, symbol, Resolution.Daily);
            var close = Identity(symbol);

            LSMA = new LeastSquaredMovingAverage(20);
            RegisterIndicator(symbol, LSMA, Resolution.Daily, Field.Close);
            
            var chart = new Chart("Plot");
            chart.AddSeries(new Series(close.Name));
            chart.AddSeries(new Series(LSMA.Name));
            
            PlotIndicator("Plot", close);
            PlotIndicator("Plot", true, LSMA);
        }

        public void OnData(TradeBars data)
        {
            if (!Portfolio[symbol].Invested)
            {
                SetHoldings(symbol, 1);
            }
        }
        #endregion
    }
}