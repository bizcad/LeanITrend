using System;
using System.Linq;
using System.Collections.Generic;
using QuantConnect.Indicators;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;

namespace QuantConnect.Algorithm.CSharp.IntradayBuyLow
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash
    /// </summary>
    public partial class IntradayBuyLow : QCAlgorithm
    {
        private int previousDays = 5;
        private int runsPerDay = 20;
        
        private int HMA_Period = 4;

        private decimal maxLeverage = 3m;
        private decimal leverageBuffer = 0.2m;
        
        private decimal leverage;
        private decimal maxLeverageValue = 0m;

        bool isMarketOpen = true;

        public static string[] symbols = { "IBM", "SPY" }; /*{ "AAPL", "AMZN", "FB", "GE", "GOOGL", "JNJ", "JPM", "MSFT",
                                             "NVS", "PFE", "PG", "PTR", "TM", "VZ", "WFC" };*/

        public Dictionary<string, BuyLowStrategy> Strategy = new Dictionary<string, BuyLowStrategy>();
        
        #region Share size
        /*public Dictionary<string, decimal> stockShareSize = new Dictionary<string, decimal>()
        {
            {"AAPL",  0.0268808004465086964875867853m},
            {"AMZN",  0.0284105472150871579422202126m},
            {"FB",    0.0306514529936117601276322134m},
            {"GE",    0.0268808004465086964875867853m},
            {"GOOGL", 0.8361228141074589339991876974m},
            {"JNJ",   0.0268808004465086964875867853m},
            {"JPM",   0.0268808004465086964875867853m},
            {"MSFT",  0.0353489342329171353539834940m},
            {"NVS",   0.0268808004465086964875867853m},
            {"PFE",   0.0275923179117485217187767271m},
            {"PG",    0.0268808004465086964875867853m},
            {"PTR",   1.1602970385264010880158079282m},
            {"TM",    0.2000760726268714143378924804m},
            {"VZ",    0.1464682555368547445190284709m},
            {"WFC",   0.0737477641699970650599500645m}
        };*/
        #endregion
        
        EquityExchange Market = new EquityExchange();
        Dictionary<string, HullMovingAverage> hma = new Dictionary<string, HullMovingAverage>();
        //Dictionary<string, ExponentialMovingAverage[]> ema = new Dictionary<string, ExponentialMovingAverage[]>();



        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);   //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            
            foreach (string symbol in symbols)
            {
                Strategy.Add(symbol, new BuyLowStrategy(previousDays, runsPerDay));
                previousStockProfit.Add(symbol, 0m);
                actualStockProfit.Add(symbol, 0m);

                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute, true, maxLeverage, false);
                
                
                //tradier does 1 dollar equity trades
                Securities[symbol].TransactionModel = new ConstantFeeTransactionModel(1.00m);


                HullMovingAverage HMA = new HullMovingAverage("myHull", 4);
                Func<BaseData, decimal> selector = null;
                RegisterIndicator(symbol, HMA, Resolution.Minute, selector);

                hma.Add(symbol, HMA);

                
                stockShareSize.Add(symbol, maxLeverage / symbols.Length);
                
            }
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            foreach (string symbol in symbols)
            {
                if (!hma[symbol].IsReady) return;

                Strategy[symbol].AddSerieValue(Time, hma[symbol]);

                isMarketOpen = Market.DateTimeIsOpen(Time) && Market.DateTimeIsOpen(Time.AddMinutes(10));

                if (isMarketOpen)
                {
                    if (Strategy[symbol].IsReady)
                    {
                        // If I have stocks and there's a turn around, then liquidate
                        if (Portfolio[symbol].HoldStock && Strategy[symbol].TurnAround)
                        {
                            Liquidate(symbol);
                        }
                        // If I don't have stocks and there's a signal, then operate.
                        else if (!Portfolio[symbol].HoldStock && Strategy[symbol].OrderSignal != 0)
                        {
                            EntryAndSetStopLoss(symbol, Strategy[symbol].OrderSignal);
                        }
                    }
                }
                // If have stocks and the market is about ot close (15 minutes earlier), liquidate.
                else if (Portfolio[symbol].HoldStock) Liquidate(symbol);
            }
            leverage = Portfolio.TotalHoldingsValue / Portfolio.TotalPortfolioValue;
            if (leverage > maxLeverageValue)
            {
                if (leverage > maxLeverage)
                {
                    Log("Leverage exceeds max allowed leverage");
                    leverageBuffer *= 0.975m;
                }
                maxLeverageValue = leverage;
            }
        }


        private void EntryAndSetStopLoss(string symbol, int signal)
        {
            SetHoldings(symbol, stockShareSize[symbol] * -1); // Constanza
        }

        
        public override void OnEndOfDay(string symbol)
        {
            Log(symbol + Portfolio[symbol].Profit);
            switch (frequency)
            {
                case RebalanceFrequency.Daily:
                    RebalanceOrderSizes(symbol);
                    break;
                case RebalanceFrequency.Weekly:
                    if (Time.DayOfWeek == DayOfWeek.Tuesday) RebalanceOrderSizes(symbol);
                    break;
                case RebalanceFrequency.Monthly:
                    if (Time.Day == 1) RebalanceOrderSizes(symbol);
                    break;
            }
        }

        public override void OnEndOfAlgorithm()
        {
            base.OnEndOfAlgorithm();
            foreach (string symbol in symbols)
            {
                Log("//" + symbol);
                Log(symbol + Strategy[symbol].ToString());
                Log(symbol + " share size " + stockShareSize[symbol]);
            }

        }
    }
}