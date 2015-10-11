/*  This class implements a serializable Signal Strategy.  Takes up where Sig2 left off. 
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
 * In this version I will add the InstantaneousTrendIndicator back into the Sig.
 * 
 *  Nick Stein 9/22/1015
 */
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;

using QuantConnect.Data.Market;
using QuantConnect.Indicators;


namespace QuantConnect.Algorithm.CSharp
{
    [Serializable()]
    public class Sig4 
    {
        #region "fields"

        private bool bReverseTrade = false;
        private decimal RevPct = 1.0015m;
        private decimal RngFac = .35m;
        private decimal nLimitPrice = 0;
        private QCAlgorithm _algorithm;
        private ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
        private InstantaneousTrend trend;
        private int period = 7;
        private int tradesize;
        #endregion


        #region "Properties"
        /// <summary>
        /// The symbol being processed
        /// </summary>
        public Symbol symbol { get; set; }
        /// <summary>
        /// The unique id assigned in the Constructor
        /// </summary>
        public int Id { get; private set; }
        /// <summary>
        /// The entry price from the last trade
        /// </summary>
        public decimal nEntryPrice { get; set; }
        /// <summary>
        /// The entry price from the last trade
        /// </summary>
        public int xOver = 0;
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
        #endregion

        /// <summary>
        /// Constuctor
        /// </summary>
        /// <param name="symbol">the symbol to track</param>
        public Sig4(Symbol _symbol)
        {
            symbol = _symbol;
            orderFilled = true;
            maketrade = true;
            trendArray = new decimal[] { 0, 0, 0 };
            Id = 4;
            trend = new InstantaneousTrend("InSig", period, 2.0m / ((decimal)period + 1.0m));
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
                info.AddValue("nEntryPrice", nEntryPrice);
                info.AddValue("xOver", xOver);
                info.AddValue("nTrig", nTrig);
                info.AddValue("orderFilled", orderFilled);
                info.AddValue("maketrade", maketrade);
                info.AddValue("trendArray", trendArray, typeof(decimal[]));
                info.AddValue("Symbol", symbol.ToString(), typeof(string));
                info.AddValue("Id", Id, typeof(int));
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
        public Sig4(SerializationInfo info, StreamingContext context)
        {
            string s = (string)info.GetValue("Symbol", typeof(string));
            symbol = new Symbol(s);
            Id = (int)info.GetValue("Id", typeof(int));
            nEntryPrice = (decimal)info.GetValue("nEntryPrice", typeof(decimal));
            xOver = (int)info.GetValue("xOver", typeof(int));
            nTrig = (decimal)info.GetValue("nTrig", typeof(decimal));
            orderFilled = (Boolean)info.GetValue("orderFilled", typeof(Boolean));
            maketrade = (Boolean)info.GetValue("maketrade", typeof(Boolean));
            trendArray = (decimal[])info.GetValue("trendArray", typeof(decimal[]));
            RevPct = 1.0015m;
            RngFac = .35m;
        }
        #endregion

        public OrderSignal CheckSignal(TradeBars data, IndicatorDataPoint trendCurrent, out string comment)
        {
            OrderSignal retval = OrderSignal.doNothing;
            if (Barcount == 1)
                comment = "";
            decimal price = (data[symbol].Close + data[symbol].Open) / 2;
            trend.Update(idp(data[symbol].EndTime, price));
            if (trendCurrent.Value != trend.Current.Value) 
                comment = "not equal";
            UpdateTrendArray(trend.Current);


            comment = "";
            bReverseTrade = false;

            if (Barcount < 4)
            {
                comment = "Trend Not Ready";
                return OrderSignal.doNothing;
            }

            #region "Selection Logic Reversals"

            try
            {
                nTrig = 2 * trendArray[0] - trendArray[2]; // Note this is backwards from a RollingWindow
                if (IsLong && nTrig < (Math.Abs(nEntryPrice) / RevPct))
                {
                    retval = OrderSignal.revertToShort;
                    bReverseTrade = true;
                    comment =
                        string.Format("{0} nTrig {1} < (nEntryPrice {2} * RevPct{3}) orderFilled {4})",
                            retval,
                            Math.Round(nTrig, 4),
                            nEntryPrice,
                            RevPct,
                            orderFilled);

                }
                else
                {
                    if (IsShort && nTrig > (Math.Abs(nEntryPrice) * RevPct))
                    {
                        retval = OrderSignal.revertToLong;
                        bReverseTrade = true;
                        comment = string.Format("{0} nTrig {1} > (nEntryPrice {2} * RevPct{3}) orderFilled {4})",
                            retval,
                            Math.Round(nTrig, 4),
                            nEntryPrice,
                            RevPct,
                            orderFilled);

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

                        if (xOver == -1 && !IsLong)
                        {
                            if (!orderFilled)
                            {
                                retval = OrderSignal.goLong;
                                comment =
                                    string.Format(
                                        "{0} IsLong && nTrig {1} > trend {2} xOver {3} orderFilled {4}",
                                        retval,
                                        Math.Round(nTrig, 4),
                                        Math.Round(trendArray[0], 4),
                                        xOver,
                                        orderFilled);
                            }
                            else
                            {
                                retval = OrderSignal.goLongLimit;
                                comment =
                                    string.Format(
                                        "{0} IsLong && nTrig {1} > trend {2} xOver {3}",
                                        retval,
                                        Math.Round(nTrig, 4),
                                        Math.Round(trendArray[0], 4),
                                        xOver);

                            }
                        }
                        if (comment.Length == 0)
                            comment = "Trigger over trend - setting xOver to 1";
                        xOver = 1;
                    }
                    else
                    {
                        if (nTrig < trendArray[0])
                        {
                            if (xOver == 1 && !IsShort)
                            {
                                if (!orderFilled)
                                {
                                    retval = OrderSignal.goShort;
                                    comment =
                                        string.Format(
                                            "{0} nTrig {1} < trend {2} xOver {3} orderFilled {4}",
                                            retval,
                                            Math.Round(nTrig, 4),
                                            Math.Round(trendArray[0], 4),
                                            xOver,
                                            orderFilled);

                                }
                                else
                                {
                                    retval = OrderSignal.goShortLimit;
                                    comment =
                                        string.Format(
                                        "{0}  nTrig {1} < trend {2} xOver {3}",
                                        retval,
                                        Math.Round(nTrig, 4),
                                        Math.Round(trendArray[0], 4),
                                        xOver);

                                }
                            }
                            if (comment.Length == 0)
                                comment = "Trigger under trend - setting xOver to -1";
                            xOver = -1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            #endregion


            return retval;
        }

        private void UpdateTrendArray(IndicatorDataPoint trendCurrent)
        {
            trendArray[2] = trendArray[1];
            trendArray[1] = trendArray[0];
            trendArray[0] = trendCurrent.Value;
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
        public string Serialize()
        {
            string json = JsonConvert.SerializeObject(this);
            return json;
        }
        public void Deserialize(string json)
        {
            var v = JsonConvert.DeserializeObject(json, GetType());
            if (v!=null)
            {
                PropertyInfo[] properties = GetType().GetProperties();
                foreach (PropertyInfo p in properties)
                {
                    try
                    {
                        PropertyInfo v1 = v.GetType().GetProperties().FirstOrDefault(n => n.Name == p.Name);
                        if (v1 != null) p.SetValue(this, v1.GetValue(v));
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                    }
                }
            }
        }
        #endregion

        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }


    }
}
