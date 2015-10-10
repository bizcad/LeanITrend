using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Based upon the article by Ceasar Alveraz found at: http://alvarezquanttrading.com/2014/08/11/simple-ideas-for-a-mean-reversion-strategy-with-good-results/
    /// </summary>
    public class SimpleSystemAlgorithm : QCAlgorithm
    {
        private int LiveSignalIndex = 8;

        #region "Variables"


        #region Indicators

        private const int Count = 10;
        private int dataCount = 0;
        private DateTime last;
        public RollingWindow<IndicatorDataPoint> Price;
        public RollingWindow<IndicatorDataPoint> Lows;

        private bool LowerLows;
        public AverageTrueRange ATR;

        private class SelectionData
        {
            public readonly SimpleMovingAverage SMA100;
            public readonly SimpleMovingAverage SMA5;

            public SelectionData()
            {
                SMA5 = new SimpleMovingAverage(5);
                SMA100 = new SimpleMovingAverage(100);
            }

            public bool Update(DateTime time, decimal value)
            {
                return SMA5.Update(time, value) && SMA100.Update(time, value);
            }
        }
        #endregion // indicators


        #endregion // variables

        #region "Strategy"
        private string comment;

        #endregion

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {

            UniverseSettings.Leverage = 1m;
            UniverseSettings.Resolution = Resolution.Daily;

            //Initialize dates
            SetStartDate(1995, 01, 01);
            SetEndDate(2015, 08, 15);
            SetCash(25000);

            var averages = new ConcurrentDictionary<string, SelectionData>();
            SetUniverse(coarse => (from cf in coarse
                                   let avg = averages.GetOrAdd(cf.Symbol, sym => new SelectionData())
                                   where avg.Update(cf.EndTime, cf.Price)
                                   // only pick symbols who have Close > 100 day MA and < 5 day MA
                                   where cf.Price > avg.SMA100 && cf.Price < avg.SMA5
                                   orderby cf.Volume descending
                                   select cf).Take(Count));

            //Add as many securities as you like. All the data will be passed into the event handler:
            //AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

            #region indicators

            Price = new RollingWindow<IndicatorDataPoint>(14);
            Lows = new RollingWindow<IndicatorDataPoint>(4);
            LowerLows = false;
            ATR = this.ATR("ATR10", 10, MovingAverageType.Simple, Resolution.Daily);

            #endregion

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            dataCount++;

            // Add the history for the bar
            var time = this.Time;
            // Update the indicators
            //Price.Add(idp(time, (data[symbol].Close + data[symbol].Open) / 2));
            //Trend.Update(idp(time, Price[0].Value));
            //Lows.Add(idp(time,data[symbol].Low));
            //ATR.Update(data[symbol]);
            //LowerLows = Lows[0].Value < Lows[1].Value && Lows[1].Value < Lows[2].Value && Lows[2].Value < Lows[3].Value;



        }
        /// <summary>
        /// Convenience function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from 
        /// new IndicatorDataPoint(this.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

    }
}
