using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Util;
using QuantConnect.Configuration;

namespace QuantConnect.Algorithm.CSharp
{
    public class IchimokuAlgorithm : QCAlgorithm
    {
        #region "Variables"
        DateTime startTime = DateTime.Now;
        private DateTime _startDate = new DateTime(2015, 8, 10);
        private DateTime _endDate = new DateTime(2015, 8, 14);
        //private DateTime _startDate = new DateTime(2015, 5, 19);
        //private DateTime _endDate = new DateTime(2015, 11, 3);
        private decimal _portfolioAmount = 26000;
        private decimal _transactionSize = 15000;

        private List<Symbol> Symbols;
        private Symbol symbol;

        private int barcount = 0;
        private string sd;      // start date as 20151011
        private string ed;

        private Dictionary<string, BaseStrategy> Strategy = new Dictionary<string, BaseStrategy>();


        #endregion
        #region "Custom Logging"
        private ILogHandler mylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
        private ILogHandler dailylog = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("DailyFileLogHandler");

        private readonly OrderTransactionFactory _orderTransactionFactory;
        private List<OrderTransaction> _transactions;

        private string ondataheader =
            @"Symbol, Time,BarCount,Volume, Open,High,Low,Close,,,Time,Price,Trend, Trigger, orderSignal, Comment,, EntryPrice, Exit Price,Unrealized,Order Id, Owned, TradeNet, Portfolio";
        private string dailyheader = @"Trading Date,Daily Profit, Portfolio Value";
        private string transactionheader = @"Symbol,Quantity,Price,Direction,Order Date,Settlement Date, Amount,Commission,Net,Nothing,Description,Action Id,Order Id,RecordType,TaxLotNumber";
        private List<OrderEvent> _orderEvents = new List<OrderEvent>();
        private int _tradecount = 0;
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

            string filepath = AssemblyLocator.ExecutingDirectory() + "transactions.csv";
            if (File.Exists(filepath)) File.Delete(filepath);
            #endregion


            //Initialize dates
            sd = Config.Get("start-date");
            ed = Config.Get("end-date");

            _startDate = new DateTime(Convert.ToInt32(sd.Substring(0, 4)), Convert.ToInt32(sd.Substring(4, 2)), Convert.ToInt32(sd.Substring(6, 2)));
            _endDate = new DateTime(Convert.ToInt32(ed.Substring(0, 4)), Convert.ToInt32(ed.Substring(4, 2)), Convert.ToInt32(ed.Substring(6, 2)));


            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);
            SetBenchmark(symbol);

            foreach (string sym in Symbols)
            {
                AddSecurity(SecurityType.Equity, sym);
            }


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
        }
        #endregion
    }
}
