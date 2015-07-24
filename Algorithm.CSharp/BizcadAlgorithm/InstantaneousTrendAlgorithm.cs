using System;
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
        private DateTime _startDate = new DateTime(2015, 5, 19);
        private DateTime _endDate = new DateTime(2015, 5, 20);
        private decimal _portfolioAmount = 22000;
        private decimal _transactionSize = 22000;

        private string symbol = "AAPL";

        // Custom Logging
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");

        private string ondataheader = @"Time,CurrentBar,Open,High,Low,Close,Time,Price,Trend,Trigger, Entry Price, Exit Price, comment , direction,slope,orderId , unrealized, shares owned,trade profit, trade fees, trade net,last trade fees, profit, fees, net, day profit, day fees, day net";
        private string dailyheader = @"Trading Date,Daily Profit, Daily Fees, Daily Net, Cum profit, Cum Fees, Cum Net, Trades/day, Portfolio Value, Shares Owned";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private int barcount = 0;
        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;
        private RollingWindow<IndicatorDataPoint> trendHistory;
        private RollingWindow<IndicatorDataPoint> trendTrigger;
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
        private InstantTrendStrategy strategy;
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
            Price = new RollingWindow<IndicatorDataPoint>(14);
            trendHistory = new RollingWindow<IndicatorDataPoint>(14);
            trendTrigger = new RollingWindow<IndicatorDataPoint>(14);
            trend = new InstantaneousTrend(7);
            strategy = new InstantTrendStrategy(symbol, 14, this);
            strategy.shouldSellOutAtEod = shouldSellOutAtEod;
            //hull = new HullMovingAverage(14);


        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            barcount++;
            tradingDate = data.Time;


            if (data.Time.Day == 20)
                Debug("here");

            var time = data.Time;
            Price.Add(idp(time, data[symbol].Close));
            trend.Update(idp(time, data[symbol].Close));
            trendHistory.Add(idp(time, trend.Current.Value)); //add last iteration value for the cycle
            trendTrigger.Add(idp(time, trend.Current.Value));
            //hull.Update(idp(time, data[symbol].Close));
            if (barcount == 1)
            {
                tradesize = (int)(Portfolio.Cash / Convert.ToInt32(Price[0].Value + 1));
                openprice = Price[0].Value;
            }

            // Logging and strategy start on bar 3 because it uses trendHistory[0] - trendHistory[3]
            if (barcount < 7)
            {
                if (barcount > 2)
                {
                    trendHistory[0].Value = (Price[0].Value + 2 * Price[1].Value + Price[2].Value) / 4;
                }
            }
            if (barcount > 2)
            {
                trendTrigger[0].Value = 2 * trendHistory[0].Value - trendHistory[2].Value;
            }
            decimal slope = 0;
            if (barcount > 21)
                slope = CalculateSlope();

            if (!CanceledUnfilledLimitOrder())
                Strategy(data);



            if (barcount == 35)
                System.Threading.Thread.Sleep(100);
            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21},{22},{23},{24},{25},{26},{27},{28},{29},{30}",
                    data.Time,
                    barcount,
                    data[symbol].Open,
                    data[symbol].High,
                    data[symbol].Low,
                    data[symbol].Close,
                    data.Time.ToShortTimeString(),
                    Price[0].Value,
                    trend.Current.Value,
                    trendTrigger[0].Value,
                    nEntryPrice,
                    nExitPrice,
                    comment,
                    nDirection,
                    slope,
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
                    "",
                    ""
                    );
            mylog.Debug(logmsg);
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;

        }

        private decimal CalculateSlope()
        {
            var deltay = Price[13].Value - Price[0].Value;
            var deltax = (decimal)14.0;
            return deltay / deltax;
        }

        private void Strategy(TradeBars data)
        {
            comment = string.Empty;
            if (barcount < 20)
                return;
            if (barcount == 77)
                comment = "";
            #region "Strategy Execution"
            comment = strategy.ExecuteStrategy(data, tradesize, trend.Current, trendTrigger[0], out orderId);
            //LocalExecuteStrategy(data);
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
                            Transactions.WaitForOrder(order.Id);
                            orderId = 0;
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
                strategy.orderFilled = false;
            }
            if (orderEvent.Status == OrderStatus.Filled)
            {
                orderCancelled = false;
                OrderReporter reporter = new OrderReporter((QCAlgorithm)this, transactionlog);
                reporter.ReportTransaction(orderEvent);

                tradecount++;

                if (Portfolio[orderEvent.Symbol].Invested)
                {
                    strategy.orderFilled = true;
                    nEntryPrice = orderEvent.FillPrice;
                    strategy.nEntryPrice = nEntryPrice;
                }
                else
                {
                    strategy.orderFilled = true;
                    nExitPrice = orderEvent.FillPrice;
                    strategy.nExitPrice = nEntryPrice;
                    CalculateTradeProfit();
                }
            }
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
                if (data.Time.Hour == 15 && data.Time.Minute > 55)
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
                    return true;
                }
            }

            return false;
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
