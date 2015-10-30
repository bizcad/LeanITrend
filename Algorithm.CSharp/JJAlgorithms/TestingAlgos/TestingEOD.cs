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
    public class TestingEOD : QCAlgorithm
    {
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            AddSecurity(SecurityType.Equity, "SPY", Resolution.Minute);

            Schedule.Event("MarketOpen")
                .EveryDay()
                .AfterMarketOpen("SPY")
                //.At(9, 30)
                .Run(() =>
                {
                    Log(string.Format("vvv {0} Market Open vvv", Time.DayOfWeek));
                });
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">Slice object keyed by symbol containing the stock data</param>
        public override void OnData(Slice data)
        {
            if (Time.Minute % 120 == 0) Log("Doing important stuff...");
        }

        public override void OnEndOfDay(string symbol)
        {
            Log(string.Format("^^^ {0} Market Close ^^^", Time.DayOfWeek));
        }
    }
}