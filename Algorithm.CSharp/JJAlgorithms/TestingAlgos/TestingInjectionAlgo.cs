using QuantConnect.Algorithm;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Data.Market;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Indicators;


using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using NodaTime;
using System;

namespace QuantConnect
{
    public partial class TestingInjectionAlgo : QCAlgorithm
    {
        #region Fields
        bool flag = true;

        private static string[] Symbols = { "AIG", "BAC", "IBM", "SPY" };

        private Dictionary<string, NoStrategy> StrategyDict = new Dictionary<string, NoStrategy>();

        private Dictionary<string, StringBuilder> stockLogging = new Dictionary<string, StringBuilder>();

        #endregion Fields

        #region QCAlgorithm Methods

        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 11);
            SetCash(250000);

            foreach (var symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Second);
                // Define an Identity Indicator with the close price.
                Identity priceIdentityIndicator = new Identity(symbol + "PriceIdentityIndicator");
                RegisterIndicator(symbol, priceIdentityIndicator, Resolution.Second, Field.Close);
                // Define an EMA.
                ExponentialMovingAverage EMA = new ExponentialMovingAverage("EMA_" + symbol, 100);
                RegisterIndicator(symbol, EMA, Resolution.Minute, Field.Close);
                // Inject the Price Identity indicator and the EMA in the Strategy object.
                StrategyDict.Add(symbol, new NoStrategy(symbol, priceIdentityIndicator, EMA));

                stockLogging.Add(symbol, new StringBuilder());
            }
        }

        public void OnData(Slice data)
        {
            foreach (string symbol in Symbols)
            {
                stockLogging[symbol].AppendLine(string.Format("{0},{1},{2}",
                    Time.ToString(CultureInfo.DefaultThreadCurrentCulture),
                    StrategyDict[symbol].Price.Current.Value,
                    StrategyDict[symbol].Trend.Current.Value
                    ));
                
                if (Time.Minute % 45 == 0 && Time.Second == 0)
                {
                    if (Portfolio[symbol].Invested)
                    {
                        MarketOrder(symbol, -10);
                    }
                    else
                    {
                        MarketOrder(symbol, 10);
                    }
                }
            }

        }

        public override void OnOrderEvent(Orders.OrderEvent orderEvent)
        {
            Log(orderEvent.ToString());
        }

        public override void OnEndOfDay()
        {
            foreach (string symbol in Symbols)
            {
                StrategyDict[symbol].Reset();
            }
        }

        public override void OnEndOfAlgorithm()
        {
            foreach (string symbol in Symbols)
            {
                string filename = string.Format("InjectingTest_{0}.csv", symbol);
                string filePath = AssemblyLocator.ExecutingDirectory() + filename;
                if (File.Exists(filePath)) File.Delete(filePath);
                File.AppendAllText(filePath, stockLogging[symbol].ToString());
            }
        }

        #endregion QCAlgorithm Methods
    }
}