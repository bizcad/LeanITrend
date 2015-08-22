/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/
using System.Collections.Generic;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp.Custom_Algos
{
    public class LaguerreBasic : QCAlgorithm
    {
        public static string[] symbols = { "AIG", "BAC", "IBM", "SPY" };
        /*{ "AAPL", "AMZN", "FB", "GE", "GOOGL", "JNJ", "JPM",
         * "MSFT", "NVS", "PFE", "PG", "PTR", "TM", "VZ", "WFC" };*/

        public static double LaguerreFactor = 0.4;
        Dictionary<string, Laguerre> Strategy = new Dictionary<string, Laguerre>();

        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            // Find more symbols here: http://quantconnect.com/data
            foreach (string symbol in symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);
                Strategy.Add(symbol, new Laguerre(LaguerreFactor));
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
                Strategy[symbol].Add(data[symbol].Price);
                if (Strategy[symbol].Signal != 0)
                {
                    bool longSignal = (Strategy[symbol].Signal == 1) ? true : false;
                    bool shortSignal = (Strategy[symbol].Signal == -1) ? true : false;
                    if (!Portfolio[symbol].HoldStock)
                    {
                        SetHoldings(symbol, Strategy[symbol].Signal * 0.25);
                    }
                    else
                    {
                        if (Portfolio[symbol].IsLong && shortSignal) Liquidate(symbol);
                        if (Portfolio[symbol].IsShort && longSignal) Liquidate(symbol);
                    }
                }
            }
        }
    }
}