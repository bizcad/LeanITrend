using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Util;
using QuantConnect.Configuration;

namespace QuantConnect.Algorithm.CSharp
{
    public class MultiSignalAlgorithm : QCAlgorithm
    {

        #region "Variables"
        DateTime startTime = DateTime.Now;
        private DateTime _startDate = new DateTime(2015, 8, 14);
        private DateTime _endDate = new DateTime(2015, 8, 14);
        //private DateTime _startDate = new DateTime(2015, 5, 19);
        //private DateTime _endDate = new DateTime(2015, 11, 3);
        private decimal _portfolioAmount = 26000;
        private decimal _transactionSize = 15000;
        //+----------------------------------------------------------------------------------------+
        //  Algorithm Control Panel                         
        // +---------------------------------------------------------------------------------------+
        private static int ITrendPeriod = 7;            // Instantaneous Trend period.
        private static decimal Tolerance = 0.000m;      // Trigger - Trend crossing tolerance.
        private static decimal RevertPCT = 1.0015m;     // Percentage tolerance before revert position.

        private static decimal maxLeverage = 1m;        // Maximum Leverage.
        private decimal leverageBuffer = 0.00m;         // Percentage of Leverage left unused.
        private int maxOperationQuantity = 500;         // Maximum shares per operation.

        private decimal RngFac = 0.35m;                 // Percentage of the bar range used to estimate limit prices.

        private bool resetAtEndOfDay = true;            // Reset the strategies at EOD.
        private bool noOvernight = true;                // Close all positions before market close.
        private decimal lossThreshhold = -55;           // When unrealized losses fall below, revert position
        // +---------------------------------------------------------------------------------------+

        private List<Symbol> Symbols;
        private Symbol symbol;

        private int barcount = 0;

        private RollingWindow<IndicatorDataPoint> Price;
        private InstantaneousTrend trend;

        #region ITrend
        private Dictionary<string, OrderSignal> LastOrderSent = new Dictionary<string, OrderSignal>();
        #endregion

        #region lists

        List<SignalInfo> signalInfos = new List<SignalInfo>();
        #endregion
        #region "logging P&L"

        // P & L
        private int sharesOwned = 0;
        private decimal tradeprofit = 0m;
        private decimal tradefees = 0m;
        private decimal tradenet = 0m;
        private decimal lasttradefees = 0;
        decimal profit = 0m;
        decimal fees = 0m;
        private decimal netprofit = 0;
        private decimal dayprofit = 0;
        private decimal dayfees = 0;
        private decimal daynet = 0;
        private decimal lastprofit = 0;
        private decimal lastfees = 0;
        private decimal totalProfit = 0;

        private int lasttradecount;
        private DateTime tradingDate;
        private decimal nExitPrice = 0;
        private OrderStatus tradeResult;


        #endregion
        #region "Custom Logging"
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");
        //private ILogHandler transactionlog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("TransactionFileLogHandler");
        private readonly OrderTransactionFactory _orderTransactionFactory;

        private string ondataheader =
            @"Symbol, Time,BarCount,Volume, Open,High,Low,Close,,,Time,Price,Trend, Trigger, orderSignal, Comment,, EntryPrice, Exit Price,Unrealized,Order Id, Owned, TradeNet, Portfolio";

        private SigC _scig5C = new SigC();

        private string json;


        private string dailyheader = @"Trading Date,Daily Profit, Portfolio Value";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private List<OrderTransaction> _transactions;
        private OrderTransactionProcessor _orderTransactionProcessor = new OrderTransactionProcessor();
        //private List<OrderTicket> _ticketsQueue;

        private List<OrderTransaction> _proformatransactions;


        private List<OrderEvent> _orderEvents = new List<OrderEvent>();
        private int _tradecount = 0;
        #endregion

        private bool WebSiteAvailable = true;

        private bool shouldSellOutAtEod = true;
        private int orderId = 0;
        private OrderSignal orderSignal;
        private decimal nEntryPrice = 0;
        private string sigcomment;
        private string comment;
        private StringBuilder minuteReturns = new StringBuilder();
        private StringBuilder minuteHeader = new StringBuilder();
        private bool minuteHeaderFlag = true;

        private string sd;
        private string ed;

        #endregion

        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetEndDate(System.DateTime)"/>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public override void Initialize()
        {
            symbol = new Symbol("NFLX");
            #region "Read Symbols from File"
            /**********************************************
             THIS SECTION IS FOR READING SYMBOLS FROM A FILE
            ************************************************/
            //string symbols;
            Symbols = new List<Symbol>();

            var filename = AssemblyLocator.ExecutingDirectory() + "symbols.txt";
            using (StreamReader sr = new StreamReader(filename))
            {
                string[] symbols = { };
                var readLine = sr.ReadLine();
                if (readLine != null) symbols = readLine.Split(',');

                foreach (string t in symbols)
                {
                    Symbols.Add(new Symbol(t));
                }

                sr.Close();
            }
            // Make sure the list contains the static symbol
            //if (!Symbols.Contains(symbol))
            //{
            //    Symbols.Add(symbol);
            //}
            #endregion

            #region logging
            var algoname = this.GetType().Name;
            mylog.Debug(algoname);
            StringBuilder sb = new StringBuilder();
            foreach (var s in Symbols)
                sb.Append(s.Value + ",");
            mylog.Debug(ondataheader);
            dailylog.Debug(algoname + " " + sb.ToString());
            dailylog.Debug(dailyheader);
            _proformatransactions = new List<OrderTransaction>();
            string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
            #endregion


            //Initialize dates
            //sd = Config.Get("start-date");
            //ed = Config.Get("end-date");

            //_startDate = new DateTime(Convert.ToInt32(sd.Substring(0, 4)), Convert.ToInt32(sd.Substring(4, 2)), Convert.ToInt32(sd.Substring(6, 2)));
            //_endDate = new DateTime(Convert.ToInt32(ed.Substring(0, 4)), Convert.ToInt32(ed.Substring(4, 2)), Convert.ToInt32(ed.Substring(6, 2)));


            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);



            minuteReturns.AppendFormat("{0},{1}", symbol, _startDate.ToShortDateString());
            minuteHeader.AppendFormat("Symbol,Date");

            //Add as many securities as you like. All the data will be passed into the event handler:
            int id = 0;
            foreach (Symbol s in Symbols)
            {
                symbol = s;
                AddSecurity(SecurityType.Equity, symbol);
                signalInfos.Add(new SignalInfo()
                {
                    Id = id++,
                    Name = s.Permtick,
                    Symbol = s,
                    SignalType = typeof(Sig9),
                    Value = OrderSignal.doNothing,
                    IsActive = true,
                    Status = OrderStatus.None,
                    SignalJson = string.Empty,
                    InternalState = string.Empty,
                    Comment = string.Empty,
                    nTrig = 0,
                    Price = new RollingWindow<IndicatorDataPoint>(14),
                    trend = new InstantaneousTrend(s.Permtick, 7, .24m)
                });
            }

            _orderTransactionProcessor = new OrderTransactionProcessor();
            _transactions = new List<OrderTransaction>();


            #region ITrend
            LastOrderSent.Add(symbol, OrderSignal.doNothing);
            #endregion

            SetBenchmark(symbol);
            // for use with Tradier. Default is IB.
            //var security = Securities[symbol];
            //security.TransactionModel = new ConstantFeeTransactionModel(1.0m);

        }
        #region "one minute events"
        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            foreach (KeyValuePair<Symbol, TradeBar> kvp in data)
            {
                OnDataForSymbol(kvp);
            }
        }
        private void OnDataForSymbol(KeyValuePair<Symbol, TradeBar> data)
        {
            #region logging

            comment = string.Empty;
            tradingDate = this.Time;

            #endregion

            string m = JsonConvert.SerializeObject(data);
            //string quote = GetQuotes(data.Key);
            string msg = string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}",
                "TradeBar",
                data.Key,
                data.Value.EndTime,
                data.Value.Period,
                data.Value.Open,
                data.Value.High,
                data.Value.Low,
                data.Value.Close,
                data.Value.Volume);

            SendMessage(typeof(TradeBar), msg);
            barcount++;
            var time = this.Time;

            List<SignalInfo> minuteSignalInfos = new List<SignalInfo>(signalInfos.Where(s => s.Name == data.Key));
            if (minuteSignalInfos.Any())
            {
                foreach (var signalInfo in minuteSignalInfos)
                {
                    //                    signalInfo.Price.Add(idp(time, (data.Value.Close + data.Value.Open) / 2));
                    signalInfo.Price.Add(idp(time, data.Value.Close));
                    // Update the indicators
                    signalInfo.trend.Update(idp(time, signalInfo.Price[0].Value));
                }
                // Get the OrderSignal from the Sig9
                GetOrderSignals(data, minuteSignalInfos);
                foreach (var currentSignalInfo in minuteSignalInfos)
                {
                    // If EOD, set signal to sell/buy out.
                    OrderSignal signal = currentSignalInfo.Value;
                    SellOutAtEndOfDay(data, ref signal);
                    currentSignalInfo.Value = signal;
                    if (currentSignalInfo.Status == OrderStatus.Submitted)
                    {
                        HandleSubmitted(data, currentSignalInfo);
                    }
                    if (currentSignalInfo.Status == OrderStatus.PartiallyFilled)
                    {
                        HandlePartiallyFilled(data, currentSignalInfo);
                    }

                    if (currentSignalInfo.Value != OrderSignal.doNothing && currentSignalInfo.IsActive)
                    {
                        // set now because MarketOrder fills can happen before ExecuteStrategy returns.
                        currentSignalInfo.Status = OrderStatus.New;
                        currentSignalInfo.IsActive = false;
                        ExecuteStrategy(currentSignalInfo.Symbol, currentSignalInfo, data);
                    }
                }
            }
            sharesOwned = Portfolio[data.Key].Quantity;
            #region "biglog"

            string logmsg =
                string.Format(
                    "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20}" +
                    ",{21},{22},{23},{24},{25},{26},{27},{28},{29},{30},{31},{32}",
                    symbol,
                    time,
                    barcount,
                    data.Value.Volume,
                    data.Value.Open,
                    data.Value.High,
                    data.Value.Low,
                    data.Value.Close,
                    "",
                    "",
                    time.ToShortTimeString(),
                    signalInfos[0].Price[0].Value,
                    signalInfos[0].trend.Current.Value,
                    signalInfos[0].nTrig,
                    signalInfos[0].Value,
                    comment,
                    "",
                    nEntryPrice,
                    signalInfos[0].IsActive,
                    Portfolio.TotalUnrealisedProfit,
                    orderId,
                    sharesOwned,
                    tradenet,
                    Portfolio.TotalPortfolioValue,
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    "",
                    ""
                    );
            mylog.Debug(logmsg);

            minuteReturns.AppendFormat(",{0}", Math.Round(Portfolio.TotalPortfolioValue, 2));
            if (minuteHeaderFlag)
            {
                minuteHeader.AppendFormat(",{0}", data.Value.EndTime.ToLongTimeString());
            }
            tradeprofit = 0;
            tradefees = 0;
            tradenet = 0;
            #endregion

            // At the end of day, reset the trend and trendHistory
            if (time.Hour == 16)
            {
                minuteReturns.AppendLine(string.Format(@",{0}", CalculateDailyProfits()));
                minuteReturns.AppendFormat("{0},{1}", symbol, this.Time.AddDays(1).ToShortDateString());
                if (minuteHeaderFlag)
                {
                    minuteHeader.AppendLine(",P/L");
                    minuteHeaderFlag = false;
                }
                barcount = 0;
            }
        }

        private void HandlePartiallyFilled(KeyValuePair<Symbol, TradeBar> data, SignalInfo currentSignalInfo)
        {
            IEnumerable<OrderTicket> livetickets =
                Transactions.GetOrderTickets(
                    t => t.Symbol == data.Key && t.Status == OrderStatus.Submitted);

            if (livetickets != null)
            {
                foreach (OrderTicket liveticket in livetickets)
                {
                    if (liveticket.Quantity > 0) // long
                    {
                        AlterLongLimit(data, liveticket, currentSignalInfo);
                    }
                    else // short
                    {
                        AlterShortLimit(data, liveticket, currentSignalInfo);
                    }
                }
            }
        }

        private void HandleSubmitted(KeyValuePair<Symbol, TradeBar> data, SignalInfo currentSignalInfo)
        {
            IEnumerable<OrderTicket> livetickets =
                Transactions.GetOrderTickets(
                    t => t.Symbol == data.Key && t.Status == OrderStatus.Submitted);

            if (livetickets != null)
            {
                foreach (OrderTicket liveticket in livetickets)
                {
                    if (liveticket.Quantity > 0) // long
                    {
                        AlterLongLimit(data, liveticket, currentSignalInfo);
                    }
                    else // short
                    {
                        AlterShortLimit(data, liveticket, currentSignalInfo);
                    }
                }
            }
        }

        private void AlterShortLimit(KeyValuePair<Symbol, TradeBar> data, OrderTicket liveticket, SignalInfo currentSignalInfo)
        {
            var limit = liveticket.Get(OrderField.LimitPrice);
            decimal newLimit = limit;
            currentSignalInfo.TradeAttempts++;
            if (limit > data.Value.High)
            {
                newLimit = data.Value.Close - 0.01m;
            }
            OrderResponse response = liveticket.Update(new UpdateOrderFields
            {
                LimitPrice = newLimit,
                Tag = "Update #" + (liveticket.UpdateRequests.Count + 1)
            });
            if (response.IsSuccess)
            {
                //Log(string.Format("Short Order {0}. Status: {1} Updated {2} to new price {3}. Trade Attempts: {4}", liveticket.OrderId, liveticket.Status, limit, newLimit, currentSignalInfo.TradeAttempts));
            }
            else
            {
                if (!response.IsProcessed)
                {
                    Log(string.Format("Order {0} not yet processed to new price {1}", liveticket.OrderId, limit));
                }
                if (response.IsError)
                {
                    Log(response.ToString());
                }
            }
        }

        private void AlterLongLimit(KeyValuePair<Symbol, TradeBar> data, OrderTicket liveticket, SignalInfo currentSignalInfo)
        {
            var limit = liveticket.Get(OrderField.LimitPrice);
            decimal newLimit = limit;
            currentSignalInfo.TradeAttempts++;
            if (newLimit < data.Value.Low)
            {
                newLimit = data.Value.Close + 0.01m;
            }
            OrderResponse response = liveticket.Update(new UpdateOrderFields
            {
                LimitPrice = newLimit,
                Tag = "Update #" + (liveticket.UpdateRequests.Count + 1)
            });
            if (response.IsSuccess)
            {
                //Log(string.Format("Long Order {0}. Status: {1} Updated {2} to new price {3}. Trade Attempts: {4}", liveticket.OrderId, liveticket.Status, limit, newLimit, currentSignalInfo.TradeAttempts));
            }
            else
            {
                if (!response.IsProcessed)
                {
                    Log(string.Format("Order {0} not yet processed to new price {1}", liveticket.OrderId, limit));
                }
                if (response.IsError)
                {
                    Log(response.ToString());
                }
            }
        }

        #endregion

        /// <summary>
        /// Run the strategy associated with this algorithm
        /// </summary>
        /// <param name="data">TradeBars - the data received by the OnData event</param>
        public void GetOrderSignals(KeyValuePair<Symbol, TradeBar> data, List<SignalInfo> signalInfos)
        {
            #region "GetOrderSignals Execution"

            foreach (SignalInfo info in signalInfos)
            {
                info.Value = OrderSignal.doNothing;

                Type t = info.SignalType;
                var sig = Activator.CreateInstance(t) as ISigSerializable;
                if (sig != null)
                {
                    //handledTickets = HandleTickets();
                    sig.symbol = data.Key;
                    sig.Deserialize(info.SignalJson);
                    sig.Barcount = barcount; // for debugging
                    switch (info.Status)
                    {
                        case OrderStatus.None:
                        case OrderStatus.New:
                        case OrderStatus.Submitted:
                            sig.orderFilled = false;
                            break;
                        case OrderStatus.Filled:
                            info.TradeAttempts = 0;
                            sig.orderFilled = true;
                            break;
                        case OrderStatus.PartiallyFilled:
                            sig.orderFilled = true;
                            break;
                        case OrderStatus.Canceled:
                        case OrderStatus.Invalid:
                            info.TradeAttempts = 0;
                            sig.orderFilled = false;
                            break;
                    }

                    Dictionary<string, string> paramlist = new Dictionary<string, string>
                            {
                                {"symbol", data.Key.ToString()},
                                {"Barcount", barcount.ToString(CultureInfo.InvariantCulture)},
                                {"nEntryPrice", nEntryPrice.ToString(CultureInfo.InvariantCulture)},
                                {"IsLong", Portfolio[symbol].IsLong.ToString()},
                                {"IsShort", Portfolio[symbol].IsShort.ToString()},
                                {"trend", info.trend.Current.Value.ToString(CultureInfo.InvariantCulture)},
                                {"lossThreshhold", lossThreshhold.ToString(CultureInfo.InvariantCulture)},
                                {"UnrealizedProfit", Portfolio[symbol].UnrealizedProfit.ToString(CultureInfo.InvariantCulture)}
                            };


                    info.Value = sig.CheckSignal(data, paramlist, out comment);
                    info.nTrig = sig.nTrig;
                    info.Comment = comment;

                    if (Time.Hour == 16)
                    {
                        sig.Reset();
                    }
                    info.SignalJson = sig.Serialize();
                    info.InternalState = sig.GetInternalStateFields().ToString();
                }
            }


            #endregion  // execution
        }


        #region "Event Processiong"
        /// <summary>
        /// Handle order events
        /// </summary>
        /// <param name="orderEvent">the order event</param>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
            ProcessOrderEvent(orderEvent);
        }

        /// <summary>
        /// Local processing of the order event.  It only logs the transaction and orderEvent
        /// </summary>
        /// <param name="orderEvent">OrderEvent - the order event</param>
        private void ProcessOrderEvent(OrderEvent orderEvent)
        {
            IEnumerable<OrderTicket> tickets;

            //add to the list of order events which is saved to a file when running locally 
            //  I will use this file to test Stefano Raggi's code

            _orderEvents.Add(orderEvent);
            var currentSignalInfo = signalInfos.FirstOrDefault(s => s.Symbol == orderEvent.Symbol);
            orderId = orderEvent.OrderId;
            tradeResult = orderEvent.Status;

            if (currentSignalInfo != null)
                currentSignalInfo.Status = orderEvent.Status;


            tickets = Transactions.GetOrderTickets(t => t.OrderId == orderId);

            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                case OrderStatus.None:
                case OrderStatus.Submitted:
                case OrderStatus.Invalid:

                    // just checking to make sure they are coming through


                    break;
                case OrderStatus.PartiallyFilled:
                    if (currentSignalInfo != null)
                    {

                        if (Portfolio[symbol].HoldStock)
                        {
                            nEntryPrice = Portfolio[symbol].AveragePrice;
                        }
                        else
                        {
                            nEntryPrice = 0;
                        }

                    }

                    break;
                case OrderStatus.Canceled:
                    //if (tickets != null)
                    //{
                    //    foreach (OrderTicket ticket in tickets)
                    //    {
                    //        int infoId = Convert.ToInt32(ticket.Tag);
                    //        SignalInfo si = signalInfos.FirstOrDefault(f => f.Id == infoId);
                    //        if (si != null)
                    //            si.IsActive = true;
                    //    }
                    //}
                    if (currentSignalInfo != null)
                        currentSignalInfo.IsActive = true;

                    break;
                case OrderStatus.Filled:

                    if (currentSignalInfo != null)
                        currentSignalInfo.IsActive = true;

                    if (Portfolio[symbol].HoldStock)
                    {
                        nEntryPrice = Portfolio[symbol].AveragePrice;
                        //nExitPrice = 0;
                    }
                    else
                    {
                        nEntryPrice = 0;
                        //nExitPrice = orderEvent.FillPrice;
                    }

                    if (tickets != null)
                    {
                        foreach (OrderTicket ticket in tickets)
                        {
                            //int infoId = Convert.ToInt32(ticket.Tag);

                            #region "log the ticket as a OrderTransacton"
                            OrderTransactionFactory transactionFactory = new OrderTransactionFactory((QCAlgorithm)this);
                            OrderTransaction t = transactionFactory.Create(orderEvent, ticket, false);
                            _transactions.Add(t);
                            _orderTransactionProcessor.ProcessTransaction(t);
                            _tradecount++;
                            if (WebSiteAvailable)
                                SendTransaction(t);
                            if (_orderTransactionProcessor.TotalProfit != totalProfit)
                            {
                                tradenet = CalculateTradeProfit(t.Symbol);
                            }
                            totalProfit = _orderTransactionProcessor.TotalProfit;
                            #endregion

                        }
                    }
                    break;
            }
        }

        private string GetQuotes(string symbol)
        {
            string ret = string.Empty;
            try
            {
                HttpRequestMessage request = CreateGetRequest(symbol);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Add("Authorization", String.Format("Bearer {0}", Config.Get("tradier-access-token")));

                //using (var client = new HttpClient())
                //{
                //    HttpResponseMessage response = client.SendAsync(request).Result;
                //    if (!response.IsSuccessStatusCode)
                //    {
                //        string r = "Error Code" + response.StatusCode + " : Message - " + response.ReasonPhrase;
                //    }
                //    ret = response.Content.ReadAsStringAsync().Result;
                //}
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return ret;
        }
        private HttpRequestMessage CreateGetRequest(string symbol)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            string _apiHost = @"https://sandbox.tradier.com/";
            string uriStem = @"v1/markets/quotes";
            StringBuilder sb = new StringBuilder();
            sb.Append("symbols=");
            sb.Append(symbol);

            request.RequestUri = new Uri(String.Format("{0}{1}?{2}", _apiHost, uriStem, sb.ToString()));
            return request;
        }

        private void SendMessage(Type type, string message)
        {
            QCMessage msg = new QCMessage();
            msg.TypeName = type.Name;
            msg.Contents = message;
            msg.TimeSent = DateTime.Now;
            msg.TimeRecv = DateTime.Now;

            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            string _apiHost = @"http://localhost:63144/";
            string uriStem = @"api/QCMessage";

            request.RequestUri = new Uri(String.Format("{0}{1}", _apiHost, uriStem));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using (var client = new HttpClient())
            {
                HttpResponseMessage response = client.PostAsJsonAsync(request.RequestUri, msg).Result;
                if (!response.IsSuccessStatusCode)
                {
                    string r = "Error Code" + response.StatusCode + " : Message - " + response.ReasonPhrase;
                    Log(r);
                }
                else
                {
                    using (HttpContent content = response.Content)
                    {
                        // ... Read the string.
                        string result = content.ReadAsStringAsync().Result;
                        QCMessage resultMessage = JsonConvert.DeserializeObject<QCMessage>(result);
                        int id = resultMessage.Id;

                        // ... Display the result.
                        //if (result != null &&
                        //result.Length >= 50)
                        //{
                        //    Console.WriteLine(result.Substring(0, 50) + "...");
                        //}
                    }

                }

                //var uri = new Uri(@"http://localhost:63144/");
                //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client.BaseAddress = uri;
                //HttpResponseMessage q = client.GetAsync("api/GetQCMessage/1").Result;
                //if (!q.IsSuccessStatusCode)
                //{
                //    string r = "Error Code" + q.StatusCode + " : Message - " + q.ReasonPhrase;
                //    Log(r);
                //}
                //HttpResponseMessage response = client.PostAsJsonAsync("api/Message", msg).Result;
                //if (!response.IsSuccessStatusCode)
                //{
                //    string r = "Error Code" + response.StatusCode + " : Message - " + response.ReasonPhrase;
                //    Log(r);
                //}
            }
            
        }
        private void SendTransaction(OrderTransaction t)
        {
            string j = JsonConvert.SerializeObject(t);
            try
            {
                using (var client = new HttpClient())
                {

                    var uri = new Uri(@"http://localhost:55293/");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.BaseAddress = uri;

                    //HttpResponseMessage response = client.PostAsJsonAsync("api/OrderTransaction", t).Result;
                    //if (!response.IsSuccessStatusCode)
                    //{
                    //    string r = "Error Code" + response.StatusCode + " : Message - " + response.ReasonPhrase;
                    //    Log(r);
                    //}
                }
            }
            catch (AggregateException aggregateException)
            {
                WebSiteAvailable = false;
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                SummarizeExceptions(sb, aggregateException);
                sb.AppendLine(aggregateException.StackTrace);
                Log(sb.ToString());
            }
        }

        private static void SummarizeExceptions(StringBuilder sb, Exception ex)
        {
            sb.Append(ex.GetType().Name);
            sb.Append(" threw ");
            sb.AppendLine(ex.Message);
            if (ex.InnerException != null)
            {
                SummarizeExceptions(sb, ex.InnerException);
            }
        }

        private void GetTransasctions()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var uri = new Uri(@"http://localhost:63144/");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.BaseAddress = uri;

                    HttpResponseMessage response = client.GetAsync("api/QCMessage/1").Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string r = "Error Code" + response.StatusCode + " : Message - " + response.ReasonPhrase;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion

        #region "Profit Calculations for logging"
        private decimal CalculateTradeProfit(Symbol symbol)
        {
            return _orderTransactionProcessor.CalculateLastTradePandL(symbol);
        }
        private decimal CalculateDailyProfits()
        {
            // get todays trades
            var trades = _orderTransactionProcessor.Trades.Where(t => t.DateAcquired.Year == tradingDate.Year
                                                                      && t.DateAcquired.Month == tradingDate.Month
                                                                      && t.DateAcquired.Day == tradingDate.Day);



            var todayNet = trades.Sum(t => t.GainOrLoss);

            #region logging
            string message = String.Format("{0},{1},{2}",
                tradingDate.ToShortDateString(),
                todayNet,
                Portfolio.TotalPortfolioValue
                );


            dailylog.Debug(message);

            lasttradecount = _tradecount;
            dayprofit = 0;
            dayfees = 0;
            daynet = 0;


            #endregion

            return todayNet;
        }
        #endregion

        #region "Methods"
        /// <summary>
        /// Executes the ITrend strategy orders.
        /// </summary>
        /// <param name="symbol">The symbol to be traded.</param>
        /// <param name="signalInfo">The actual arder to be execute.</param>
        /// <param name="data">The actual TradeBar data.</param>
        private void ExecuteStrategy(Symbol symbol, SignalInfo signalInfo, KeyValuePair<Symbol, TradeBar> data)
        {
            decimal limitPrice = 0m;
            int shares = Convert.ToInt32(PositionShares(symbol, signalInfo));
            if (shares == 0)
            {
                return;
            }
            ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
            OrderTicket ticket;

            if (shares == 0)
                signalInfo.Value = OrderSignal.doNothing;

            switch (signalInfo.Value)
            {
                case OrderSignal.goLongLimit:
                    // Define the limit price.
                    limitPrice = priceCalculator.Calculate(data.Value, signalInfo, RngFac);
                    //ticket = MarketOrder(symbol, shares, false, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                    ticket = LimitOrder(symbol, shares, limitPrice, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                    //_ticketsQueue.Add(ticket);
                    break;

                case OrderSignal.goShortLimit:
                    limitPrice = priceCalculator.Calculate(data.Value, signalInfo, RngFac);
                    ticket = MarketOrder(symbol, shares, false, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                    //ticket = LimitOrder(symbol, shares, limitPrice, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                    //_ticketsQueue.Add(ticket);
                    break;

                case OrderSignal.goLong:
                case OrderSignal.goShort:
                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    ticket = MarketOrder(symbol, shares, false, signalInfo.Id.ToString(CultureInfo.InvariantCulture));
                    //_ticketsQueue.Add(ticket);
                    break;

                default: break;
            }
        }


        /// <summary>
        /// This function is called prior to calling the GetOrderSignals to check to see if a ticket was filled
        /// If the ticket did not fill within one bar, cancel it and assume the market moved away from the limit order
        /// 
        /// It re
        /// </summary>

        //private List<ProformaOrderTicket> HandleTickets()
        //{
        //List<ProformaOrderTicket> proformaOrderTickets = new List<ProformaOrderTicket>();
        ////if (maketrade)
        ////{
        //// process a real order
        //foreach (OrderTicket queuedTicket in _ticketsQueue)
        //{
        //    ProformaOrderTicket proformaLiveTicket = new ProformaOrderTicket();

        //    // Check the ticket against the Transactions version of the ticket
        //    IEnumerable<OrderTicket> livetickets = Transactions.GetOrderTickets(t => t.OrderId == queuedTicket.OrderId);

        //    if (livetickets != null)
        //    {
        //        foreach (OrderTicket liveticket in livetickets)
        //        {
        //            proformaLiveTicket.Status = liveticket.Status;
        //            proformaLiveTicket.OrderId = liveticket.OrderId;
        //            proformaLiveTicket.Symbol = liveticket.Symbol;
        //            proformaLiveTicket.Source = liveticket.Tag;
        //            proformaLiveTicket.TicketTime = liveticket.Time;
        //            proformaLiveTicket.Security_Type = liveticket.SecurityType;
        //            proformaLiveTicket.Tag = liveticket.Tag;
        //            proformaLiveTicket.TicketOrderType = liveticket.OrderType;

        //            switch (liveticket.Status)
        //            {
        //                case OrderStatus.Canceled:
        //                case OrderStatus.New:
        //                case OrderStatus.None:
        //                    break;
        //                case OrderStatus.Invalid:
        //                    proformaLiveTicket.ErrorMessage = liveticket.GetMostRecentOrderResponse().ErrorMessage;
        //                    break;
        //                case OrderStatus.Submitted:
        //                    liveticket.Cancel();
        //                    proformaLiveTicket.Status = OrderStatus.Canceled;
        //                    proformaLiveTicket.QuantityFilled = 0; // they are probably already 0
        //                    proformaLiveTicket.AverageFillPrice = 0;
        //                    break;
        //                case OrderStatus.Filled:
        //                case OrderStatus.PartiallyFilled:

        //                    #region logging
        //                    if (Portfolio[symbol].Invested)
        //                    {
        //                        nEntryPrice = Portfolio[symbol].IsLong ? liveticket.AverageFillPrice : liveticket.AverageFillPrice * -1;
        //                        nExitPrice = 0;
        //                    }
        //                    else
        //                    {
        //                        nExitPrice = liveticket.AverageFillPrice;
        //                        nEntryPrice = 0;
        //                    }
        //                    #endregion
        //                    proformaLiveTicket.Direction = liveticket.Quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell;
        //                    proformaLiveTicket.Status = OrderStatus.Filled;
        //                    proformaLiveTicket.QuantityFilled = (int)liveticket.QuantityFilled;
        //                    proformaLiveTicket.AverageFillPrice = liveticket.AverageFillPrice;
        //                    break;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        proformaLiveTicket.ErrorMessage =
        //            string.Format("Ticket with Id {0} could not be found in Transactions.", queuedTicket.OrderId);


        //    }
        //    proformaOrderTickets.Add(proformaLiveTicket);
        //}
        //return proformaOrderTickets;
        //}

        private decimal GetBetSize(Symbol symbol, SignalInfo signalInfo)
        {
            // *********************************
            //  ToDo: Kelly Goes here in a custom bet sizer
            //  This implementation uses the same as the original algo
            //    and just refactors it out to a class.
            // *********************************
            IBetSizer allocator = new InstantTrendBetSizer(this);
            return allocator.BetSize(symbol, signalInfo.Price[0].Value, _transactionSize, signalInfo);
        }

        /// <summary>
        /// Estimate number of shares, given a kind of operation.
        /// </summary>
        /// <param name="symbol">The symbol to operate.</param>
        /// <param name="order">The kind of order.</param>
        /// <returns>The signed number of shares given the operation.</returns>
        public decimal PositionShares(Symbol symbol, SignalInfo signalInfo)
        {
            decimal quantity = 0;
            int operationQuantity;
            decimal targetSize = GetBetSize(symbol, signalInfo);

            switch (signalInfo.Value)
            {
                case OrderSignal.goLongLimit:
                case OrderSignal.goLong:
                    //operationQuantity = CalculateOrderQuantity(symbol, targetSize);     // let the algo decide on order quantity
                    //operationQuantity = (int)targetSize;
                    quantity = Math.Min(maxOperationQuantity, targetSize);
                    break;

                case OrderSignal.goShortLimit:
                case OrderSignal.goShort:
                    //operationQuantity = CalculateOrderQuantity(symbol, targetSize);     // let the algo decide on order quantity
                    operationQuantity = (int)targetSize;
                    quantity = -Math.Min(maxOperationQuantity, targetSize);
                    break;

                case OrderSignal.closeLong:
                case OrderSignal.closeShort:
                    if (Portfolio[symbol].Quantity != 0)
                        quantity = -Portfolio[symbol].Quantity;
                    break;

                case OrderSignal.revertToLong:
                case OrderSignal.revertToShort:
                    if (Portfolio[symbol].Quantity != 0)
                        quantity = -2 * Portfolio[symbol].Quantity;
                    break;

                default:
                    quantity = 0;
                    break;
            }

            return quantity;
        }

        /// <summary>
        /// Sells out all positions at 15:50, and calculates the profits for the day
        ///  emails the transactions for the day to me
        /// </summary>
        /// <param name="data">TradeBars - the data</param>
        /// <param name="signalInfosMinute"></param>
        /// <param name="signal">the current OrderSignal</param>
        /// <returns>false if end of day, true during the day </returns>
        private void SellOutAtEndOfDay(KeyValuePair<Symbol, TradeBar> data, ref OrderSignal signal)
        {
            if (shouldSellOutAtEod)
            {
                signal = OrderSignal.doNothing;      // Just in case a signal slipped through in the last minute.
                #region logging
                if (Time.Hour == 16)
                {
                    #region logging

                    SendTransactionsToFile(data.Key + "transactions.csv");
                    #endregion

                    NotifyUser();
                }
                #endregion

                if (Time.Hour == 15 && Time.Minute > 45)
                {
                    if (Portfolio[data.Key].IsLong)
                    {
                        signal = OrderSignal.goShort;
                    }
                    if (Portfolio[data.Key].IsShort)
                    {
                        signal = OrderSignal.goLong;
                    }
                }
            }
        }

        private void NotifyUser()
        {
            #region logging

            if (this.Time.Hour == 16)
            {
                var transactionsAsCsv = CsvSerializer.Serialize<OrderTransaction>(",", _transactions, true);
                StringBuilder sb = new StringBuilder();
                var transcount = _transactions.Count();
                foreach (string s in transactionsAsCsv)
                    sb.AppendLine(s);
                string attachment = sb.ToString();

                Notify.Email("nicholasstein@cox.net",
                    string.Format("Transactions For: {0}", Time.ToLongDateString()),
                    string.Format("Todays Date: {0} \nNumber of Transactions: {1}", Time.ToLongDateString(), _transactions.Count()),
                    attachment);


                var tradesAsCsv = CsvSerializer.Serialize<MatchedTrade>(",",
                    _orderTransactionProcessor.Trades.Where(f => f.DateAcquired == tradingDate), true);
                var tradecount = _orderTransactionProcessor.Trades.Count();

                sb = new StringBuilder();
                foreach (string s in tradesAsCsv)
                {
                    sb.AppendLine(s);
                }
                attachment = sb.ToString();

                Notify.Email("nicholasstein@cox.net",
                    string.Format("Trades for: {0}", Time.ToLongDateString()),
                    string.Format("Todays Date: {0} \nNumber of Trades: {1}", Time.ToLongDateString(), tradecount),
                    attachment);

                _transactions = new List<OrderTransaction>();
            }

            #endregion
        }
        /// <summary>
        /// Handles the On end of algorithm 
        /// </summary>
        public override void OnEndOfAlgorithm()
        {
            StringBuilder sb = new StringBuilder();
            //sb.Append(" Symbols: ");
            foreach (var s in Symbols)
            {

                sb.Append(s.ToString());
                sb.Append(",");
            }
            string symbolsstring = sb.ToString();
            symbolsstring = symbolsstring.Substring(0, symbolsstring.LastIndexOf(",", System.StringComparison.Ordinal));
            string debugstring =
                string.Format(
                    "\nAlgorithm Name: {0}\n Symbol: {1}\n Ending Portfolio Value: {2} \n lossThreshhold = {3}\n Start Time: {4}\n End Time: {5}",
                    this.GetType().Name, symbolsstring, Portfolio.TotalPortfolioValue, lossThreshhold, startTime,
                    DateTime.Now);
            Logging.Log.Trace(debugstring);

            #region logging

            NotifyUser();

            //            string filepath = @"I:\MyQuantConnect\Logs\" + symbol + "dailyreturns" + sd + ".csv";
            string filepath = @"I:\MyQuantConnect\Logs\" + symbol + "dailyreturns.csv";
            using (
                StreamWriter sw = new StreamWriter(filepath))
            {
                sw.Write(minuteHeader.ToString());
                sw.Write(minuteReturns.ToString());
                sw.Flush();
                sw.Close();
            }

            SendTradesToFile(symbolsstring + "trades.csv", _orderTransactionProcessor.Trades);

            #endregion
        }

        //private IndicatorDataPoint CalculateNewTrendHistoryValue(int barcount, DateTime time, RollingWindow<IndicatorDataPoint> price, InstantaneousTrend tr)
        //{
        //    //if (barcount < 7 && barcount > 2)
        //    //{
        //    //    return (idp(time, (price[0].Value + 2 * price[1].Value + price[2].Value) / 4));
        //    //}
        //    //else
        //    //{
        //    //    return (idp(time, tr.Current.Value)); //add last iteration value for the cycle
        //    //}
        //    if (!trendHistory.IsReady)
        //        return idp(time, price[0].Value);
        //    return (idp(time, tr.Current.Value)); //add last iteration value for the cycle
        //}

        #endregion

        #region "Logging Methods"
        private void SendTradesToFile(string filename, IList<MatchedTrade> tradelist)
        {
            string filepath = @"I:\MyQuantConnect\Logs\" + filename;
            //string filepath = AssemblyLocator.ExecutingDirectory() + filename;
            if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<MatchedTrade>(",", tradelist);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                    fs.WriteLine(s);
                fs.Flush();
                fs.Close();
            }
        }

        private void SendTransactionsToFile(string filename)
        {
            string filepath = @"I:\MyQuantConnect\Logs\" + filename;
            //string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            //if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<OrderTransaction>(",", _transactions, true);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                {
                    if (!s.Contains("Symbol"))
                        fs.WriteLine(s);
                }
                fs.Flush();
                fs.Close();
            }
        }
        private void SendOrderEventsToFile()
        {
            string filepath = AssemblyLocator.ExecutingDirectory() + "orderEvents.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
            var liststring = CsvSerializer.Serialize<OrderEvent>(",", _orderEvents, true);
            using (StreamWriter fs = new StreamWriter(filepath, true))
            {
                foreach (var s in liststring)
                    fs.WriteLine(s);
                fs.Flush();
                fs.Close();
            }
        }
        #endregion


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
