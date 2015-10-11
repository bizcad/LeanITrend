/*  This class implements a serializable Signal Strategy.  Takes up where Sig4 left off. 
 * There are two kinds of serialization implemented
 * Binary with thanks to http://www.codeproject.com/Articles/1789/Object-Serialization-using-C which shows how to serialize to a file
 *  
 * Json which uses Json.Net
 *    It also has some limitations and advantages
 *      It produces a readable string
 *      It cannot serialize a RollingWindow.  It is better to use basic data types, thus the use of the trendArray
 *      It serializes the properties, but not the fields (all public variables)
 *       
 * Some assumptions:
 *  1.  The Id is set  each time it is constructed.   So if you are going to deserialize between instances, make sure to include the Id.
 *  
 *  In this version I attempted to save state between calls to CheckSignal.  IT FAILED.  The returns went from 1135% in 96 days to 928%
 *      The reason it failed as the Deserialize had to repopulate the InstantaneousTrend each time and the history got messed up
 *      The trend.Current did not match the trendCurrent coming in from the algorithm.  A continuous trend works better.
 *      
 *  Nick Stein 9/27/1015
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Policy;
using System.Text;
using Microsoft.Win32;
using Newtonsoft.Json;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;


namespace QuantConnect.Algorithm.CSharp
{

    public class Sig6 : ISigSerializable
    {
        #region "fields"

        private bool bReverseTrade = false;
        private decimal RevPct = 1.0015m;
        private decimal RngFac = .35m;
        private decimal nLimitPrice = 0;
        //private QCAlgorithm _algorithm;
        private ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
        private int period = 14;
        private int tradesize;
        #endregion


        #region "Properties"
        /// <summary>
        /// The symbol being processed
        /// </summary>
        public Symbol symbol { get; set; }

        public int Id { get; set; }
        /// <summary>
        /// The entry price from the last trade, set from the outside
        /// </summary>
        public decimal nEntryPrice { get; set; }

        /// <summary>
        /// The crossover carried from one instance to the next via serialization
        /// set to 1 when (nTrig Greater Than the current trend)
        /// set to -1 when (nTrig less than the current trend)
        /// </summary>
        public int xOver { get; set; }

        /// <summary>
        /// The trigger use in the decision process
        /// </summary>
        public decimal nTrig { get; set; }
        /// <summary>
        /// True if the the order was filled in the last trade.  Mostly used after Limit orders
        /// </summary>
        public Boolean orderFilled { get; set; }
        /// <summary>
        /// A flag to disable the trading.  True means make the trade.  This is left over from the 
        /// InstantTrendStrategy where the trade was being made in the strategy.  
        /// </summary>
        public Boolean maketrade { get; set; }
        /// <summary>
        /// The array used to keep track of the last n trend inputs
        /// </summary>
        public decimal[] trendArray { get; set; }
        /// <summary>
        /// The bar count from the algorithm
        /// </summary>
        public int Barcount { get; set; }
        /// <summary>
        /// The state of the portfolio.
        /// </summary>
        public bool IsShort { get; set; }
        /// <summary>
        /// The state of the portfolio.
        /// </summary>
        public bool IsLong { get; set; }


        public bool BarcountLT4 { get; set; }
        public bool NTrigLTEP { get; set; }
        public bool NTrigGTEP { get; set; }
        public bool NTrigGTTA0 { get; set; }
        public bool NTrigLTTA0 { get; set; }
        public bool ReverseTrade { get; set; }
        public bool xOverIsPositive { get; set; }
        public bool xOverisNegative { get; set; }
        public bool OrderFilled { get; set; }

        //public IndicatorDataPoint[] priceArray { get; set; }

        //private ExponentialMovingAverage ema { get; set; }


        #endregion

        public Sig6()
        {
            trendArray = new decimal[period + 1];       // initialized to 0.  Add a period for Deserialize to make IsReady true
            Id = 6;
        }
        /// <summary>
        /// Constuctor
        /// </summary>
        /// <param name="symbol">the symbol to track</param>
        [JsonConstructor]
        public Sig6(Symbol _symbol)
        {
            symbol = _symbol;
            orderFilled = true;
            maketrade = true;
            trendArray = new decimal[period + 1];       // initialized to 0.  Add a period for Deserialize to make IsReady true
            //priceArray = new IndicatorDataPoint[10];
            Id = 6;
            //            trend = new InstantaneousTrend("InSig", period, 2.0m / ((decimal)period + 1.0m));
            //trend = new InstantaneousTrend("InSig", period, .25m);
            //ema = new ExponentialMovingAverage(6, .9m);

        }
        #region "Binary Serialization"
        /// <summary>
        /// The custom serializer
        /// </summary>
        /// <param name="info">the bag to put the serialized data into</param>
        /// <param name="context">The stream to store the data</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            try
            {
                info.AddValue("Symbol", symbol.ToString(), typeof(string));
                info.AddValue("Id", Id, typeof(int));
                info.AddValue("nEntryPrice", nEntryPrice);
                info.AddValue("xOver", xOver);
                info.AddValue("maketrade", maketrade);
                info.AddValue("trendArray", trendArray, typeof(IndicatorDataPoint[]));
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        /// <summary>
        /// The Deserializing constuctor
        /// </summary>
        /// <param name="info">the bag into which the serialized data was put</param>
        /// <param name="context">the stream to get the data from.</param>
        public Sig6(SerializationInfo info, StreamingContext context)
        {
            string s = (string)info.GetValue("Symbol", typeof(string));
            symbol = new Symbol(s);
            Id = (int)info.GetValue("Id", typeof(int));
            nEntryPrice = (decimal)info.GetValue("nEntryPrice", typeof(decimal));
            xOver = (int)info.GetValue("xOver", typeof(int));
            maketrade = (Boolean)info.GetValue("maketrade", typeof(Boolean));
            trendArray = (decimal[])info.GetValue("trendArray", typeof(decimal[]));
        }
        #endregion

        public OrderSignal CheckSignal(TradeBars data, IndicatorDataPoint trendCurrent, out string comment)
        {
            OrderSignal retval = OrderSignal.doNothing;
            comment = string.Empty;
            //decimal price = (data[symbol].Close + data[symbol].Open) / 2;

            // Here is an attempt to smooth the price
            //ema.Update(idp(data[symbol].EndTime, price));
            //if (ema.IsReady)
            //    UpdatePriceArray(ema.Current);
            //else
            //    UpdatePriceArray(idp(data[symbol].EndTime, price));
            //if (Barcount == 17)
            //    comment = "";
            //// Update the trend with the smoothed price
            //trend.Update(ema.Current);
            //trend.Current.Symbol = data[symbol].Symbol; // make sure the symbol is correct

            //if (trend.Current.Value != trendCurrent.Value)
            //    comment = "trends not equal";
            UpdateTrendArray(trendCurrent.Value);

            bReverseTrade = false;
            ReverseTrade = false;

            NTrigGTEP = false;
            NTrigLTEP = false;
            NTrigGTTA0 = false;
            NTrigLTTA0 = false;
            BarcountLT4 = false;

            if (Barcount < 4)
            {
                BarcountLT4 = true;
                comment = "Barcount < 4";
                retval = OrderSignal.doNothing;
            }
            else
            {
                nTrig = 2m * trendArray[0] - trendArray[2];


                #region "Selection Logic Reversals"

                try
                {

                    if (nTrig < (Math.Abs(nEntryPrice) / RevPct))
                    {
                        NTrigLTEP = true;
                        if (IsLong)
                        {
                            retval = OrderSignal.revertToShort;
                            bReverseTrade = true;
                            ReverseTrade = true;
                            comment =
                                string.Format("nTrig {0} < (nEntryPrice {1} * RevPct {2}) {3} IsLong {4} )",
                                    Math.Round(nTrig, 4),
                                    nEntryPrice,
                                    RevPct,
                                    NTrigLTEP,
                                    IsLong);
                        }
                        else
                        {
                            NTrigLTEP = false;
                        }
                    }
                    else
                    {
                        if (nTrig > (Math.Abs(nEntryPrice) * RevPct))
                        {
                            NTrigGTEP = true;
                            if (IsShort)
                            {
                                retval = OrderSignal.revertToLong;
                                bReverseTrade = true;
                                ReverseTrade = true;
                                comment =
                                    string.Format("nTrig {0} > (nEntryPrice {1} * RevPct {2}) {3} IsLong {4} )",
                                        Math.Round(nTrig, 4),
                                        nEntryPrice,
                                        RevPct,
                                        NTrigLTEP,
                                        IsLong);
                            }
                            else
                            {
                                NTrigGTEP = false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }

                #endregion
                #region "selection logic buy/sell"

                try
                {
                    if (!bReverseTrade)
                    {
                        if (nTrig > trendArray[0])
                        {
                            NTrigGTTA0 = true;
                            if (xOver == -1)
                            {
                                #region "If Not Long"
                                if (!IsLong)
                                {

                                    if (!orderFilled)
                                    {
                                        OrderFilled = false;
                                        retval = OrderSignal.goLong;
                                        comment =
                                            string.Format(
                                                "nTrig {0} > trend {1} xOver {2} !IsLong {3} !orderFilled {4}",
                                                Math.Round(nTrig, 4),
                                                Math.Round(trendArray[0], 4),
                                                xOver,
                                                !IsLong,
                                                !orderFilled);
                                    }
                                    else
                                    {
                                        retval = OrderSignal.goLongLimit;
                                        comment =
                                            string.Format(
                                                "nTrig {0} > trend {1} xOver {2} !IsLong {3} !orderFilled {4}",
                                                Math.Round(nTrig, 4),
                                                Math.Round(trendArray[0], 4),
                                                xOver,
                                                !IsLong,
                                                !orderFilled);

                                    }
                                }
                                #endregion
                            }

                            if (comment.Length == 0)
                                comment = "Trigger over trend - setting xOver to 1";
                            xOver = 1;
                            xOverisNegative = xOver < 0;
                            xOverIsPositive = xOver > 0;
                        }
                        else
                        {
                            if (nTrig < trendArray[0])
                            {
                                NTrigLTTA0 = true;
                                if (xOver == 1)
                                {
                                    #region "If Not Short"
                                    if (!IsShort)
                                        {
                                            if (!orderFilled)
                                            {
                                                OrderFilled = false;
                                                retval = OrderSignal.goShort;
                                                comment =
                                                    string.Format(
                                                        "nTrig {0} < trend {1} xOver {2} !isShort {3} orderFilled {4}",
                                                        Math.Round(nTrig, 4),
                                                        Math.Round(trendArray[0], 4),
                                                        xOver,
                                                        !IsShort,
                                                        !orderFilled);

                                            }
                                            else
                                            {
                                                retval = OrderSignal.goShortLimit;
                                                comment =
                                                    string.Format(
                                                        "nTrig {0} < trend {1} xOver {2} !isShort {3} orderFilled {4}",
                                                        Math.Round(nTrig, 4),
                                                        Math.Round(trendArray[0], 4),
                                                        xOver,
                                                        !IsShort,
                                                        !orderFilled);

                                            }
                                        }
                                    #endregion
                                }
                                if (comment.Length == 0)
                                    comment = "Trigger under trend - setting xOver to -1";
                                xOver = -1;
                                xOverisNegative = xOver < 0;
                                xOverIsPositive = xOver > 0;
                            }



                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }

                #endregion
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(comment);
            sb.Append(",");
            sb.Append(retval.ToString());
            sb.Append(",");
            //sb.Append(ToInt32());
            sb.Append(",");
            //sb.Append(ToIntCsv());
            comment = sb.ToString();
            return retval;
        }

        public OrderSignal CheckSignal(TradeBars data, Dictionary<string, string> paramlist, out string current)
        {
            throw new NotImplementedException();
        }

        public OrderSignal CheckSignal(KeyValuePair<Symbol, TradeBar> data, IndicatorDataPoint trendCurrent, out string current)
        {
            throw new NotImplementedException();
        }

        public OrderSignal CheckSignal(KeyValuePair<Symbol, TradeBar> data, Dictionary<string, string> paramlist, out string current)
        {
            throw new NotImplementedException();
        }

        //private void UpdatePriceArray(IndicatorDataPoint priceCurrent)
        //{
        //    for (int i = priceArray.Length - 2; i >= 0; i--)
        //    {
        //        priceArray[i + 1] = priceArray[i];
        //    }
        //    priceArray[0] = priceCurrent;
        //}

        private void UpdateTrendArray(decimal trendCurrent)
        {
            for (int i = trendArray.Length - 2; i >= 0; i--)
            {
                trendArray[i + 1] = trendArray[i];
            }
            trendArray[0] = trendCurrent;
        }
        public void Reset()
        {
            // resetting the trend decreased profit by half.  

            //trendArray[2] = 0;
            //trendArray[1] = 0;
            //trendArray[0] = 0;
            xOver = 0;
            Barcount = 0;
        }

        public int GetId()
        {
            return Id;
        }

        public void SetTradesize(int size)
        {
            tradesize = size;
        }

        #region "Json serialization"
        /// <summary>
        /// CAlls GetObjectData and converts the output to typed json
        /// </summary>
        /// <returns>the json string</returns>
        public string Serialize()
        {
            // SerializeObject (as opposed to just Serialize) adds some type data for the JsonConstructor
            //  It calls the GetObjectData under the covers to get the items to serialize.
            string jsonTypeNameAll = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            
            return jsonTypeNameAll;
        }

        /// <summary>
        /// Sets properties in this object to the values in the json string by calling the Deserializing constructor
        /// </summary>
        /// <param name="json">the json string</param>
        public void Deserialize(string json)
        {
            try
            {
                object v = JsonConvert.DeserializeObject(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                if (v != null)
                {
                    PropertyInfo[] properties = GetType().GetProperties();
                    foreach (PropertyInfo p in properties)
                    {
                        PropertyInfo v1 = GetType().GetProperties().FirstOrDefault(n => n.Name == p.Name);
                        if (v1 != null)
                        {
                            p.SetValue(this, v1.GetValue(v));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }

        public SigC GetInternalStateProperties()
        {
            throw new NotImplementedException();
        }

        public SigC GetInternalStateFields()
        {
            throw new NotImplementedException();
        }

        public SigC GetInternalState()
        {
            throw new NotImplementedException();
        }

        public string ToCsv()
        {
            throw new NotImplementedException();
        }

        #endregion

        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

    }
}
