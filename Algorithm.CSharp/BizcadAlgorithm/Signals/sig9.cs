
/*  This class adds a parameter list.  Takes up where Sig8 left off. 
 *  The difference it that this class handles the signal for one KeyValuePair<Symbol, TradeBar>
 *    instead of the collection of TradeBars from OnData
 *    
 *  Removes try/catch blocks
 *  
 *  Completely disconnects the Signal from the algorithm.
 *  
 *  There are three kinds of variables in this class:
 *  1. Variables (fields) which are private to the class and used internally.
 *  
 *  2. Variables (properties) which are carried from one Strategy to the next are serialized out.  
 *  At this time the serialized Json is saved in the SignalInfo structure. The serialization is
 *  a two step process.
 *      a. The properties which are to be serialized between instantiations are serialized as typed
 *          data in the GetObjectData method.  This is standard Microsoft serialization.
 *      b. The object data is then serialized to Json in the Serialize method.  I am not sure how,
 *          but under the covers the Serialize method calls the GetObjectData method.  So the variable you
 *          want to save between instantiations must be added to the SerializationInfo object in GetObjectData
 *  Deserialization follows a similar process.
 *      a.  The Deserialize method calls the Serialization constructor attributed with [JsonConstructor]
 *      b.  The constructor assigns the values to the variables (properties) in the constructor.
 *  
 *  3. Variables which are supplied to the Strategy from outside the strategy.  
 *      For example from an indicator (like trend) or Portfolio (like IsLong)
 *      These variables change each time the strategy is run and are in a dictionary of string,string.  
 *      *** Important ***
 *      The names of the keys in the parameter list must match the names of the variable they are to replace
 *      
 *  Nick Stein 10/7/1015
 *  
 *  Possible enhancements:
 *  1.  Add RevPct and RngFac to inputs from the outside (type 3)
 *  
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;


namespace QuantConnect.Algorithm.CSharp
{

    public class Sig9 : ISigSerializable
    {
        #region "fields"

        private bool bReverseTrade = false;
        private decimal RevPct = 1.0015m;
        private decimal RngFac = .35m;
        // private decimal nLimitPrice = 0; // no limit price needed
        //private QCAlgorithm _algorithm;   // no algorithm needed
        //private ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();  // no price calculator needed
        private int period = 4;     // used to size the length of the trendArray

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
        /// It needs to be public because it is Serialized
        /// </summary>
        public int xOver { get; set; }

        /// <summary>
        /// The trigger use in the decision process
        /// Used internally only, not serialized or set from the outside
        /// </summary>
        private decimal nTrig { get; set; }
        /// <summary>
        /// True if the the order was filled in the last trade.  Mostly used after Limit orders
        /// It needs to be public because it is set from the outside by checking the ticket in the Transactions collection
        /// </summary>
        public Boolean orderFilled { get; set; }
        /// <summary>
        /// A flag to disable the trading.  True means make the trade.  This is left over from the 
        /// InstantTrendStrategy where the trade was being made in the strategy.  
        /// </summary>
        //public Boolean maketrade { get; set; }
        /// <summary>
        /// The array used to keep track of the last n trend inputs
        /// It works like a RollingWindow by pushing down the [0] to [1] etc. before updating the [0]
        /// </summary>
        public decimal[] trendArray { get; set; }
        /// <summary>
        /// The bar count from the algorithm
        /// This is set each time through to the barcount in the algorithm
        /// </summary>
        public int Barcount { get; set; }
        /// <summary>
        /// The state of the portfolio.  This is pushed in each time it is run from the Portfolio
        /// it is not Serialized.
        /// </summary>
        public bool IsShort { get; set; }
        /// <summary>
        /// The state of the portfolio.  This is pushed in each time it is run from the Portfolio
        /// </summary>
        public bool IsLong { get; set; }
        /// <summary>
        /// Internal state variables.  This POCO is used to report the internal state of the Signal.
        /// </summary>
        public SigC sigC { get; set; }


        private bool BarcountLT4 { get; set; }
        private bool NTrigLTEP { get; set; }
        private bool NTrigGTEP { get; set; }
        private bool NTrigGTTA0 { get; set; }
        private bool NTrigLTTA0 { get; set; }
        private bool ReverseTrade { get; set; }
        private bool xOverIsPositive { get; set; }
        private bool xOverisNegative { get; set; }
        private bool OrderFilled { get; set; }

        #endregion

        public Sig9()
        {
            trendArray = new decimal[period + 1];       // initialized to 0.  Add a period for Deserialize to make IsReady true
            Id = 9;
            orderFilled = true;
        }
        /// <summary>
        /// Constuctor
        /// </summary>
        /// <param name="symbol">the symbol to track</param>
        [JsonConstructor]
        public Sig9(Symbol _symbol)
        {
            symbol = _symbol;
            orderFilled = true;
            //maketrade = true;
            trendArray = new decimal[period + 1];       // initialized to 0.  Add a period for Deserialize to make IsReady true
            Id = 9;


        }
        #region "Binary Serialization"
        /// <summary>
        /// The custom serializer
        /// </summary>
        /// <param name="info">the bag to put the serialized data into</param>
        /// <param name="context">The stream to store the data</param>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("Symbol", symbol.ToString(), typeof(string));
            info.AddValue("Id", Id, typeof(int));
            info.AddValue("nEntryPrice", nEntryPrice);
            info.AddValue("xOver", xOver);
            //info.AddValue("maketrade", maketrade);
            info.AddValue("trendArray", trendArray, typeof(IndicatorDataPoint[]));
        }
        /// <summary>
        /// The Deserializing constuctor
        /// </summary>
        /// <param name="info">the bag into which the serialized data was put</param>
        /// <param name="context">the stream to get the data from.</param>
        public Sig9(SerializationInfo info, StreamingContext context)
        {
            string s = (string)info.GetValue("Symbol", typeof(string));
            symbol = new Symbol(s);
            Id = (int)info.GetValue("Id", typeof(int));
            nEntryPrice = (decimal)info.GetValue("nEntryPrice", typeof(decimal));
            xOver = (int)info.GetValue("xOver", typeof(int));
            //maketrade = (Boolean)info.GetValue("maketrade", typeof(Boolean));
            trendArray = (decimal[])info.GetValue("trendArray", typeof(decimal[]));

            // ************ Important ***********
            // The initial state of this variable must be reset to true each time the Signal is instantiated
            //  If you do not, Limit Orders never get Signaled and that is very very bad.  Only market orders
            //   does bad things to your returns.
            // **********************************
            orderFilled = true;
        }
        #endregion



        public OrderSignal CheckSignal(KeyValuePair<Symbol, TradeBar> data, Dictionary<string, string> paramlist, out string comment)
        {
            PropertyInfo[] properties = GetType().GetProperties();

            // make sure symbol is set first for getting trendCurrent
            PropertyInfo s = properties.FirstOrDefault(x => x.Name == "symbol");
            if (s != null)
            {
                symbol = new Symbol(paramlist["symbol"], paramlist["symbol"]);
            }
            IndicatorDataPoint trendCurrent = new IndicatorDataPoint(data.Value.EndTime, 0); ;
            string trend = paramlist["trend"];
            trendCurrent.Value = System.Convert.ToDecimal(trend);

            foreach (var item in paramlist)
            {
                PropertyInfo p = properties.FirstOrDefault(x => x.Name == item.Key);
                if (p != null)
                {
                    if (item.Key != "symbol")
                    {
                        {
                            var converter = TypeDescriptor.GetConverter(p.PropertyType);
                            var convertedvalue = converter.ConvertFrom(item.Value);
                            var setmethod = p.SetMethod;
                            if (setmethod != null)
                                p.SetValue(this, convertedvalue);
                        }
                    }
                }
            }
            string current;
            OrderSignal retval = CheckSignal(data, trendCurrent, out current);
            comment = current;
            return retval;
        }

        public OrderSignal CheckSignal(TradeBars data, IndicatorDataPoint trendCurrent, out string comment)
        {
            OrderSignal retval = OrderSignal.doNothing;
            comment = string.Empty;

            UpdateTrendArray(trendCurrent.Value);

            bReverseTrade = false;
            ReverseTrade = false;

            NTrigGTEP = false;
            NTrigLTEP = false;
            NTrigGTTA0 = false;
            NTrigLTTA0 = false;
            BarcountLT4 = false;
            OrderFilled = orderFilled;

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

                #endregion
                #region "selection logic buy/sell"


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

        public OrderSignal CheckSignal(TradeBars data, Dictionary<string, string> paramlist, out string comment)
        {
            PropertyInfo[] properties = GetType().GetProperties();

            // make sure symbol is set first for getting trendCurrent
            PropertyInfo s = properties.FirstOrDefault(x => x.Name == "symbol");
            if (s != null)
            {
                symbol = new Symbol(paramlist["symbol"], paramlist["symbol"]);
            }
            IndicatorDataPoint trendCurrent = new IndicatorDataPoint(data[symbol].EndTime, 0); ;
            string trend = paramlist["trend"];
            trendCurrent.Value = System.Convert.ToDecimal(trend);

            foreach (var item in paramlist)
            {
                PropertyInfo p = properties.FirstOrDefault(x => x.Name == item.Key);
                if (p != null)
                {
                    if (item.Key != "symbol")
                    {
                        {
                            var converter = TypeDescriptor.GetConverter(p.PropertyType);
                            var convertedvalue = converter.ConvertFrom(item.Value);
                            var setmethod = p.SetMethod;
                            if (setmethod != null)
                                p.SetValue(this, convertedvalue);
                        }
                    }
                }
            }
            string current;
            OrderSignal retval = CheckSignal(data, trendCurrent, out current);
            comment = current;
            return retval;
        }

        public OrderSignal CheckSignal(KeyValuePair<Symbol, TradeBar> data, IndicatorDataPoint trendCurrent, out string comment)
        {
            OrderSignal retval = OrderSignal.doNothing;
            comment = string.Empty;

            UpdateTrendArray(trendCurrent.Value);

            bReverseTrade = false;
            ReverseTrade = false;

            NTrigGTEP = false;
            NTrigLTEP = false;
            NTrigGTTA0 = false;
            NTrigLTTA0 = false;
            BarcountLT4 = false;
            OrderFilled = orderFilled;

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
                #endregion
                #region "selection logic buy/sell"

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

            // However the xOver and the Barcount are reset every day
            xOver = 0;
            Barcount = 0;
        }

        /// <summary>
        /// gets the Id of this class
        /// </summary>
        /// <returns>the integer Id</returns>
        public int GetId()
        {
            return Id;
        }

        //public void SetTradesize(int size)
        //{
        //    tradesize = size;
        //}

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
            object v = JsonConvert.DeserializeObject(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

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
        /// <summary>
        /// Gets a data transfer object of the internal state of properties 
        /// </summary>
        /// <returns></returns>
        public SigC GetInternalStateProperties()
        {
            SigC v = new SigC();

            PropertyInfo[] properties = v.GetType().GetProperties();
            foreach (PropertyInfo p in properties)
            {

                PropertyInfo v1 = GetType().GetProperties().FirstOrDefault(n => n.Name == p.Name);

                var setmethod = p.SetMethod;
                if (setmethod != null)
                    p.SetValue(v, v1.GetValue(this));

            }
            return v;
        }
        /// <summary>
        /// Gets a data transfer object of the internal state of properties 
        /// </summary>
        /// <returns></returns>
        public SigC GetInternalStateFields()
        {
            SigC v = new SigC();

            FieldInfo[] fields = v.GetType().GetFields();
            foreach (FieldInfo p in fields)
            {

                FieldInfo v1 = GetType().GetFields().FirstOrDefault(n => n.Name == p.Name);

                if (v1 != null) p.SetValue(v, v1.GetValue(this));
            }
            return v;
        }
        /// <summary>
        /// Gets fields or properties as a csv string
        /// Used in debugging and logging.
        /// </summary>
        /// <returns>the csv string</returns>
        public string ToCsv()
        {
            string ret = string.Empty;

            Dictionary<string, string> list = new Dictionary<string, string>();

            //// Uncommenting these lines will also get public Properties.
            //PropertyInfo[] properties = GetType().GetProperties();
            //foreach (PropertyInfo p in properties)
            //{
            //    var value = p.GetValue(this);

            //    list.Add(p.Name, value == null ? "null" : value.ToString());
            //}

            FieldInfo[] fields = GetType().GetFields();
            foreach (FieldInfo f in fields)
            {
                var value = f.GetValue(this);
                list.Add(f.Name, value == null ? "null" : value.ToString());
            }
            foreach (var s in list)
            {
                ret += s.Key + ":" + s.Value + ",";
            }
            return ret;
        }
        #endregion

        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }

    }
}
