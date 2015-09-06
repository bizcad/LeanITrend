using System;
using System.Collections.Generic;
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
                FieldInfo[] fields = typeof (T).GetFields();
                PropertyInfo[] properties = typeof (T).GetProperties();
                yield return
                    String.Join(separator, fields.Select(f => f.Name).Union(properties.Select(p => p.Name)).ToArray());
                foreach (var o in objectlist)
                {
                    yield return
                        string.Join(separator,
                            (properties.Select(p => (p.GetValue(o, null) ?? "").ToString())).ToArray());
                }
            }

        }
    }
}
