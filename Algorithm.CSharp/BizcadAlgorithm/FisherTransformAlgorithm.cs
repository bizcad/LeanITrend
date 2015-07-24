using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Notifications;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Examples
{
    /// <summary>
    /// A raw test of the Fisher Transform
    /// </summary>
    public class FisherTransformAlgorithm : QCAlgorithm
    {

        private string _symbol = "SPY";

        // Logging Headers
        //private string transheader = @"Symbol,Quantity,Price,ActionNameUS,TradeDate,SettledDate,Interest,Amount,Commission,Fees,CUSIP,Description,ActionId,TradeNumber,RecordType,TaxLotNumber";
        private string ondataheader = @"Time,CurrentBar,Open,High,Low,Close,MinL,MaxH,fish0,fish1,Signal,Fisher,unrealized,Direction,LastProfit,Total Fees,Total Close Profit";
        private string tradeheader = @"Time,CurrentBar,Open,High,Low,Close,MinL,MaxH,fish0,fish1,Signal,Fisher,unrealized,Direction,LastProfit,Total Fees,Total Close Profit,Unrealized,Direction,tradeprofit,Buy Price,Sell Price,Profit,HoldingCost,FillQty,Fees,TransAmt,Qty Held";


        private int barcount = 0;       // Number of bars processed
        private int _period = 10;        // number of bars in a RollingWindow
        private decimal lastTransactionFish = 0;

        // Order Event
        private decimal holdingcost;

        //Day summary
        private decimal lastprofit;
        private decimal lastfees;
        private decimal dayprofit;
        private decimal dayfees;
        private decimal daynet;
        private decimal _totalFees;
        //private decimal _lastUnrealized;

        // Custom Logging
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");

        private Minimum minLow;
        private Maximum maxHigh;
        private RollingWindow<IndicatorDataPoint> value1;
        private RollingWindow<IndicatorDataPoint> fish;
        //private LinearWeightedMovingAverage wma;
        //private RollingWindow<IndicatorDataPoint> wwma;
        //private Minimum fishLow;
        //private Maximum fishHigh;
        private FisherTransform fx;

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
            SetStartDate(2013, 10, 07);
            SetEndDate(2013, 10, 07);
            SetCash(25000);

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, _symbol, Resolution.Minute);


            maxHigh = new Maximum("MaxHigh", _period);
            minLow = new Minimum("MinLow", _period);
            value1 = new RollingWindow<IndicatorDataPoint>(_period);
            fish = new RollingWindow<IndicatorDataPoint>(_period);
            //wma = new LinearWeightedMovingAverage(5);       // induces 2 bar lag
            //wwma = new RollingWindow<IndicatorDataPoint>(_period);
            //fishHigh = new Maximum("FishHigh", 400);
            //fishLow = new Minimum("FishLow", 400);
            fx = new FisherTransform(_symbol,_period);
            //fx = FT(_symbol, _period, Resolution.Minute);

            // Add a bars to initialize the RollingWindow
            value1.Add(new IndicatorDataPoint(DateTime.MinValue, .0001m));
            value1.Add(new IndicatorDataPoint(DateTime.MinValue, .0001m));
            fish.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
            fish.Add(new IndicatorDataPoint(DateTime.MinValue, 0m));
            //wwma.Add(new IndicatorDataPoint(DateTime.MinValue, .0001m));
            //wwma.Add(new IndicatorDataPoint(DateTime.MinValue, .0001m));

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            barcount++;
            //decimal dp;
            try
            {

                maxHigh.Update(new IndicatorDataPoint(data.Time, data[_symbol].Close));
                minLow.Update(new IndicatorDataPoint(data.Time, data[_symbol].Close));
                fx.Update(new IndicatorDataPoint(data.Time, data[_symbol].Close));
                if (fx.IsReady)
                {

                    var Price = data[_symbol].Close;
                    var MinL = minLow.Current.Value;
                    var MaxH = maxHigh.Current.Value;


                    var v0 = value1[0].Value;
                    value1.Add(new IndicatorDataPoint(data.Time, .33m * 2m * ((Price - MinL) / (MaxH - MinL) - .5m) + .67m * v0));

                    if (value1[0].Value > .9999m) value1[0].Value = .9999m;
                    if (value1[0].Value < -.9999m) value1[0].Value = -.9999m;
                    var fish0 = fish[0];
                    var fish1 =
                        System.Convert.ToDecimal(.5 * 2.0 *
                                                 Math.Log((1.0 + (double)value1[0].Value) /
                                                          (1.0 - (double)value1[0].Value))) + .5m * fish0.Value;
                    fish.Add(new IndicatorDataPoint(data.Time, fish1));
                    //wma.Update(fish[0]);
                    //wwma.Add(new IndicatorDataPoint(data.Time, wma.Current));
                    //fishHigh.Update(fish[0]);
                    //fishLow.Update(fish[0]);

                    var fishdirval = fish[1].Value - fish[0].Value;
                    var fishdirection = fishdirval > 0m ? "Up" : "Down";
                    var signal = CalculateSignal();

                    string logmsg =
                        string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17}",
                            data.Time,
                            barcount,
                            data[_symbol].Open,
                            data[_symbol].High,
                            data[_symbol].Low,
                            data[_symbol].Close,
                            MinL,
                            MaxH,
                            fish[0].Value,
                            fish[1].Value,
                            signal,
                            fx.Current.Value,
                            Portfolio[_symbol].UnrealizedProfit,
                            fishdirection,
                            Portfolio[_symbol].LastTradeProfit,
                            Portfolio[_symbol].TotalFees,
                            Portfolio[_symbol].TotalCloseProfit(),
                            ""
                            );
                    mylog.Debug(logmsg);

                    /* 
                    Here is a way to generate trading signals using the fisher transform indicator:
                    A bullish signal is generated when the fisher line turns up below -1 threshold and crosses above the signal line.
                    A bearish signal is generated when the fisher line turns down above the 1 threshold and crosses below the signal line.

                    Read more: http://www.quantshare.com/item-528-fisher-transform-technical-indicator#ixzz3ZV8fR0Dw 
                    Follow us: @quantshare on Twitter
                    */

                    if (signal != 0)
                    {
                        if (Math.Abs(lastTransactionFish - fish[0].Value) > 1)
                        {
                            SetHoldings(_symbol, 0.5*signal);
                            lastTransactionFish = fish[0].Value;
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Error(ex.Message);
            }
        }


        private int CalculateSignal()
        {
            return FisherCrosses();
            //if (!Portfolio[_symbol].Invested)
            //{
            //    // If a bottom go long
            //    if ((fish[0].Value > fishLow.Current.Value) && (fish[0].Value < -4))
            //    {
            //        return 1;
            //    }
            //    // if a top sell short
            //    if ((fish[0].Value < fishHigh.Current.Value) && (fish[0].Value > 4))
            //    {
            //        return -1;
            //    }
            //}
            //else
            //{

            //    if (Portfolio[_symbol].IsLong)
            //    {
            //        if (fish[0].Value > 4)
            //        {
            //            Liquidate();
            //        }
            //    }
            //    else
            //    {
            //        // if the current wwma is higher that any of the last 5 periods
            //        if (fish[0].Value < -4)
            //        {
            //            Liquidate();
            //        }
            //    }
            //}
            //return 0;  // otherwise hold
        }

        private int FisherCrosses()
        {
            if (((fish[0].Value > fish[1].Value) && (fish[1].Value > fish[2].Value)) ||
                ((fish[0].Value < fish[1].Value) && (fish[1].Value < fish[2].Value)))
            {
                return 0;
            }
            else
            {
                if (fish[0].Value > fish[1].Value)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
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

                    fpbuy = fillprice.ToString();
                else
                    fpsell = fillprice.ToString();

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

                string actionNameUs;
                int actionid;

                if (direction.ToString() == "Buy")
                {
                    amount += fees;
                    actionNameUs = direction.ToString();
                    actionid = 1;
                }
                else
                {
                    amount += fees;
                    actionNameUs = direction.ToString();
                    actionid = 13;


                }


                mylog.Debug(tradeheader);

                string logmsg =
                    string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27}",
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
                        "",
                        "",
                        "",
                        "",
                        "",
                        "",
                        Portfolio.TotalUnrealizedProfit,
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

                string transmsg = string.Format(
                        "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15}",
                        _symbol,
                        fillquantity,
                        fillprice,
                        direction.ToString(),
                        dt,
                        dt.AddDays(4),
                        0,
                        amount,
                        fees,
                        0,
                        "60505104",
                        actionNameUs + " share of " + _symbol + "at $" + fillprice.ToString(),
                        actionid,
                        orderEvent.OrderId,
                        "Trade",
                        "taxlot"
                        );
                // mylog.Debug(transmsg);

            }
        }

        /// <summary>
        /// End of a trading day event handler. This method is called at the end of the algorithm day (or multiple times if trading multiple assets).
        /// </summary>
        /// <param name="symbol">Asset symbol for this end of day event. Forex and equities have different closing hours.</param>
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
                string msg = ",Security,Profit,Fees,NetForDay";
                //mylog.Debug(msg);
                msg = string.Format(",{0},{1},{2},{3}", holding.Symbol, dayprofit, dayfees, daynet);
                //mylog.Debug(msg);

            }

            barcount = 0;

        }

        /// <summary>
        /// End of algorithm run event handler. This method is called at the end of a backtest or live trading operation. Intended for closing out logs.
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

    }
}
