using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp.BizcadAlgorithm
{
    public class ConsolidatorAlgorithm : QCAlgorithm
    {
        public override void Initialize()
        {
            SetStartDate(2013, 1, 1);
            SetEndDate(2013, 2, 1);
            SetCash(25000);

            AddSecurity(SecurityType.Equity, "SPY", Resolution.Minute);

            // define our 15 minute consolidator
            var fifteenMinuteConsolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(15));

            // if we want to make decisions every 15 minutes as well, we can add an event handler
            // to the DataConsolidated event
            fifteenMinuteConsolidator.DataConsolidated += OnFiftenMinuteSPY;

            int fast = 15;
            int slow = 30;

            // define our EMA, we'll manually register this, so we aren't using the helper function 'EMA(...)'
            var fastEmaOnFifteenMinuteBars = new ExponentialMovingAverage("SPY_EMA15", fast);
            var slowEmaOnFifteenMinuteBars = new ExponentialMovingAverage("SPY_EMA30", slow);

            // we can define complex indicator's using various extension methods.
            // here I use the 'Over' extension method which performs division
            // so this will be fast/slow. This returns a new indicator that represents
            // the division operation between the two
            var ratio = fastEmaOnFifteenMinuteBars.Over(slowEmaOnFifteenMinuteBars, "SPY_Ratio_EMA");

            // now we can use the 'Of' extension method to define the ROC on the ratio
            // The 'Of' extension method allows combining multiple indicators together such
            // that the data from one gets sent into the other
            var rocpOfRatio = new RateOfChangePercent("SPY_ROCP_Ratio", fast).Of(ratio);

            // we an even define a smoothed version of this indicator
            var smoothedRocpOfRatio = new ExponentialMovingAverage("SPY_Smoothed_ROCP_Ratio", 5).Of(rocpOfRatio);

            // register our indicator and consolidator together. this will wire the consolidator up to receive
            // data for the specified symbol, and also set up the indicator to receive its data from the consolidator
            //RegisterIndicator("SPY", fastEmaOnFifteenMinuteBars, fifteenMinuteConsolidator);
            //RegisterIndicator("SPY", slowEmaOnFifteenMinuteBars, fifteenMinuteConsolidator);

            // register the indicator to be plotted along
            PlotIndicator("SPY", fastEmaOnFifteenMinuteBars);
            PlotIndicator("SPY", slowEmaOnFifteenMinuteBars);
            PlotIndicator("SPY_ROCP_Ratio", rocpOfRatio, smoothedRocpOfRatio);
            PlotIndicator("SPY_Ratio_EMA", ratio);
        }

        //15 minute events here:
        public void OnFiftenMinuteSPY(object sender, TradeBar data)
        {
            if (!Portfolio.Invested)
            {
                SetHoldings("SPY", 1.0);
            }
        }

        //Traditional 1 minute events here:
        public void OnData(TradeBars data)
        {
        }
    }
}