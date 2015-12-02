using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Securities.Equity;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp.BizcadAlgorithm.VixWvf
{
    public class VixWvfAlgorithm : QCAlgorithm
    {

        #region "Variables"

        private DateTime startTime = DateTime.Now;
        private DateTime _startDate = new DateTime(2015, 10, 27);
        private DateTime _endDate = new DateTime(2015, 11, 27);
        private decimal _portfolioAmount = 25000;

        /* +-------------------------------------------------+
         * |Algorithm Control Panel                          |
         * +-------------------------------------------------+*/
        private static int Period = 22; // Instantaneous Trend period.
        private static decimal Tolerance = 0.0001m; // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m; // Percentage tolerance before revert position.
        private static decimal maxLeverage = 1m; // Maximum Leverage.
        private decimal leverageBuffer = 0.25m; // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500; // Maximum shares per operation.
        private decimal RngFac = 0.35m; // Percentage of the bar range used to estimate limit prices.
        private bool noOvernight = true; // Close all positions before market close.
        /* +-------------------------------------------------+*/

        private string[] symbolarray = new string[] { "VIX", "SPY" };
        private List<Symbol> Symbols = new List<Symbol>();
        WilliamsVixFixIndicator wvfh = new WilliamsVixFixIndicator(Period);
        WilliamsVixFixIndicatorReverse wvfl = new WilliamsVixFixIndicatorReverse(Period);
        InverseFisherTransform ifwvfh = new InverseFisherTransform(22);
        InverseFisherTransform ifwvfl = new InverseFisherTransform(22);
        IchimokuKinkoHyo ichi = new IchimokuKinkoHyo("Ichi1");
        IchimokuKinkoHyo ichi5 = new IchimokuKinkoHyo("Ichi5");
        IchimokuKinkoHyo ichi10 = new IchimokuKinkoHyo("Ichi10");
        InstantaneousTrend iTrend = new InstantaneousTrend(22);
        private TradeBarConsolidator tenMinuteConsolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(10));
        private TradeBarConsolidator fiveMinuteConsolidator = new TradeBarConsolidator(TimeSpan.FromMinutes(5));


        private int barcount = 0;
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private TradeBar vix = new TradeBar();

        private bool headingwritten = false;

        #endregion

        public override void Initialize()
        {
            SetStartDate(_startDate); //Set Start Date
            SetEndDate(_endDate); //Set End Date
            SetCash(_portfolioAmount); //Set Strategy Cash

            foreach (string t in symbolarray)
            {
                Symbols.Add(new Symbol(t));
            }

            foreach (string symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                var priceIdentity = Identity(symbol, selector: Field.Close);

            }
            fiveMinuteConsolidator.DataConsolidated += OnFiveMinute;
            tenMinuteConsolidator.DataConsolidated += OnTenMinute;
            RegisterIndicator("SPY", ichi5, new TimeSpan(0,5,0));
            RegisterIndicator("SPY", ichi10, new TimeSpan(0, 10, 0));
            
        }

        private void OnTenMinute(object sender, TradeBar e)
        {
            ichi10.Update(e);
        }

        private void OnFiveMinute(object sender, TradeBar e)
        {
            ichi5.Update(e);
        }
        #region "one minute events"
        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            barcount++;
            string msg;
            if (barcount == 100)
                msg = "here";
            foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            {
                OnDataForSymbol(kvp);
            }
        }

        private void OnDataForSymbol(KeyValuePair<Symbol, TradeBar> data)
        {
            if (data.Key == "VIX")
            {
                vix = data.Value;
            }
            if (data.Key == new Symbol("SPY"))
            {
                wvfh.Update(data.Value);
                wvfl.Update(data.Value);
                ifwvfh.Update(wvfh.Current);
                ifwvfl.Update(wvfl.Current);
                ichi.Update(data.Value);
                iTrend.Update(new IndicatorDataPoint(data.Value.EndTime, data.Value.Close));

                #region "biglog"

                if (!headingwritten)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("Barcount, Symbol,EndTime,Volume,Open,High,Low,Close");
                    sb.Append(",EndTime");
                    sb.Append(",vix");
                    sb.Append(",iTrend");
                    sb.Append(",wvfh");
                    sb.Append(",fwvfh");
                    sb.Append(",wvfl");
                    sb.Append(",fwvfl");
                    sb.Append(",t1");
                    sb.Append(",k1");
                    sb.Append(",sa1");
                    sb.Append(",sb1");
                    sb.Append(",t5");
                    sb.Append(",k5");
                    sb.Append(",sa5");
                    sb.Append(",sb5");
                    sb.Append(",t10");
                    sb.Append(",k10");
                    sb.Append(",sa10");
                    sb.Append(",sb10");
                    mylog.Debug(sb.ToString());
                    headingwritten = true;
                }
                string logmsg =
                    string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}" +
                        ",{20},{21},{22},{23},{24},{25},{26},{27}" //,{28},{29} "
                        //+ ",{30},{31},{32},{33}"
                        ,
                        barcount,
                        data.Key,
                        data.Value.EndTime,
                        data.Value.Volume,
                        data.Value.Open,
                        data.Value.High,
                        data.Value.Low,
                        data.Value.Close,
                        data.Value.EndTime.ToShortTimeString(),
                        vix.Close,
                        iTrend.Current.Value,
                        wvfh.Current.Value,
                        ifwvfh.Current.Value * -1,
                        wvfl.Current.Value,
                        ifwvfl.Current.Value * -1,
                        //data.Value.EndTime.ToShortTimeString(),
                        //data.Value.Close,
                        ichi.Tenkan.Current.Value,
                        ichi.Kijun.Current.Value,
                        ichi.SenkouA.Current.Value,
                        ichi.SenkouB.Current.Value,
                        //data.Value.EndTime.ToShortTimeString(),
                        //data.Value.Close,
                        ichi5.Tenkan.Current.Value,
                        ichi5.Kijun.Current.Value,
                        ichi5.SenkouA.Current.Value,
                        ichi5.SenkouB.Current.Value,
                        //data.Value.EndTime.ToShortTimeString(),
                        //data.Value.Close,
                        ichi10.Tenkan.Current.Value,
                        ichi10.Kijun.Current.Value,
                        ichi10.SenkouA.Current.Value,
                        ichi10.SenkouB.Current.Value,
                        ""
                        );
                mylog.Debug(logmsg);


                #endregion

            }
        }
        #endregion
    }
}
