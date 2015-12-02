using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp
{
    public class QCMessage
    {
        public int Id { get; set; }
        public string TypeName { get; set; }
        public string Contents { get; set; }
        public DateTime TimeSent { get; set; }
        public DateTime TimeRecv { get; set; }
    }
}
