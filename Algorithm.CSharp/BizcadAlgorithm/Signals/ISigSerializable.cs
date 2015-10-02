using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp
{
    public interface ISigSerializable : ISig, ISerializable
    {
        string Serialize();
        void Deserialize(string json);
        SigC GetInternalStateProperties();
        SigC GetInternalStateFields();
        string ToCsv();
    }
}
