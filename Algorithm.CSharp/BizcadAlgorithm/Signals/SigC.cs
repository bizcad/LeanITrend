using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm
{
    public class SigC
    {
        public bool IsLong { get; set; }
        public bool IsShort { get; set; }
        public bool BarcountLT4 { get; set; }
        public bool NTrigLTEP { get; set; }
        public bool NTrigGTEP { get; set; }
        public bool NTrigGTTA0 { get; set; }
        public bool NTrigLTTA0 { get; set; }
        public bool ReverseTrade { get; set; }
        public bool xOverIsPositive { get; set; }
        public bool xOverisNegative { get; set; }
        public bool OrderFilled { get; set; }

        public int ToInt32()
        {
            int ret = 0;
            
            PropertyInfo[] properties = GetType().GetProperties();
            for (int i = 0; i < properties.Length; i++)
            {
                var p = properties[i];
                try
                {
                    ret = ret << 1;             // shift first so as not to shift after last one.
                    Boolean val = System.Convert.ToBoolean(p.GetValue(this));
                    int x = val ? 1 : 0;
                    ret |= x;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
            return ret;
        }

        public void FromInt32(int input)
        {
            Int32 x = input;
            bool y = false;
           
            PropertyInfo[] properties = GetType().GetProperties();
            for (int i = properties.Length - 1; i >= 0; i--)
            {
                var p = properties[i];
                try
                {
                    var setmethod = p.SetMethod;
                    if (setmethod != null)
                    {
                        p.SetValue(this, System.Convert.ToBoolean(x & 1));
                        x = x >> 1;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }

        public override string ToString()
        {
            PropertyInfo[] properties = GetType().GetProperties();
            var pe = string.Join("", properties.Select(p => System.Convert.ToInt32(p.GetValue(this, null) ?? "")));
            return pe;
        }

        public string ToIntCsv()
        {
            PropertyInfo[] properties = GetType().GetProperties();
            var pe = string.Join(",", properties.Select(p => System.Convert.ToInt32(p.GetValue(this, null) ?? "")));
            return pe;
        }

        public string ToBooleanCsv()
        {
            PropertyInfo[] properties = GetType().GetProperties();
            var pe = string.Join(",", properties.Select(p => System.Convert.ToBoolean(p.GetValue(this, null) ?? "")));
            return pe;
        }

        public void FromIntCsv(string csv)
        {
            PropertyInfo[] properties = GetType().GetProperties();
            string[] arr = csv.Split(',');

            for (int i = 0; i < properties.Length; i++)
            {
                var p = properties[i];
                try
                {
                    bool convertedvalue = arr[i] != "0";

                    var setmethod = p.SetMethod;
                    if (setmethod != null)
                        p.SetValue(this, convertedvalue);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }
        public void FromBooleanCsv(string csv)
        {
            PropertyInfo[] properties = GetType().GetProperties();
            string[] arr = csv.Split(',');

            for (int i = 0; i < properties.Length; i++)
            {
                var p = properties[i];
                try
                {
                    bool convertedvalue = arr[i].ToLower() != "false";

                    var setmethod = p.SetMethod;
                    if (setmethod != null)
                        p.SetValue(this, convertedvalue);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }

        public string ToJson()
        {
            string jsonTypeNameAll = JsonConvert.SerializeObject(this);

            return jsonTypeNameAll;
        }
        public string GetNames()
        {
            string pe;
            PropertyInfo[] properties = GetType().GetProperties();
              pe = String.Join(",", properties.Select(p => p.Name));
            return pe;

        }
    }
}
