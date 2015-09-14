using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Notifications;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.MyAlgorithms
{
    /// <summary>
    /// Algo to test Ehlers Optimal Tracking Indicator
    /// </summary>
    public class OptimalTrackingAlgorithm : QCAlgorithm
    {
        #region "Logging Headers"
        private string transheader = @"Symbol,Quantity,Price,ActionNameUS,TradeDate,SettledDate,Interest,Amount,Commission,Fees,CUSIP,Description,ActionId,TradeNumber,RecordType,TaxLotNumber";
        private string ondataheader = @"Time,CurrentBar,Open,High,Low,Close,Volume,Price,ema,zema,Value3,Value1,Value2,lambda,alpha,priceOptimalDiff,priceOptimalSign,priceOptimalCross,abs_fudge,Instant,Trigger, trend, Price, OptimalTrackingFilter, val3trig, ROC, CyberCycle, RVI, CG, RviTrigger,stochCC1,stochCC2,fishCC,invfishCC,sharesOwned, Portfolio Value";
        private string tradeheader = @"Symbol, Date, Bar,,,,,,,,,,Direction,Qty, Price, Amount, Fees, Holding Cost, holdingProfit, Last Trade Profit, Invested, ROC Sign, nearMin, nearMax";
        #endregion
        // Custom Logging
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        
        private string symbol = "SPY";
        private int barcount = 0;       // Number of bars processed
        #region indicators
        private RollingWindow<IndicatorDataPoint> Price;
        private RollingWindow<IndicatorDataPoint> ema;
        private RollingWindow<IndicatorDataPoint> zema;
        private RollingWindow<IndicatorDataPoint> OptimalValue1;
        private RollingWindow<IndicatorDataPoint> OmtimalValue2;
        private RollingWindow<IndicatorDataPoint> OptimalTrackingFilter;
        private RollingWindow<IndicatorDataPoint> lambda;
        private RollingWindow<IndicatorDataPoint> alpha;
        private RollingWindow<IndicatorDataPoint> priceOptimalDiff;
        private RollingWindow<IndicatorDataPoint> priceOptimalSign;
        private RollingWindow<IndicatorDataPoint> priceOptimalCross;
        private RollingWindow<IndicatorDataPoint> fudge;
        private RollingWindow<IndicatorDataPoint> instantTrend;
        private RollingWindow<IndicatorDataPoint> instantTrendTrigger;
        private RollingWindow<IndicatorDataPoint> cyberCycle;
        private RollingWindow<IndicatorDataPoint> cyberCycleSmooth;
        private RollingWindow<IndicatorDataPoint> centerGravity;
        private RelativeVigorIndex rvi;
        private RollingWindow<IndicatorDataPoint> rviHistory;
        private RollingWindow<IndicatorDataPoint> stochCenterGravityValue1;
        private RollingWindow<IndicatorDataPoint> stochCenterGravityValue2;
        private RollingWindow<IndicatorDataPoint> stochCyberCycleValue1;
        private RollingWindow<IndicatorDataPoint> stochCyberCycleValue2;
        private RollingWindow<IndicatorDataPoint> stochCyberCycleInverseFisher;
        private RollingWindow<IndicatorDataPoint> stochCyberCycleFisher;
        private RollingWindow<IndicatorDataPoint> stochRviHistoryValue1;
        private RollingWindow<IndicatorDataPoint> stochRviHistoryValue2;
        // Sine Wave Indicator
        private RollingWindow<IndicatorDataPoint> sineWave;
        private RollingWindow<IndicatorDataPoint> leadSineWave;
        //private RollingWindow<IndicatorDataPoint> Cycle;
        private RollingWindow<IndicatorDataPoint> I1;
        private RollingWindow<IndicatorDataPoint> Q1;
        //private RollingWindow<IndicatorDataPoint> DeltaPhase;
        private RollingWindow<IndicatorDataPoint> MedianDelta;
        private RollingWindow<IndicatorDataPoint> Value1;
        //private RollingWindow<IndicatorDataPoint> DCPeriod;
        private RollingWindow<IndicatorDataPoint> RealPart;
        private RollingWindow<IndicatorDataPoint> ImagPart;
        //private RollingWindow<IndicatorDataPoint> DCPhase; 


        



        private Maximum maxCyberCycle;
        private Minimum minCyberCycle;
        private RateOfChange ROC;
        #endregion
        private decimal fudgemultiplier = .00015m;
        private decimal staticAlpha = .05m;
        private decimal staticAlpha2 = .2m;
        private decimal estimatedVelocity = .5m;
        private decimal a = .05m;  // used in instantTrend

        private int sharesOwned = 0;
        private bool openForTrading = false;
        //private List<DailyProfitAndLoss> dailyProfitAndLosses;
        private DateTime processingDate;
        private int _ROCSign;

        private int samplesize = 20;
        private decimal _totalFees;
        private decimal _holdingcost;
        private bool _nearMin;
        private bool _nearMax;

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {
            //Initialize dates
            SetStartDate(2015, 6, 15);
            SetEndDate(2015, 6, 15);
            SetCash(25000);

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
            #region "Init indicators"
            Price = new RollingWindow<IndicatorDataPoint>(samplesize);
            ema = new RollingWindow<IndicatorDataPoint>(samplesize);
            zema = new RollingWindow<IndicatorDataPoint>(samplesize);
            OptimalValue1 = new RollingWindow<IndicatorDataPoint>(samplesize);
            OmtimalValue2 = new RollingWindow<IndicatorDataPoint>(samplesize);
            OptimalTrackingFilter = new RollingWindow<IndicatorDataPoint>(samplesize);
            lambda = new RollingWindow<IndicatorDataPoint>(samplesize);
            alpha = new RollingWindow<IndicatorDataPoint>(samplesize);
            priceOptimalDiff = new RollingWindow<IndicatorDataPoint>(samplesize);
            priceOptimalSign = new RollingWindow<IndicatorDataPoint>(samplesize);
            priceOptimalCross = new RollingWindow<IndicatorDataPoint>(samplesize);
            fudge = new RollingWindow<IndicatorDataPoint>(samplesize);
            instantTrend = new RollingWindow<IndicatorDataPoint>(samplesize);
            instantTrendTrigger = new RollingWindow<IndicatorDataPoint>(samplesize);
            cyberCycle = new RollingWindow<IndicatorDataPoint>(samplesize);
            centerGravity = new RollingWindow<IndicatorDataPoint>(samplesize);
            cyberCycleSmooth = new RollingWindow<IndicatorDataPoint>(samplesize);
            rvi = new RelativeVigorIndex(8);
            rviHistory = new RollingWindow<IndicatorDataPoint>(samplesize);

            stochCenterGravityValue1 = new RollingWindow<IndicatorDataPoint>(8);
            stochCenterGravityValue2 = new RollingWindow<IndicatorDataPoint>(8);

            stochCyberCycleValue1 = new RollingWindow<IndicatorDataPoint>(8);
            stochCyberCycleValue2 = new RollingWindow<IndicatorDataPoint>(8);
            stochCyberCycleInverseFisher = new RollingWindow<IndicatorDataPoint>(8);
            stochCyberCycleFisher = new RollingWindow<IndicatorDataPoint>(8);

            stochRviHistoryValue1 = new RollingWindow<IndicatorDataPoint>(8);
            stochRviHistoryValue2 = new RollingWindow<IndicatorDataPoint>(8);

            ROC = new RateOfChange(4);
            maxCyberCycle = new Maximum(8);
            minCyberCycle = new Minimum(8);
            #endregion
            //mylog.Debug(transheader);
            mylog.Debug(ondataheader);
            string msg = "Security,Date,Day Profit,Day Fees, Day Net, Total Profit, Total Fees";
            mylog.Debug(msg);
            mylog.Debug(tradeheader);

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            barcount++;
            processingDate = this.Time;
            var time = this.Time;
            try
            {
                decimal val = ((data[symbol].High - data[symbol].Low) / 2) + data[symbol].Low;
                val = data[symbol].Close;
                Price.Add(idp(time, val));


                UpdateEma(time);
                UpdateOptimalTrackingFilter(time, data[symbol].High, data[symbol].Low);
                UpdateZema(time);
                priceOptimalDiff.Add(idp(time, Price[0].Value - OptimalTrackingFilter[0].Value));
                priceOptimalSign.Add(idp(time, Math.Sign(priceOptimalDiff[0].Value)));
                priceOptimalCross.Add(barcount > 1 ? idp(time, priceOptimalSign[0].Value - priceOptimalSign[1].Value) : idp(time, 0m));
                fudge.Add(idp(time, Math.Abs(priceOptimalDiff[0].Value / Price[0].Value)));

                UpdateInstantTrend(time);
                UpdateCyberCycle(time);
                UpdateCenterGravity(time);
                rvi.Update(data[symbol]);
                rviHistory.Add(rvi.Current);
                ROC.Update(OptimalTrackingFilter[0]);
                _ROCSign = Math.Sign(ROC.Current.Value);
                _nearMax = stochCyberCycleInverseFisher[0].Value > .75m;
                _nearMin = stochCyberCycleInverseFisher[0].Value < .1m;
                decimal trend = (instantTrend[0].Value - instantTrendTrigger[0].Value);
                trend = trend > 0 ? 1 : -1;

                Strategy(data);

                if (barcount > 2)
                {
                    string logmsg =
                        string.Format(
                            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35},{36},{37}",
                            time,
                            barcount,
                            data[symbol].Open,
                            data[symbol].High,
                            data[symbol].Low,
                            data[symbol].Close,
                            data[symbol].Volume,
                            Price[0].Value,
                            ema[0].Value,
                            zema[0].Value,
                            OptimalTrackingFilter[0].Value,
                            OptimalValue1[0].Value,
                            OmtimalValue2[0].Value,
                            lambda[0].Value,
                            alpha[0].Value,
                            priceOptimalDiff[0].Value,
                            priceOptimalSign[0].Value,
                            priceOptimalCross[0].Value,
                            fudge[0].Value,
                            instantTrend[0].Value,
                            instantTrendTrigger[0].Value,
                            trend,
                            Price[0].Value,
                            OptimalTrackingFilter[0].Value,
                            OptimalTrackingFilter[2].Value,
                            ROC.Current.Value,
                            cyberCycle[0].Value,
                            rvi.Current.Value,
                            centerGravity[0].Value,
                            rviHistory[1].Value,
                            stochCyberCycleValue1[0].Value,
                            stochCyberCycleValue2[0].Value,
                            stochCyberCycleFisher[0].Value,
                            stochCyberCycleInverseFisher[0].Value,
                            sharesOwned,
                            Portfolio.TotalPortfolioValue,
                            Portfolio[symbol].UnrealizedProfit,
                            ""
                            );
                    mylog.Debug(logmsg);
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private void Strategy(TradeBars data)
        {
            //return;
            decimal trend = (instantTrend[0].Value - instantTrendTrigger[0].Value);
            decimal trend1 = trend;
            if (instantTrend.Count > 1)
                trend1 = (instantTrend[1].Value - instantTrendTrigger[1].Value);
            
            var truecross = (priceOptimalCross[0].Value != 0) && (fudge[0].Value > fudgemultiplier);
            var changeoftrend = (trend != trend1);
            var signal = truecross && changeoftrend;
            //signal = false;

            openForTrading = true;
            // wait 10 minutes to start trading to avoid opening moves
            if (this.Time.Hour == 9 && this.Time.Minute < 40)
            {
                openForTrading = false;
                return;
            }

            // liquidate at 3:50 to avoid the 4:00 rush.
            if (this.Time.Hour == 15 && this.Time.Minute > 49)
            {
                openForTrading = false;
                if (Portfolio.Invested)
                {
                    Liquidate();
                }
            }
            if (openForTrading)
            {
                if (Portfolio[symbol].Invested)
                {
                    if (signal && trend > 0 && Portfolio[symbol].IsShort && _nearMin) // trend is up  && _nearMin
                    {
                        Buy(symbol, Portfolio[symbol].AbsoluteQuantity * 2);
                    }
                    if (signal && trend < 0 && Portfolio[symbol].IsLong && _nearMax) // trend is dn && _nearMax
                    {
                        Sell(symbol, Portfolio[symbol].AbsoluteQuantity * 2);
                    }

                }
                else
                {
                    if (signal && trend > 0 && _nearMin) // trend is up
                    {
                        Buy(symbol, Portfolio.Cash / Convert.ToInt32(Price[0].Value + 1));
                    }
                    if (signal && trend < 0 && _nearMax) // trend is dn
                    {
                        Sell(symbol, Portfolio.Cash / Convert.ToInt32(Price[0].Value + 1));
                    }
                }
            }
            sharesOwned = Portfolio[symbol].Quantity;
        }

        #region UpdateIndicators
        private void UpdateEma(DateTime time)
        {
            if (barcount > 1)
            {
                decimal emaval = staticAlpha * Price[0].Value + (1 - staticAlpha) * ema[0].Value;
                ema.Add(idp(time, emaval));
            }
            else
            {
                ema.Add(idp(time, Price[0].Value));
            }
        }


        private void UpdateOptimalTrackingFilter(DateTime time, decimal high, decimal low)
        {
            if (barcount > 1)
            {
                decimal v1 = staticAlpha2 * (Price[0].Value - Price[1].Value) + (1 - staticAlpha2) * OptimalValue1[0].Value;
                OptimalValue1.Add(idp(time, v1));
                decimal v2 = .1m * (high - low) + (1 - staticAlpha2) * OmtimalValue2[0].Value;
                OmtimalValue2.Add(idp(time, v2));
            }
            else
            {

                OptimalValue1.Add(idp(time, Price[0].Value));
                OmtimalValue2.Add(idp(time, Price[0].Value));
            }
            lambda.Add(OmtimalValue2[0].Value != 0 ? idp(time, Math.Abs(OptimalValue1[0].Value / OmtimalValue2[0].Value)) : idp(time, 0m));
            double la = System.Convert.ToDouble(lambda[0].Value);
            double alphaval = (la * -1 * la + Math.Sqrt(la * la * la * la + 16 * la * la)) / 8;
            alpha.Add(idp(time, System.Convert.ToDecimal(alphaval)));
            if (barcount > 4)
            {
                decimal v3 = alpha[0].Value * Price[0].Value + (1 - alpha[0].Value) * OptimalTrackingFilter[0].Value;
                OptimalTrackingFilter.Add(idp(time, v3));
            }
            else
            {
                OptimalTrackingFilter.Add(idp(time, Price[0].Value));
            }
        }

        private void UpdateZema(DateTime time)
        {
            if (barcount > 4)
            {
                decimal zemaval = staticAlpha * (Price[0].Value + estimatedVelocity * (Price[0].Value - Price[4].Value)) +
                                  (1 - staticAlpha) * zema[0].Value;
                zema.Add(idp(time, zemaval));
            }
            else
            {
                zema.Add(idp(time, ema[0].Value));
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
                instantTrendTrigger.Add(idp(time, instantTrend[2].Value));
            }
            else
            {
                instantTrend.Add(idp(time, Price[0].Value));
                instantTrendTrigger.Add(idp(time, Price[0].Value));
            }

        }
        /// <summary>
        ///  The Cycle is a High Pass Filter.  We are going to cyberCycleSmooth the price so that the lag is 1.5 bars
        /// to eliminate two and 3 bar cyberCycle components, and to remove some noise.
        /// </summary>
        /// <param name="time"></param>
        private void UpdateCyberCycle(DateTime time)
        {
            if (barcount > 2)
            {
                cyberCycleSmooth.Add(idp(time, (OptimalTrackingFilter[0].Value + 2 * OptimalTrackingFilter[1].Value + OptimalTrackingFilter[2].Value / 6)));

                if (barcount < 7)
                {
                    cyberCycle.Add(idp(time, (OptimalTrackingFilter[0].Value - 2 * OptimalTrackingFilter[1].Value + OptimalTrackingFilter[2].Value) / 4));
                }
                else
                {
                    // From Ehlers page 15 equation 2.7
                    var hfp = (1 - a / 2) * (1 - a / 2) * (cyberCycleSmooth[0].Value - 2 * cyberCycleSmooth[1].Value + cyberCycleSmooth[2].Value)
                             + 2 * (1 - a) * cyberCycle[0].Value - (1 - a) * (1 - a) * cyberCycle[1].Value;
                    cyberCycle.Add(idp(time, hfp));
                }
            }
            else
            {
                cyberCycleSmooth.Add(idp(time, OptimalTrackingFilter[0].Value));
                cyberCycle.Add(idp(time, 0));
            }
            var x = cyberCycle[0];
            // Update the stoch version
            maxCyberCycle.Update(cyberCycle[0]);
            minCyberCycle.Update(cyberCycle[0]);
            //decimal maxCycle = GetMaxOf(stochCyberCycleValue1);
            //decimal minCycle = GetMinOf(stochCyberCycleValue1);
            decimal sccvalue1 = 0;

            if ((maxCyberCycle.Current.Value != minCyberCycle.Current.Value))
                sccvalue1 = (cyberCycle[0].Value - minCyberCycle.Current.Value) / (maxCyberCycle.Current.Value - minCyberCycle.Current.Value);


            stochCyberCycleValue1.Add(idp(time, sccvalue1));
            if (barcount > 4)
            {
                var value2 = (4 * stochCyberCycleValue1[0].Value
                            + 3 * stochCyberCycleValue1[1].Value
                            + 2 * stochCyberCycleValue1[2].Value
                            + stochCyberCycleValue1[3].Value);

                stochCyberCycleValue2[0].Value = 2 * (value2 - .5m);

                // v2 is the double version of value2 for use in the fishers
                double v2 = System.Convert.ToDouble(value2);
                // limit the new OptimalValue1 so that it falls within positive or negative unity
                if (v2 > .9999)
                    v2 = .9999;
                if (v2 < -.9999)
                    v2 = -.9999;


                // Inverse Fisherized value2
                // From inverse fisher
                //(Math.Exp(2*(double) OptimalValue1[0].Value) - 1)/(Math.Exp(2*(double) OptimalValue1[0].Value) + 1);
                double v3 = (Math.Exp(2 * v2) - 1) / (Math.Exp(2 * v2) + 1);
                decimal v5 = System.Convert.ToDecimal(v3);
                stochCyberCycleInverseFisher.Add(idp(time, v5));
                if (barcount == 25)
                    System.Diagnostics.Debug.WriteLine("here");


                // Fisherized value2
                // From Fisher
                //Convert.ToDecimal(.5* Math.Log((1.0 + (double) OptimalValue1[0].Value)/(1.0 - (double) OptimalValue1[0].Value)));
                decimal v6 = Convert.ToDecimal(.5 * Math.Log((1.0 + v2) / (1.0 - v2)));

                //double v4 = .5 * Math.Log((1 + 1.98 * (v2 - .5) / 1 - 1.98 * (v2 - .5)));
                //decimal v6 = System.Convert.ToDecimal(v4);
                stochCyberCycleFisher.Add(idp(time, v6));



            }
            else
            {
                stochCyberCycleValue2.Add(idp(time, 0m));
                stochCyberCycleFisher.Add(idp(time, 0));
                stochCyberCycleInverseFisher.Add(idp(time, 0));
            }


        }
        /// <summary>
        /// Updates the CenterGravity 
        /// </summary>
        /// <param name="time"></param>
        private void UpdateCenterGravity(DateTime time)
        {
            decimal num = 0;
            decimal den = 0;
            int count = samplesize / 2;

            if (barcount > count)
            {
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        num += (1 + i) * (Price[i].Value);
                        den += Price[i].Value;
                    }
                    catch (Exception e)
                    {
                        throw new Exception(e.Message);
                    }
                }
                if (den != 0)
                {
                    decimal c = count + 1;
                    decimal u = System.Convert.ToDecimal(-num / den);
                    centerGravity.Add(idp(time, u + c / 2));
                }
            }
            else
            {
                centerGravity.Add(idp(time, 0));
            }

        }

        //private void UpdateSineWave(DateTime time)
        //{
        //    // Smooth is already calculated
        //    Cycle.Add(idp(time, cyberCycle[0]));
        //    if (barcount < 7)

            
        //}
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        public override void OnEndOfDay(string symbol)
        {
            base.OnEndOfDay();
            decimal lastprofit = 0;
            decimal lastfees = 0;
            
            foreach (SecurityHolding holding in Portfolio.Values)
            {
            }
            //minRvi.Reset();
            //minSignal.Reset();
            //maxRvi.Reset();
            //maxSignal.Reset();
            //barcount = 0;

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderEvent"></param>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            decimal fees = 0m;
            decimal lastTradeProfit = 0m;
            decimal holdingProfit = 0m;
            base.OnOrderEvent(orderEvent);
            if (orderEvent.Status == OrderStatus.Filled)
            {
                var fillprice = orderEvent.FillPrice;
                var fillquantity = orderEvent.FillQuantity;
                var amount = orderEvent.FillPrice * orderEvent.FillQuantity;
                OrderDirection direction = orderEvent.Direction;
                var orderid = Portfolio.Transactions.LastOrderId;



                var fpbuy = "";
                var fpsell = "";
                if (direction == OrderDirection.Buy)
                    fpbuy = fillprice.ToString(CultureInfo.InvariantCulture);
                else
                    fpsell = fillprice.ToString(CultureInfo.InvariantCulture);

                foreach (SecurityHolding holding in Portfolio.Values)
                {
                    fees = holding.TotalFees - _totalFees;
                    lastTradeProfit = holding.LastTradeProfit;
                    holdingProfit = holding.Profit;
                    _totalFees = holding.TotalFees;
                    _holdingcost = holding.HoldingsCost;
                }
                var order = Transactions.GetOrderById(orderEvent.OrderId);
                var dt = order.Time;
                var quantity = Portfolio[symbol].Quantity;


                if (direction.ToString() == "Buy")
                {
                    amount += fees;
                }
                else
                {
                    amount += fees;

                }
                #region Scottrade
                //string transmsg = string.Format(
                //        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                //        symbol,
                //        fillquantity,
                //        fillprice,
                //        direction.ToString(),
                //        dt,
                //        dt.AddDays(4),
                //        0,
                //        amount,
                //        fees,
                //        0,
                //        "60505104",
                //        actionNameUs + " share of " + _symbol + "at $" + fillprice.ToString(),
                //        actionid,
                //        orderEvent.OrderId,
                //        "Trade",
                //        "taxlot"
                //        );
                //mylog.Debug(transmsg);
                #endregion
                string logmsg =
                        string.Format(
                            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24}",
                            symbol,
                            dt,
                            barcount,
                            "",
                            "",
                            "",
                            "",
                            "",
                            "",
                            "",
                            "",
                            "",
                            direction.ToString(),
                            fillquantity,
                            fillprice,
                            amount,
                            fees,
                            _holdingcost,
                            holdingProfit,
                            lastTradeProfit,
                            Portfolio.Invested,
                            _ROCSign,
                            _nearMin,
                            _nearMax,
                            ""
                            );
                    mylog.Debug(logmsg);
           

            }
        }
    }
}
