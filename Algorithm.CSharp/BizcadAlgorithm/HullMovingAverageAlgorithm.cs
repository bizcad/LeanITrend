using System;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.MyAlgorithms
{
    /// <summary>
    /// A test algo to look at the HullMovingAverage for the SPY
    /// </summary>
    public class HullMovingAverageAlgorithm : QCAlgorithm
    {
        private string symbol = "SPY";

        // Custom Logging
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private string ondataheader = @"Time,CurrentBar,Open,High,Low,Close,HMA7,HMA14,HMA28,Instant";

        private int barcount = 0;
        private RollingWindow<IndicatorDataPoint> Price;
        private HullMovingAverage hma7;
        private HullMovingAverage hma14;
        private HullMovingAverage hma28;
        private RollingWindow<IndicatorDataPoint> instantTrend;


        private decimal a = .05m;  // used in instantTrend


        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {
            //mylog.Debug(transheader);
            mylog.Debug(ondataheader);

            //Initialize dates
            SetStartDate(2015, 5, 13);
            SetEndDate(2015, 5, 13);
            SetCash(25000);

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);


            Price = new RollingWindow<IndicatorDataPoint>(14);
            hma7 = new HullMovingAverage("hma7", 7);
            hma14 = new HullMovingAverage("hma14",14);
            hma28 = new HullMovingAverage("hma28",28);
            instantTrend = new RollingWindow<IndicatorDataPoint>(7);

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            barcount++;
            var time = this.Time;
            hma7.Update(time, data[symbol].Close);
            hma14.Update(time, data[symbol].Close);
            hma28.Update(time, data[symbol].Close);
            Price.Add(idp(time, data[symbol].Close));
            UpdateInstantTrend(time);
            if (hma28.IsReady)
            {
                string logmsg = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                    this.Time,
                    barcount,
                    data[symbol].Open,
                    data[symbol].High,
                    data[symbol].Low,
                    data[symbol].Close,
                    hma7.Current.Value,
                    hma14.Current.Value,
                    hma28.Current.Value,
                    instantTrend[0].Value,
                    "");
                mylog.Debug(logmsg);
            }
        }

        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

        private void UpdateInstantTrend(DateTime time)
        {
            if (barcount > 2)
            {
                // From Ehlers page 16 equation 2.9
                var it = (a - ((a / 2) * (a / 2))) * Price[0].Value + ((a * a) / 2) * Price[1].Value
                         - (a - (3 * (a * a) / 4)) * Price[2].Value + 2 * (1 - a) * instantTrend[0].Value
                         - ((1 - a) * (1 - a)) * instantTrend[1].Value;
                instantTrend.Add(idp(time, it));
                //instantTrendTrigger.Add(idp(time, instantTrend[2].Value));
            }
            else
            {
                instantTrend.Add(idp(time, Price[0].Value));
                //instantTrendTrigger.Add(idp(time, Price[0].Value));
            }

        }
    }
}
