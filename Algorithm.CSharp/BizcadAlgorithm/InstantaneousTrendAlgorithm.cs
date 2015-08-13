using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography.X509Certificates;
//using MathNet.Numerics.RootFinding;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Examples
{
    class InstantaneousTrendAlgorithm : QCAlgorithm
    {
        private DateTime _startDate = new DateTime(2015, 7, 14);
        private DateTime _endDate = new DateTime(2015, 7, 15);
        private decimal _portfolioAmount = 22000;
        private decimal _transactionSize = 22000;

        private string symbol = "AAPL";

        // Custom Logging
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");

        private string ondataheader = @"Time,BarCount,trade size,Open,High,Low,Close,Time,Price,Trend,Trigger,ZeroLag,SMA20, iFishTrend,CyberCycle,iFishCyberCycle,maximum, minimum,pricePassedMax,pricePassedMin,ROC,RSI High,RSI Low, iFishRSIHigh, iFishRSILow, direction,slope, comment, Entry Price, Exit Price,orderId , unrealized, shares owned,trade profit, trade fees, trade net,last trade fees, profit, fees, net, day profit, day fees, day net, Portfolio Value";
        private string dailyheader = @"Trading Date,Daily Profit, Daily Fees, Daily Net, Cum profit, Cum Fees, Cum Net, Trades/day, Portfolio Value, Shares Owned";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private int barcount = 0;
        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;
        private RollingWindow<IndicatorDataPoint> trendTrigger;
        private RateOfChangePercent rocp;
        private RelativeStrengthIndex rsiHigh;
        private RelativeStrengthIndex rsiLow;
        public Maximum maximum;
        public Minimum minimum;
        private CyberCycle cyberCycle;
        private InverseFisherTransform iFishCyberCycle;
        private RollingWindow<IndicatorDataPoint> iFishCyberCycleHistory;
        private InverseFisherTransform iFishRsiHigh;
        private RollingWindow<IndicatorDataPoint> iFishRsiHighHistory;
        private InverseFisherTransform iFishRsiLow;
        private RollingWindow<IndicatorDataPoint> iFishRsiLowHistory;
        private InverseFisherTransform iFishTrend;
        private RollingWindow<IndicatorDataPoint> iFishTrendHistory;
        public SimpleMovingAverage sma20;


        private TradeBar dailyOpen;
        private decimal dailyDirection = 0;
        private Boolean pricePassedAMaximum = false;
        private Boolean pricePassedAMinimum = false;
        private SortedList<DateTime, decimal> topTenHighs;
        private SortedList<DateTime, decimal> topTenLows;

        //private HullMovingAverage hull;

        // P & L
        private bool openForTrading = true;
        private int sharesOwned = 0;
        private decimal portfolioProfit = 0;
        private decimal fillprice = 0;

        decimal tradeprofit = 0m;
        decimal tradefees = 0m;
        decimal tradenet = 0m;
        private decimal lasttradefees = 0;
        decimal profit = 0m;
        decimal fees = 0m;
        private decimal netprofit = 0;
        private decimal dayprofit = 0;
        private decimal dayfees = 0;
        private decimal daynet = 0;
        private decimal lastprofit = 0;
        private decimal lastfees = 0;

        // Strategy
        private InstantTrendStrategy iTrendStrategy;
        private RateOfChangePercentStrategy iRateOfChangePercentStrategy;

        private decimal nEntryPrice = 0;
        private decimal nExitPrice = 0;
        private decimal RevPct = 1.015m;
        private decimal RngFac = .35m;
        private bool bReverseTrade = false;
        private int nDirection = 0;
        private decimal nLimitPrice = 0;
        private int xOver = 0;
        private int nStatus = 0;

        private int orderId = 0;
        private bool orderCancelled = false;
        private string comment;
        private int tradesize;
        private decimal openprice = 0;

        private int tradecount;
        private int lasttradecount;
        private DateTime tradingDate;
        private bool shouldSellOutAtEod = true;

        // Zero Lag
        public Dictionary<string, decimal> todayHigh = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> todayLow = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> yesterdayHigh = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> yesterdayLow = new Dictionary<string, decimal>();
        public RollingWindow<IndicatorDataPoint> zeroLag;
        public RollingWindow<TradeBar> barHistory;


        // Scottrade
        private int fillquantity;
        private decimal amount;
        private OrderDirection orderDirection;
        private decimal _lasttotalFees;
        private readonly OrderReporter _orderReporter;


        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {
            //mylog.Debug(transheader);
            var algoname = this.GetType().Name;
            mylog.Debug(algoname);
            mylog.Debug(ondataheader);
            dailylog.Debug(algoname);
            dailylog.Debug(dailyheader);
            transactionlog.Debug(transactionheader);

            //Initialize dates
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);

            //Add as many securities as you like. All the data will be passed into the event handler:
            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

            foreach (var security in Portfolio.Securities)
            {
                todayHigh.Add(security.Key, decimal.MinValue);
                todayLow.Add(security.Key, decimal.MaxValue);
                yesterdayHigh.Add(security.Key, decimal.MinValue);
                yesterdayLow.Add(security.Key, decimal.MaxValue);
            }

            // Indicators
            Price = new RollingWindow<IndicatorDataPoint>(14);      // The price history
            barHistory = new RollingWindow<TradeBar>(14);           // The TradeBar History

            // ITrend
            trend = new InstantaneousTrend(7);
            trendHistory = new RollingWindow<IndicatorDataPoint>(14);
            iFishTrend = new InverseFisherTransform("ift", 14);
            iFishTrendHistory = new RollingWindow<IndicatorDataPoint>(14);
            trendTrigger = new RollingWindow<IndicatorDataPoint>(14);

            // Local minimum Low and maximum High
            maximum = new Maximum(14);
            minimum = new Minimum(14);

            // Percent Rate of Change
            rocp = ROCP(symbol, 4, Resolution.Minute, Field.Close);

            // Relative Strength
            rsiHigh = RSI(symbol, 14, MovingAverageType.Wilders, Resolution.Minute, Field.High);
            iFishRsiHigh = new InverseFisherTransform("ifrsh", 14);
            iFishRsiHighHistory = new RollingWindow<IndicatorDataPoint>(14);

            rsiLow = RSI(symbol, 14, MovingAverageType.Wilders, Resolution.Minute, Field.Low);
            iFishRsiLow = new InverseFisherTransform("ifrsl", 14);
            iFishRsiLowHistory = new RollingWindow<IndicatorDataPoint>(14);

            // Zero Lag Filter
            zeroLag = new RollingWindow<IndicatorDataPoint>(14);
            // CyberCycle
            cyberCycle = new CyberCycle("cc", 14);
            iFishCyberCycle = new InverseFisherTransform("ifcc", 14);
            iFishCyberCycleHistory = new RollingWindow<IndicatorDataPoint>(14);

            // The ITrendStrategy
            iTrendStrategy = new InstantTrendStrategy(symbol, 14, this);
            iTrendStrategy.shouldSellOutAtEod = shouldSellOutAtEod;

            // the ROCP Strategy
            iRateOfChangePercentStrategy = new RateOfChangePercentStrategy(symbol, 14, this);
            iRateOfChangePercentStrategy.shouldSellOutAtEod = shouldSellOutAtEod;
            sma20 = new SimpleMovingAverage(20);
            //hull = new HullMovingAverage(14);



        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            comment = string.Empty;
            barcount++;
            tradingDate = data.Time;
            decimal slope = 0;

            // Add the history for the bar
            var time = data.Time;
            Price.Add(idp(time, (data[symbol].Close + data[symbol].Open)/2));
            barHistory.Add(data[symbol]);

            // Get the high and low for the bar and accumulate into the HL for today
            todayHigh[symbol] = Math.Max(data[symbol].High, todayHigh[symbol]);
            todayLow[symbol] = Math.Min(data[symbol].Low, todayLow[symbol]);

            // Get the daily open and calculate the direction for the day
            if (barcount == 1)
            {
                dailyOpen = data[symbol];
            }
            else
            {
                if (StartingNewDay(data))
                {
                    dailyOpen = data[symbol];
                    rocp.Reset();
                    rsiHigh.Reset();
                    rsiLow.Reset();
                    yesterdayHigh[symbol] = todayHigh[symbol];
                    yesterdayLow[symbol] = todayLow[symbol];
                    todayHigh[symbol] = decimal.MinValue;
                    todayLow[symbol] = decimal.MaxValue;
                }
            }
            CalculateDailyDirection(data[symbol]);      // direction for the day

            if (barcount == 14)
                Debug("here");

            // Update the indicators
            trend.Update(idp(time, Price[0].Value));
            trendHistory.Add(idp(time, trend.Current.Value)); //add last iteration value for the cycle
            iFishTrend.Update(idp(time, trend.Current.Value));
            iFishTrendHistory.Add(idp(time, iFishTrend.Current.Value));
            trendTrigger.Add(idp(time, trend.Current.Value));
            sma20.Update(idp(time, trend.Current.Value));

            maximum.Update(idp(time, Price[0].Value));
            minimum.Update(idp(time, Price[0].Value));
            pricePassedAMaximum = PricePassedAPeak();
            pricePassedAMinimum = PricePassedAValley();

            // Update the CyberCycle
            cyberCycle.Update(idp(time, Price[0].Value));
            iFishCyberCycle.Update(idp(time, cyberCycle.Current.Value));
            iFishCyberCycleHistory.Add(idp(time, iFishCyberCycle.Current.Value));

            // RateOfChange (rocp), RSI of Highs (rsiHigh), RSI of Lows (rsiLow)
            //  are calculated automagically by the engine
            // So just inverse fisherize the rsiHigh and Low by hand
            iFishRsiHigh.Update(idp(time, rsiHigh.Current.Value));
            iFishRsiHighHistory.Add(idp(time, iFishRsiHigh.Current.Value));
            iFishRsiLow.Update(idp(time, rsiLow.Current.Value));
            iFishRsiLowHistory.Add(idp(time, iFishRsiLow.Current.Value));

            if (barcount == 1)
            {
                tradesize = (int)(Portfolio.Cash / Convert.ToInt32(Price[0].Value + 1));
                openprice = Price[0].Value;
            }

            // Logging and iTrendStrategy start on bar 3 because it uses trendHistory[0] - trendHistory[3]
            if (barcount < 7 && barcount > 2)
            {
                trendHistory[0].Value = (Price[0].Value + 2 * Price[1].Value + Price[2].Value) / 4;
            }

            if (barcount > 2)
            {
                trendTrigger[0].Value = 2 * trendHistory[0].Value - trendHistory[2].Value;
            }

            // compute the zero lag filter
            zeroLag.Add(barcount > 4
                ? idp(time, .7m * (Price[0].Value + .5m * (Price[0].Value - Price[3].Value)) + .3m * zeroLag[0])
                : idp(time, Price[0]));

            // Compute the slope since the start of the day
            slope = CalculateSlope(Price[0]);
            if (barcount == 390)
                Debug("here");

            /*
                        if (!CanceledUnfilledLimitOrder())
                        {
                            if (barcount == 14)
                                Debug("here");
                            //(dailyOpen.EndTime.Day != data[symbol].EndTime.Day) && 
                            if ((todayHigh[symbol] >= yesterdayHigh[symbol]
                                || todayLow[symbol] <= yesterdayLow[symbol]))
                            {
                                if (data[symbol].EndTime.Hour >= 9 
                                    && data[symbol].EndTime.Minute >= 32 
                                    && Portfolio[symbol].Invested == false)
                                {
                                    if (zeroLag[0].Value > trend.Current.Value
                                        && trendTrigger[0].Value > zeroLag[0].Value
                                        && barHistory[0].Open > barHistory[1].Open
                                        && barHistory[0].High > barHistory[1].High
                                        && barHistory[1].Close > barHistory[1].Low + (barHistory[1].High - barHistory[1].Low) / 3)
                                    {
                                        var ticket = Buy(symbol, tradesize);
                                        if (ticket.OrderId != 0)
                                        {
                                            comment = "Not invested Bot Long ";
                                        }
                                    }
                                    if (zeroLag[0].Value < trend.Current.Value
                                        && trendTrigger[0].Value < zeroLag[0].Value
                                        && barHistory[0].Open < barHistory[1].Open
                                        && barHistory[0].Low < barHistory[1].Low
                                        && barHistory[1].Close > barHistory[1].High + (barHistory[1].High - barHistory[1].Low) / 3)
                                    {
                                        var ticket = Sell(symbol, tradesize);
                                        if (ticket.OrderId != 0)
                                        {
                                            comment = "Not invested Sold Short ";
                                        }
                                    }

                                }

                                if (barcount == 18)
                                    comment = "decending price";
                                if (pricePassedAMaximum && Portfolio[symbol].IsLong)
                                {
                                    var ticket = Sell(symbol, tradesize);
                                    if (ticket.OrderId != 0)
                                    {
                                        comment = "Was Long: Sold just passed a Max";
                                        maximum.Reset();
                                    }
                                }
                                if (pricePassedAMinimum && Portfolio[symbol].IsShort)
                                {
                                    var ticket = Buy(symbol, tradesize);
                                    if (ticket.OrderId != 0)
                                    {
                                        comment = "Was short: Bot just passed a Min";
                                        minimum.Reset();
                                    }
                                }
                                if (ZeroLagCrossedAboveTrendline()
                                    && barHistory[0].High > barHistory[1].High
                                    && barHistory[0].Close > barHistory[0].Low + (barHistory[0].High - barHistory[0].Low) / 2)
                                {
                                    var ticket = Buy(symbol, tradesize);
                                    if (ticket.OrderId != 0)
                                    {
                                        comment = "Invested zeroLag Crossed above trendline Bot Long ";
                                    }
                                }
                                if (ZeroLagCrossedUnderTrendline()
                                    && barHistory[0].Low > barHistory[1].Low
                                    && barHistory[0].Close > barHistory[0].High - (barHistory[0].High - barHistory[0].Low) / 2)
                                {
                                    var ticket = Sell(symbol, tradesize);
                                    if (ticket.OrderId != 0)
                                    {
                                        comment = "Not invested Sold Short ";
                                    }
                                }
                                if (PriceCrossedAboveTrendline()
                                    && rocp.Current.Value >= 50
                                    && rsiHigh.Current.Value >= 80
                                    && !Portfolio.Invested)
                                {
                                    var ticket = Buy(symbol, tradesize);
                                    if (ticket.OrderId != 0)
                                    {
                                        comment = "Price crossed over trendline on a rise";
                                    }
                                }
                                if (PriceCrossedUnderTrendline()
                                    && rocp.Current.Value <= 50
                                    && rsiHigh.Current.Value <= 20
                                    && !Portfolio.Invested)
                                {
                                    var ticket = Sell(symbol, tradesize);
                                    if (ticket.OrderId != 0)
                                    {
                                        comment = "Price crossed under trendline on a drop";
                                    }
                                }
                            }
                            
 
                        }
             **/
            Strategy(data);


            sharesOwned = Portfolio[symbol].Quantity;

            if (barcount == 77)
                System.Threading.Thread.Sleep(100);
            #region logging
            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32},{33},{34},{35},{36},{37},{38},{39},{40},{41},{42},{43},{44}",
                    data.Time,
                    barcount,
                    tradesize,
                    data[symbol].Open,
                    data[symbol].High,
                    data[symbol].Low,
                    data[symbol].Close,
                    data.Time.ToShortTimeString(),
                    Price[0].Value,
                    trend.Current.Value,
                    trendTrigger[0].Value,
                    zeroLag[0].Value,
                    sma20.Current.Value,
                    iFishTrend.Current.Value,
                    cyberCycle.Current.Value,
                    iFishCyberCycle.Current.Value,
                    maximum.Current.Value,
                    minimum.Current.Value,
                    pricePassedAMaximum ? Price[0].Value : maximum.Current.Value,
                    pricePassedAMinimum ? Price[0].Value : minimum.Current.Value,
                    rocp.Current.Value,
                    rsiHigh.Current.Value,
                    rsiLow.Current.Value,
                    iFishRsiHigh.Current.Value,
                    iFishRsiLow.Current.Value,
                    dailyDirection,
                    slope,
                    comment,
                    nEntryPrice,
                    nExitPrice,
                    orderId,
                    Portfolio.TotalUnrealisedProfit,
                    sharesOwned,
                    tradeprofit,
                    tradefees,
                    tradenet,
                    lasttradefees,
                    profit,
                    fees,
                    netprofit,
                    dayprofit,
                    dayfees,
                    daynet,
                    Portfolio.TotalPortfolioValue,
                    ""
                    );
            mylog.Debug(logmsg);
            #endregion
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;

        }

        private void RocpInline()
        {
            if (!Portfolio.Invested)
            {
                if (PricePassedAValley() && rocp.Current.Value < 0)
                {
                    Buy(symbol, tradesize);
                    comment = "Bot new position ppMin && rocp < 0";
                }
                if (PricePassedAPeak() && rocp.Current.Value > 0)
                {
                    Sell(symbol, tradesize);
                    comment = "sld new position ppMAX && rocp > 0";
                }
            }
            else
            {
                if (PricePassedAPeak() && Portfolio[symbol].IsLong)
                {
                    var ticket = Sell(symbol, tradesize*2);
                    if (ticket.OrderId != 0)
                    {
                        comment = "sld Long position ppMAX";
                        //maximum.Reset();
                    }
                }
                if (PricePassedAValley() && Portfolio[symbol].IsShort)
                {
                    var ticket = Buy(symbol, tradesize*2);
                    if (ticket.OrderId != 0)
                    {
                        comment = "sld Short position ppMin";
                        //minimum.Reset();
                    }
                }
            }
            
        }

        private bool StartingNewDay(TradeBars data)
        {
            return dailyOpen.EndTime.Day != data[symbol].EndTime.Day;
        }

        private bool PriceCrossedAboveTrendline()
        {
            return Price[0].Value > trendHistory[0].Value && Price[1].Value <= trendHistory[1].Value;
        }
        private bool PriceCrossedUnderTrendline()
        {
            return Price[0].Value < trendHistory[0].Value && Price[1].Value >= trendHistory[1].Value;
        }

        private bool ZeroLagCrossedAboveTrendline()
        {
            return zeroLag[0].Value > trendHistory[0].Value && zeroLag[1].Value <= trendHistory[1].Value;
        }
        private bool ZeroLagCrossedUnderTrendline()
        {
            return zeroLag[0].Value < trendHistory[0].Value && zeroLag[1].Value >= trendHistory[1].Value;
        }


        private decimal CalculateSlope(IndicatorDataPoint currentPrice)
        {
            decimal deltay = 0;
            decimal deltax = 0;
            if (!Price.IsReady)
            {
                deltay = currentPrice.Value - Price[Price.Count - 1];
                deltax = Price.Count - 1;
            }
            else
            {
                deltay = currentPrice.Value - Price[13].Value;
                deltax = (decimal)13.0;
            }
            if (deltax == 0)
                return 0;
            return deltay / deltax;
        }

        private bool PricePassedAPeak()
        {
            try
            {
                if (barcount == 1)
                {
                    return false;
                }
                if (maximum >= Price[0].Value && maximum == Price[1].Value)
                    return true;
                return false;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
        private bool PricePassedAValley()
        {
            if (barcount == 1)
            {
                return false;
            }
            if (minimum <= Price[0].Value && minimum == Price[1].Value)
                return true;
            return false;
        }

        private void CalculateDailyDirection(TradeBar currentBar)
        {
            dailyDirection = currentBar.Close - dailyOpen.Open;
        }

        private void Strategy(TradeBars data)
        {

            comment = string.Empty;
            if (barcount < 20)
                return;
            if (barcount == 100)
                comment = "";
            if (barcount == 110)
                comment = "";
            #region "Strategy Execution"
            //comment = iTrendStrategy.ExecuteStrategy(data, tradesize, trend.Current, trendTrigger[0], out orderId);
            if (SellOutEndOfDay(data))
            {
               
                //RocpInline();
                comment = iRateOfChangePercentStrategy.ExecuteStrategy(data, tradesize, maximum.Current, minimum.Current, rocp, out orderId);
            }
            

            #endregion
            if (data.Time.Hour == 16)
            {
                CalculateDailyProfits();
            }
            sharesOwned = Portfolio[symbol].Quantity;

        }

        private bool CanceledUnfilledLimitOrder()
        {
            #region "Unfilled Limit Orders"

            OrderTicket ticket;
            bool retval = false;
            var orders = Transactions.GetOpenOrders();
            foreach (var order in orders)
            {
                if (order.Id == 30)
                {
                    System.Diagnostics.Debug.Write("here");
                    var o = Transactions.GetOrderById(order.Id);
                }
                if (!orderCancelled)
                {

                    try
                    {
                        // if we are flat just cancel the order and wait for the next set up
                        if (!Securities[symbol].HoldStock)
                        {
                            ticket = Transactions.CancelOrder(order.Id);

                            nStatus = 0; // neither long nor short
                            orderId = ticket.OrderId;
                            sharesOwned = Portfolio[symbol].Quantity;
                            comment = string.Format("Flat Not Filled. Cancelled {0} order {1}", order.Direction, orderId);
                            retval = true;
                        }
                        else
                        {
                            ticket = Transactions.CancelOrder(order.Id);

                            orderId = ticket.OrderId;
                            nStatus = 0; // neither long nor short
                            sharesOwned = Portfolio[symbol].Quantity;
                            comment = string.Format("{0} Order Not Filled. Still hold {1}", order.Direction, sharesOwned);
                            retval = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }

            #endregion

            return retval;
        }

        /// <summary>
        /// Handle order events
        /// </summary>
        /// <param name="orderEvent">the order event</param>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
            orderId = orderEvent.OrderId;
            if (orderEvent.OrderId == 30)
                System.Diagnostics.Debug.Write("here");
            if (orderEvent.Status == OrderStatus.Canceled)
            {
                orderCancelled = true;

                iTrendStrategy.orderFilled = false;



            }
            if (orderEvent.Status == OrderStatus.Filled)
            {
                orderCancelled = false;
                OrderReporter reporter = new OrderReporter((QCAlgorithm)this, transactionlog);
                reporter.ReportTransaction(orderEvent);

                tradecount++;

                if (Portfolio[orderEvent.Symbol].Invested)
                {
                    iTrendStrategy.orderFilled = true;
                    nEntryPrice = orderEvent.FillPrice;
                    iTrendStrategy.nEntryPrice = nEntryPrice;
                    CalculateTradeProfit();
                }
                else
                {
                    iTrendStrategy.orderFilled = true;
                    nExitPrice = orderEvent.FillPrice;
                    iTrendStrategy.nExitPrice = nEntryPrice;
                    CalculateTradeProfit();
                }
            }
        }

        private void ExecuteMarketOrder()
        {

        }
        private void CalculateTradeProfit()
        {
            tradeprofit = Securities[symbol].Holdings.LastTradeProfit;
            tradefees = Securities[symbol].Holdings.TotalFees - lasttradefees;
            tradenet = tradeprofit - tradefees;
            lasttradefees = Securities[symbol].Holdings.TotalFees;
            
        }
        /// <summary>
        /// Calculates and reports profits or losses after the last trade of the day
        /// </summary>
        private void CalculateDailyProfits()
        {
            foreach (SecurityHolding holding in Portfolio.Values)
            {
                dayprofit = holding.Profit - lastprofit;
                dayfees = holding.TotalFees - lastfees;
                daynet = dayprofit - dayfees;
                lastprofit = holding.Profit;
                lastfees = holding.TotalFees;
                string msg = String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}",
                    tradingDate.ToShortDateString(),
                    dayprofit,
                    dayfees,
                    daynet,
                    holding.Profit,
                    holding.TotalFees,
                    holding.Profit - holding.TotalFees,
                    tradecount - lasttradecount,
                    Portfolio.TotalPortfolioValue,
                    sharesOwned,
                    ""
                    );
                dailylog.Debug(msg);
                lasttradecount = tradecount;
                dayprofit = 0;
                dayfees = 0;
                daynet = 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        public override void OnEndOfDay(string symbol)
        {
            base.OnEndOfDay();
        }

        public bool SellOutEndOfDay(TradeBars data)
        {
            if (shouldSellOutAtEod)
            {
                if (data.Time.Hour == 15 && data.Time.Minute > 49 || data.Time.Hour == 16)
                {
                    if (Portfolio[symbol].IsLong)
                    {
                        Sell(symbol, Portfolio[symbol].AbsoluteQuantity);
                    }
                    if (Portfolio[symbol].IsShort)
                    {
                        Buy(symbol, Portfolio[symbol].AbsoluteQuantity);
                    }

                    System.Threading.Thread.Sleep(100);

                    sharesOwned = Portfolio[symbol].Quantity;
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Factory function which creates an IndicatorDataPoint
        /// </summary>
        /// <param name="time">DateTime - the bar time for the IndicatorDataPoint</param>
        /// <param name="value">decimal - the value for the IndicatorDataPoint</param>
        /// <returns>a new IndicatorDataPoint</returns>
        /// <remarks>I use this function to shorten the a Add call from 
        /// new IndicatorDataPoint(data.Time, value)
        /// Less typing.</remarks>
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

    }
}
