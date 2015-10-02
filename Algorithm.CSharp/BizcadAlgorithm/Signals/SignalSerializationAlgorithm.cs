using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using Newtonsoft.Json;

namespace QuantConnect.Algorithm.CSharp
{
    class SignalSerializationAlgorithm : QCAlgorithm
    {
        private DateTime _startDate = new DateTime(2015, 8, 11);
        private DateTime _endDate = new DateTime(2015, 8, 11);
        private decimal _portfolioAmount = 10000;
        private decimal _transactionSize = 15000;
        private Symbol symbol = new Symbol("AAPL");

        private Sig3 sig3;
        private decimal v;
        string json;
        private int barcount = 0;
        public int tradesize = 1;

        public override void Initialize()
        {
            //Initialize dates
            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetCash(_portfolioAmount);

            AddSecurity(SecurityType.Equity, symbol, Resolution.Minute);

            sig3 = new Sig3(symbol);
            v = 0;
        }

        public void OnData(TradeBars data)
        {

            
            string comment;
            sig3.nTrig = v + .1m;
            sig3.orderFilled = !sig3.orderFilled;
            v++;

            if (v == 3m)
            {
                sig3.nEntryPrice = data[symbol].Close;
                json = sig3.Serialize();
            }

            if (v > 3)
            {
                sig3 = new Sig3(symbol);
                sig3.Deserialize(json);
            }
            sig3.Barcount = barcount++;
            sig3.CheckSignal(data, idp(Time, data[symbol].Close), out comment);

            if (v == 5m)
            {
                // Open a file and serialize the object into it in binary format.
                // EmployeeInfo.osl is the file that we are creating. 
                // Note:- you can give any extension you want for your file
                // If you use custom extensions, then the user will now 
                //   that the file is associated with your program.
                Stream stream = File.Open(AssemblyLocator.ExecutingDirectory() + "sig3.osl", FileMode.Create);
                BinaryFormatter bformatter = new BinaryFormatter();

                System.Diagnostics.Debug.WriteLine("Writing Information");
                bformatter.Serialize(stream, sig3);
                stream.Close();

            }
            if (v == 6)
            {
                //Open the file written above and read values from it.
                Stream stream = File.Open(AssemblyLocator.ExecutingDirectory() + "sig3.osl", FileMode.Open);
                var bformatter = new BinaryFormatter();

                Console.WriteLine("Reading Employee Information");
                sig3 = (Sig3)bformatter.Deserialize(stream);
                stream.Close();
                
            }
        }
        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }
    }
}
