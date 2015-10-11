/*  This class implements a serializable signal strategy 
 * There are two kinds of serialization implemented
 * Binary with thanks to http://www.codeproject.com/Articles/1789/Object-Serialization-using-C which shows how to serialize to a file
 *   It has some limitations 
 *      RollingWindow cannot be serialized because on of it's properties,  private readonly ReaderWriterLockSlim _listLock, is not serializable.
 *      QCAlgorithm is not serializable because somewhere deep inside it is something not serializable.  So you have to use Property Injection.
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
    
    public class Sig2 
    {
        #region "fields"

        private bool bReverseTrade = false;
        private Symbol _symbol { get; set; }
        private decimal RevPct = 1.0015m;
        private decimal RngFac = .35m;
        private decimal nLimitPrice = 0;
        private int nStatus = 0;
        private RollingWindow<decimal> trendHistory { get; set; }
        private QCAlgorithm _algorithm;
        private ILimitPriceCalculator priceCalculator = new InstantTrendLimitPriceCalculator();
        #endregion

        #region "Properties"
        /// <summary>
        /// The unique id assigned in the Constructor
        /// </summary>
        public int Id { get; private set; }
        /// <summary>
        /// The entry price from the last trade
        /// </summary>
        public decimal nEntryPrice { get; set; }
        public int xOver = 0;
        public decimal nTrig { get; set; }
        public Boolean orderFilled { get; set; }
        public Boolean maketrade { get; set; }
        public decimal[] trendArray { get; set; }
        public int Barcount { get; set; }
        public bool IsShort { get; set; }
        public bool IsLong { get; set; }
        private int tradesize;
        #endregion

        public Sig2(Symbol symbol, int period, QCAlgorithm algorithm)
        {
            _symbol = symbol;
            trendHistory = new RollingWindow<decimal>(3);
            _algorithm = algorithm;
            orderFilled = true;
            maketrade = true;
            trendArray = new decimal[] { 0, 0, 0 };
            Id = 2;
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
                // Cannot serialize the _listLock
                //info.AddValue("trendHistory", trendHistory, typeof(RollingWindow<decimal>));
                info.AddValue("nEntryPrice", nEntryPrice);
                info.AddValue("xOver", xOver);
                info.AddValue("nTrig", nTrig);
                info.AddValue("orderFilled", orderFilled);
                info.AddValue("maketrade", maketrade);
                info.AddValue("trendArray", trendArray, typeof(decimal[]));
                info.AddValue("Symbol", _symbol.ToString(), typeof(string));
                //info.AddValue("algorithm",_algorithm,typeof(QCAlgorithm));
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
        public Sig2(SerializationInfo info, StreamingContext context)
        {
            string s = (string)info.GetValue("Symbol", typeof(string));
            _symbol = new Symbol(s);
            //_algorithm = (QCAlgorithm)info.GetValue("algorithm", typeof(QCAlgorithm));
            Id = (int)info.GetValue("Id", typeof(int));

            trendHistory = new RollingWindow<decimal>(3);
            nEntryPrice = (decimal)info.GetValue("nEntryPrice", typeof(decimal));
            xOver = (int)info.GetValue("xOver", typeof(int));
            nTrig = (decimal)info.GetValue("nTrig", typeof(decimal));
            orderFilled = (Boolean)info.GetValue("orderFilled", typeof(Boolean));
            maketrade = (Boolean)info.GetValue("maketrade", typeof(Boolean));
            trendArray = (decimal[])info.GetValue("trendArray", typeof(decimal[]));
            foreach (decimal t in trendArray)
            {
                trendHistory.Add(t);
            }
            RevPct = 1.0015m;
            RngFac = .35m;
        }
        #endregion

        public OrderSignal CheckSignal(TradeBars data, IndicatorDataPoint trendCurrent, out string comment)
        {
            OrderSignal retval = OrderSignal.doNothing;

            comment = "";
            UpdateTrendArray(trendCurrent);
            trendHistory.Add(trendCurrent);
            nStatus = 0;
            if (_algorithm.Portfolio[_symbol].IsLong) nStatus = 1;
            if (_algorithm.Portfolio[_symbol].IsShort) nStatus = -1;

            if (Barcount < 4)
            {
                comment = "Trend Not Ready";
                return OrderSignal.doNothing;
            }

            bReverseTrade = false;
            try
            {
                nTrig = 2 * trendArray[0] - trendArray[2];   // Note this is backwards from a RollingWindow
                if (_algorithm.Portfolio[_symbol].IsLong && nTrig < (Math.Abs(nEntryPrice) / RevPct))
                {
                    retval = OrderSignal.revertToShort;
                    bReverseTrade = true;
                    comment =
                        string.Format("{0} nStatus == {1} && nTrig {2} < (nEntryPrice {3} * RevPct{4} orderFilled {5})",
                            retval, nStatus, Math.Round(nTrig, 4), nEntryPrice, RevPct, orderFilled);

                }
                else
                {
                    if (_algorithm.Portfolio[_symbol].IsShort && nTrig > (Math.Abs(nEntryPrice) * RevPct))
                    {
                        retval = OrderSignal.revertToLong;
                        bReverseTrade = true;
                        comment = string.Format("{0} nStatus == {1} && nTrig {2} > (nEntryPrice {3} * RevPct{4} orderFilled {5})",
                            retval, nStatus, Math.Round(nTrig, 4), nEntryPrice, RevPct, orderFilled);

                    }
                }

                if (!bReverseTrade)
                {
                    if (nTrig > trendArray[0])
                    {

                        if (xOver == -1 && !_algorithm.Portfolio[_symbol].IsLong)
                        {
                            if (!orderFilled)
                            {
                                retval = OrderSignal.goLong;
                                comment =
                                    string.Format(
                                        "{0} nStatus {1} nTrig {2} > trendArray[0].Value {3} xOver {4} orderFilled {5}",
                                        retval, nStatus, Math.Round(nTrig, 4), Math.Round(trendArray[0], 4),
                                        xOver, orderFilled);

                            }
                            else
                            {
                                retval = OrderSignal.goLongLimit;
                                nLimitPrice = priceCalculator.Calculate(data[_symbol], retval, RngFac);
                                //Math.Round(Math.Max(data[_symbol].Low, (data[_symbol].Close - (data[_symbol].High - data[_symbol].Low) * RngFac)), 2, MidpointRounding.ToEven);
                                comment =
                                    string.Format(
                                        "{0} nStatus {1} nTrig {2} > trendArray[0].Value {3} xOver {4} Limit Price {5}",
                                        retval, nStatus, Math.Round(nTrig, 4), Math.Round(trendArray[0], 4), xOver,
                                        nLimitPrice);

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
                            if (xOver == 1 && !_algorithm.Portfolio[_symbol].IsShort) //nStatus != -1
                            {
                                if (!orderFilled)
                                {
                                    retval = OrderSignal.goShort;
                                    comment =
                                        string.Format(
                                            "{0} nStatus {1} nTrig {2} < trendArray[0].Value {3} xOver {4} orderFilled {5}",
                                            retval, nStatus, Math.Round(nTrig, 4), Math.Round(trendArray[0], 4),
                                            xOver, orderFilled);

                                }
                                else
                                {
                                    retval = OrderSignal.goShortLimit;
                                    nLimitPrice = priceCalculator.Calculate(data[_symbol], retval, RngFac);
                                    comment = string.Format("{0} nStatus {1} nTrig {2} < trendArray[0].Value {3} xOver {4} Limit Price {5}", retval, nStatus, Math.Round(nTrig, 4), Math.Round(trendArray[0], 4), xOver, nLimitPrice);

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
            trendArray[2] = 0;
            trendArray[1] = 0;
            trendArray[0] = 0;
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

        #region "Json Serialization"

        public string Serialize()
        {
            for (int i = 0; i < trendHistory.Count; i++)
            {
                trendArray[i] = trendHistory[i];
            }
            string json = JsonConvert.SerializeObject(this);
            return json;
        }

        public void Deserialize(string json)
        {
            var v = JsonConvert.DeserializeObject(json, GetType());
            if (v != null)
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
                for (int i = 0; i < trendArray.Length; i++)
                {
                    trendHistory.Add(trendArray[i]);
                }
            }
        }
        #endregion

        public void SetAlgorithm(QCAlgorithm algorithm)
        {
            _algorithm = algorithm;

        }
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }



    }
}
