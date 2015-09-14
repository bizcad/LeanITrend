using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect
{
    /// <summary>
    /// 
    /// </summary>
    public class RVIAlgorithm : QCAlgorithm
    {
        private string _symbol = "SPY";
        //string _customSymbol = "BTC";

        //private string transheader = @"Symbol,Quantity,Price,ActionNameUS,TradeDate,SettledDate,Interest,Amount,Commission,Fees,CUSIP,Description,ActionId,TradeNumber,RecordType,TaxLotNumber";

        private string ondataheader =
            @"Time,CurrentBar,Open,High,Low,Close,MacD,Signal,Fish,IFish,unrealized";

        private Boolean logtrade = false;

        private string tradeheader = @"Time,,,,,,,,,,,Direction,TradeProfit,Buy Price,Sell Price,Profit,HoldingCost,FillQty,Fees,TransAmt,Qty Held";

        // for computing a sine wave
        //private double t = 0;
        //private double Vp = 1;
        //private double fo = 100;
        //private double Phase = 0;
        //private double Vdc = 0;
        //private double deltat = 0.0001;
        //private decimal lag = 9;


        //private decimal alpha = .07m;
        private int samplesize = 8;
        //private decimal medianDelta;
        //private decimal Dc;
        //private decimal dcphase;
        private int barcount = 0;
        private decimal _totalFees;
        private decimal holdingcost;
        //private decimal num;
        //private decimal denom;

        private decimal lastprofit;
        private decimal lastfees;
        private decimal dayprofit;
        private decimal dayfees;
        private decimal daynet;

        private AlgoIndicators _indicators;
        //private RollingWindow<IndicatorDataPoint> open;
        //private RollingWindow<IndicatorDataPoint> close;
        //private RollingWindow<IndicatorDataPoint> high;
        //private RollingWindow<IndicatorDataPoint> low;
        //private RollingWindow<IndicatorDataPoint> i1;
        //private RollingWindow<IndicatorDataPoint> instperiod;
        //private RollingWindow<IndicatorDataPoint> v2;
        //private RollingWindow<IndicatorDataPoint> v1;
        private RollingWindow<IndicatorDataPoint> unrealized;
        private RollingWindow<IndicatorDataPoint> RsiHistory;
        private RollingWindow<IndicatorDataPoint> iFishes;
        //private RollingWindow<IndicatorDataPoint> Signal;
        //private Minimum minRvi;
        //private Maximum maxRvi;
        //private Minimum minSignal;
        //private Maximum maxSignal;
        private FisherTransform fish;
        private InverseFisherTransform ifish;
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private RelativeVigorIndex Rvi;

        private decimal tradefish = 0;
        private int periodwait = 0;
        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {

            //            mylog.Debug(",CurrentBar,Time,Price,smooth,low,i1,cycle0,cycle1,cycle2,fish,medianDelta,DC,instaperiod, v2,DCPeriod,realpart,imagpart,dcphase");
            mylog.Debug(ondataheader);
            //mylog.Debug(",Time,CurrentBar,Direction,TradeProfit,,Price,Profit,HoldingCost,FillQty,Fees,TransAmt");
            //mylog.Debug(transheader);
            //Initialize
            SetStartDate(2015, 05, 12);
            SetEndDate(2015, 05, 12);
            SetCash(25000);

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, _symbol, Resolution.Minute);

            _indicators = new AlgoIndicators
            {
                //BB = BB(_symbol, 20, 1, MovingAverageType.Simple, Resolution.Daily),
                RSI = RSI(_symbol, 14, MovingAverageType.Simple, Resolution.Daily),
                //ATR = ATR(_symbol, 14, MovingAverageType.Simple, Resolution.Daily),
                //EMA = EMA(_symbol, 14, Resolution.Daily),
                //SMA = SMA(_symbol, 14, Resolution.Daily),
                MACD = MACD(_symbol, 12, 26, 9, MovingAverageType.Simple, Resolution.Minute)
                //AROON = AROON(_symbol, 20, Resolution.Daily),
                //MOM = MOM(_symbol, 20, Resolution.Daily),
                //MOMP = MOMP(_symbol, 20, Resolution.Daily),
                //STD = STD(_symbol, 20, Resolution.Daily),
                //MIN = MIN(_symbol, 14, Resolution.Daily), // by default if the symbol is a tradebar type then it will be the min of the low property
                //MAX = MAX(_symbol, 14, Resolution.Daily),  // by default if the symbol is a tradebar type then it will be the max of the high property

                //open = new WindowIndicator<IndicatorDataPoint>(4)
                //Ft = FT(_symbol, samplesize, Resolution.Minute),
                //Rvi = RVI(_symbol, samplesize, Resolution.Minute)
                

            };
            //open = new RollingWindow<IndicatorDataPoint>(samplesize);
            //close = new RollingWindow<IndicatorDataPoint>(samplesize);
            //high = new RollingWindow<IndicatorDataPoint>(samplesize);
            //low = new RollingWindow<IndicatorDataPoint>(samplesize);
            //i1 = new RollingWindow<IndicatorDataPoint>(samplesize);
            //instperiod = new RollingWindow<IndicatorDataPoint>(samplesize);
            //v2 = new RollingWindow<IndicatorDataPoint>(samplesize);
            //v1 = new RollingWindow<IndicatorDataPoint>(samplesize);
            //Rvi = new RollingWindow<IndicatorDataPoint>(samplesize);
            unrealized = new RollingWindow<IndicatorDataPoint>(samplesize);
            iFishes = new RollingWindow<IndicatorDataPoint>(samplesize);
            RsiHistory = new RollingWindow<IndicatorDataPoint>(samplesize);
            //Signal = new RollingWindow<IndicatorDataPoint>(samplesize);
            //maxRvi = new Maximum("RVI_Max", samplesize);
            //minRvi = new Minimum("RVi_Min", samplesize);
            //maxSignal = new Maximum("Sig_Max", samplesize);
            //minSignal = new Minimum("Sig_Min", samplesize);
            fish = new FisherTransform(samplesize);
            ifish = new InverseFisherTransform(samplesize);
            //Crossing = new RollingWindow<IndicatorDataPoint>(samplesize);
            //FisherHistory = new RollingWindow<IndicatorDataPoint>(samplesize);
            Rvi = new RelativeVigorIndex(_symbol, samplesize);

            for (int x = 0; x < samplesize; x++)
            {
                //open.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //close.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //high.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //low.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //i1.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //instperiod.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //v1.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //v2.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //Rvi.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                unrealized.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                iFishes.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //Signal.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
                //Crossing.Add(new IndicatorDataPoint(DateTime.MinValue, .0001m));

            }
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            barcount++;
            //if (!_indicators.MACD.IsReady)
            //    return;
            //decimal dp;
            try
            {
                // wait 10 minutes to start trading to avoid opening moves
                if (this.Time.Hour == 9 && this.Time.Minute < 40)
                    return;

                RsiHistory.Add(new IndicatorDataPoint(this.Time, _indicators.RSI.Current));
                fish.Update(new IndicatorDataPoint(this.Time, _indicators.MACD.Current));
                ifish.Update(new IndicatorDataPoint(this.Time, _indicators.MACD.Current));
                iFishes.Add(ifish.Current);

                unrealized.Add(new IndicatorDataPoint(this.Time, Portfolio.TotalUnrealizedProfit));
                decimal open = _indicators.Rvi.Bars[0].Open;
                decimal high = _indicators.Rvi.Bars[0].High;
                decimal low = _indicators.Rvi.Bars[0].Low;
                decimal close = _indicators.Rvi.Bars[0].Close;

                string ondatamsg =
                    string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}",
                        this.Time,
                        barcount,
                        open,
                        high,
                        low,
                        close,
                        _indicators.MACD.Current.Value,
                        _indicators.MACD.Signal,
                    //_indicators.Rvi.RviWindow[0].Value,
                    //_indicators.Rvi.RviWindow[1].Value,
                        fish.Current.Value,
                        ifish.Current.Value,
                        unrealized[0].Value,
                    //Portfolio.Invested,
                    //Portfolio[_symbol].IsLong,
                    //Portfolio[_symbol].IsShort
                    //"",
                        "",
                        "",
                        "",
                        "",
                        ""

                        );
                mylog.Debug(ondatamsg);


            }
            catch (Exception ex)
            {
                Error(ex.Message);
            }

            // liquidate at 3:50 to avoid the 4:00 rush.
            if (this.Time.Hour == 15 && this.Time.Minute > 49)
            {
                if (Portfolio.Invested)
                {
                    Liquidate();
                }


            }

            //Strategy();
        }

        private void Strategy()
        {
            if (barcount == 63)
                System.Threading.Thread.Sleep(100);

            if (ifish.IsReady)
            {
                if (ifish > 0m)
                {
                    if (Portfolio.Invested)
                    {
                        // 3 losses
                        if (unrealized[0].Value < 0 && unrealized[1].Value < 0 && unrealized[2].Value < 0)
                        {
                            if (Portfolio[_symbol].IsLong)
                                SetHoldings(_symbol, -1);
                            else
                                SetHoldings(_symbol, 1);
                        }
                        if (unrealized[0].Value > 50m)
                        {
                            if ((unrealized[0].Value < unrealized[1].Value) &&
                                (unrealized[1].Value > unrealized[2].Value))
                            {
                                Liquidate();
                                periodwait = 4;
                            }
                        }
                    }
                }

                if (ifish < 0m)
                {
                    if (Portfolio.Invested)
                    {
                        // 3 losses in a row
                        if (unrealized[0].Value < 0 && unrealized[1].Value < 0 && unrealized[2].Value < 0)
                        {
                            if (Portfolio[_symbol].IsShort)
                                SetHoldings(_symbol, 1);
                            else
                                SetHoldings(_symbol, -1); // go 
                        }
                        if (unrealized[0].Value > 50m)
                        {
                            // look for a profit peak
                            if ((unrealized[0].Value < unrealized[1].Value) &&
                                (unrealized[1].Value > unrealized[2].Value))
                            {
                                Liquidate();
                                periodwait = 4;
                            }
                        }
                    }
                }


                if (ifish > 0.5m)
                {
                    if (!Portfolio.Invested && periodwait-- <= 0)
                    {
                        // look for a Inverse Fisher peak
                        if ((iFishes[0].Value < iFishes[1].Value) && (iFishes[1].Value > iFishes[2].Value))
                        {
                            SetHoldings(_symbol, -1); // go 
                        }
                    }
                }
                if (ifish < -0.5m)
                {
                    if (!Portfolio.Invested && periodwait-- <= 0)
                    {
                        // look for a valley
                        if ((iFishes[0].Value > iFishes[1].Value) && (iFishes[1].Value < iFishes[2].Value))
                        {
                            SetHoldings(_symbol, 1); // go long
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderEvent"></param>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            decimal fees = 0m;
            decimal tradeprofit = 0m;
            decimal profit = 0m;
            base.OnOrderEvent(orderEvent);
            if (orderEvent.Status == OrderStatus.Filled)
            {
                var fillprice = orderEvent.FillPrice;
                var fillquantity = orderEvent.FillQuantity;
                var amount = orderEvent.FillPrice * orderEvent.FillQuantity;
                var direction = orderEvent.Direction;
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
                    tradeprofit = holding.LastTradeProfit;
                    profit = holding.Profit;
                    _totalFees = holding.TotalFees;
                    holdingcost = holding.HoldingsCost;
                }
                var order = Transactions.GetOrderById(orderEvent.OrderId);
                var dt = order.Time;
                var quantity = Portfolio[_symbol].Quantity;


                if (direction.ToString() == "Buy")
                {
                    amount += fees;
                }
                else
                {
                    amount += fees;

                }

                //string transmsg = string.Format(
                //        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                //        _symbol,
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
                if (logtrade)
                {

                    mylog.Debug(tradeheader);

                    string logmsg =
                        string.Format(
                            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}",
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
                            direction,
                            tradeprofit,
                            fpbuy,
                            fpsell,
                            profit,
                            holdingcost,
                            fillquantity,
                            fees,
                            profit - fees,
                            quantity
                            );
                    mylog.Debug(logmsg);
                }

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        public override void OnEndOfDay(string symbol)
        {
            base.OnEndOfDay();
            foreach (SecurityHolding holding in Portfolio.Values)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("{0},{1},{2},{3},{4}", lastprofit, lastfees,
                    holding.Profit, holding.TotalFees, holding.Profit - holding.TotalFees));
                dayprofit = holding.Profit - lastprofit;
                dayfees = holding.TotalFees - lastfees;
                daynet = dayprofit - dayfees;
                lastprofit = holding.Profit;
                lastfees = holding.TotalFees;
                string msg = "Security,Profit,Fees,NetForDay";
                mylog.Debug(msg);
                msg = string.Format("{0},{1},{2},{3}", holding.Symbol, dayprofit, dayfees, daynet);
                mylog.Debug(msg);

            }
            //minRvi.Reset();
            //minSignal.Reset();
            //maxRvi.Reset();
            //maxSignal.Reset();
            barcount = 0;

        }

        /// <summary>
        /// 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            base.OnEndOfAlgorithm();
            foreach (SecurityHolding holding in Portfolio.Values)
            {
                string msg = ",Security,Profit,Fees,NetForAlog";
                mylog.Debug(msg);
                msg = string.Format(",{0},{1},{2},{3}", holding.Symbol, holding.Profit, holding.TotalFees,
                    holding.Profit - holding.TotalFees);
                mylog.Debug(msg);
            }
        }

        private double RadToDeg(double p)
        {
            var tmp = p * (Math.PI / 180);
            return tmp;
        }

        private double DegToRad(double i)
        {
            var tmp = i * (180 / Math.PI);
            return tmp;
        }

        private double sinewave(double t, double Vp, double fo, double Phase, double Vdc)
        {
            var pi = Math.PI;
            return Vp * Math.Sin(2 * pi * fo * t + Phase * pi / 180) + Vdc;

        }

        /// <summary>
        /// 
        /// </summary>
        public class AlgoIndicators
        {
            //public BollingerBands BB;
            //public SimpleMovingAverage SMA;
            //public ExponentialMovingAverage EMA;
            /// <summary>
            /// An RSI
            /// </summary>
            public RelativeStrengthIndex RSI;
            //public AverageTrueRange ATR;
            //public StandardDeviation STD;
            //public AroonOscillator AROON;
            //public Momentum MOM;
            //public MomentumPercent MOMP;
            /// <summary>
            /// A MACD
            /// </summary>
            public MovingAverageConvergenceDivergence MACD;
            //public Minimum MIN;
            //public Maximum MAX;

            //public RollingWindow<IndicatorDataPoint> open;

            //public FisherTransform Ft;
            //public InverseFisherTransform Ift;

            /// <summary>
            /// The Relative Vigor Index
            /// </summary>
            public RelativeVigorIndex Rvi;
        }

    }


}
