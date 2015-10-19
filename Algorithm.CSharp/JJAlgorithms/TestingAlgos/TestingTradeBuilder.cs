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

using QuantConnect.Data;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Basic template algorithm simply initializes the date range and cash
    /// </summary>
    public class TestingTradeBuilder : QCAlgorithm
    {
        string symbol = "SPY";
        int closeTradeCounter = 0;

        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 8);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (Time.Minute % 60 == 0)
            {
                if (!Portfolio[symbol].Invested) MarketOrder(symbol, 10);
                else MarketOrder(symbol, -10);
            }
        }

        public override void OnOrderEvent(Orders.OrderEvent orderEvent)
        {
            if ((orderEvent.Direction == Orders.OrderDirection.Sell) &&
                (orderEvent.Status == Orders.OrderStatus.Filled))
            {
                Log(string.Format("Trade {0} close at {1}", closeTradeCounter, Time));
                closeTradeCounter++;
            }
        }

        public override void OnEndOfAlgorithm()
        {
            int i = 0;
            foreach (var trade in TradeBuilder.ClosedTrades)
            {
                Log(string.Format("Trade {0} closed the {1} at {2}",
                    i,
                    trade.ExitTime.ToShortDateString(),
                    trade.ExitTime.ToShortTimeString()));
                i++;
            }
        }
    }
}