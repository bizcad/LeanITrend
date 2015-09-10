using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp.Common
{
    public static class ObjectToCsv
    {
        public static IEnumerable<string> ToCsv<T>(string separator, IEnumerable<T> objectlist, bool includeFields = true)
        {
            if (objectlist != null)
            {
                FieldInfo[] fields = typeof(T).GetFields();
                PropertyInfo[] properties = typeof(T).GetProperties();
                if (includeFields)
                    yield return String.Join(separator, fields.Select(f => f.Name).Union(properties.Select(p => p.Name)).ToArray());
                foreach (var o in objectlist)
                {
                    yield return
                        string.Join(separator,
                            (properties.Select(p => (p.GetValue(o, null) ?? "").ToString())).ToArray());
                }
            }
        }

        public static void FromCsv<T>(string separator, string csv, ref T inobj, bool includeFields = false)
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
