using System.Collections.Generic;
using System.Text;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Algorithm
{
    public class TradeBarReporter
    {
        private QCAlgorithm _algorithm;
        private ILogHandler _logHandler;
        private string columnHeader = @"Time,CurrentBar,Open,High,Low,Close";
        private int barcount = 0;
        public Dictionary<string, decimal> ColumnList { get; set; }
        public bool HasPrintedHeading { get; set; }

        /// <summary>
        /// Parameter constructor to inject the algorithm to report from
        /// </summary>
        /// <param name="algorithm">the algorithm running</param>
        public TradeBarReporter(QCAlgorithm algorithm)
        {
            _algorithm = algorithm;
            _logHandler = Composer.Instance.GetExportedValueByTypeName<ILogHandler>("CustomFileLogHandler");
            HasPrintedHeading = false;
        }
        /// <summary>
        /// Prints the report heading
        /// </summary>
        public void ReportHeading(string heading)
        {
            _logHandler.Debug(heading);
            StringBuilder sb = new StringBuilder();
            sb.Append(columnHeader);
            foreach (var item in ColumnList)
            {
                sb.Append(",");
                sb.Append(item.Key);
            }
            _logHandler.Debug(sb.ToString());
        }
        /// <summary>
        /// Logs the OrderEvent Transaction
        /// </summary>
        public void ReportDailyBar(TradeBar tradeBar)
        {
            #region "Print"
            if(!HasPrintedHeading)
            {
                ReportHeading(_algorithm.Name);
                HasPrintedHeading = true;
            }
            StringBuilder sb = new StringBuilder();
            string msg = (string.Format(
                "{0},{1},{2},{3},{4},{5}",
                tradeBar.Time,
                barcount++,
                tradeBar.Open,
                tradeBar.High,
                tradeBar.Low,
                tradeBar.Close
                ));
            sb.Append(msg);
            
            foreach (var item in ColumnList)
            {
                sb.Append(",");
                sb.Append(item.Value);
            }
            _logHandler.Debug(sb.ToString());

            #endregion
        }

    }
}
