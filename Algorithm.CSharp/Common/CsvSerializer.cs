using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace QuantConnect.Algorithm.CSharp
{
    public static class CsvSerializer
    {
        public static IEnumerable<string> Serialize<T>(string separator, IEnumerable<T> objectlist, bool includeFields = true)
        {
            if (objectlist != null)
            {
                FieldInfo[] fields = typeof(T).GetFields();
                PropertyInfo[] properties = typeof(T).GetProperties();
                if (includeFields)
                    yield return String.Join(separator, fields.Select(f => f.Name).Union(properties.Select(p => p.Name)).ToArray());
                foreach (var o in objectlist)
                {
                    var fe = string.Join(separator, fields.Select(f => f.GetValue(o)));
                    var pe = string.Join(separator, properties.Select(p => p.GetValue(o, null) ?? ""));

                    if (fe.Length > 0) 
                        fe += ",";
                    yield return fe + pe;
                    //yield return string.Join(separator,(properties.Select(p => (p.GetValue(o, null) ?? "").ToString())).ToArray());
                }
            }
        }

        public static void Deserialize<T>(string separator, string csv, ref T inobj, bool includeFields = false)
        {
            if (csv.Length > 0)
            {
                FieldInfo[] fields = typeof(T).GetFields();
                PropertyInfo[] properties = typeof(T).GetProperties();
                string[] arr = csv.Split(',');

                for (int i = 0; i < properties.Length; i++)
                {
                    var p = properties[i];
                    var converter = TypeDescriptor.GetConverter(properties[i].PropertyType);
                    try
                    {
                        var convertedvalue = converter.ConvertFrom(arr[i]);
                        var setmethod = p.SetMethod;
                        if (setmethod != null)
                            p.SetValue(inobj, convertedvalue);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                    }
                }
            }
        }
    }
}
