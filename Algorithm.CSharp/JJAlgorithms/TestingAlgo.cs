using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Data.Consolidators;
using QuantConnect.Orders;
using QuantConnect.Indicators;


namespace QuantConnect
{
    public partial class TestingAlgo : QCAlgorithm
    {
        #region Fields
        private static string[] Symbols = { "AIG", "BAC", "IBM", "SPY" };
        int counter;
        int onOrderCounter;
        
        CyclePeriod cyclePeriod;
                
        StringBuilder toFile = new StringBuilder();
        #endregion

        #region QCAlgorithm Methods
        public override void Initialize()
        {
            SetStartDate(2013, 10, 7);
            SetEndDate(2013, 10, 9);

            SetCash(250000);

            foreach (var symbol in Symbols)
            {
                AddSecurity(SecurityType.Equity, symbol, Resolution.Minute); 
            }

            cyclePeriod = new CyclePeriod("Period");
            counter = 0;
            onOrderCounter = 0;
            
        }

        public void OnData(TradeBars data)
        {
            if (counter % 30 == 0)
            {
                foreach (var symbol in Symbols)
                {
                    Buy(symbol, 10);
                    //LimitOrder(symbol, 10, data[symbol].High * 1.01m);
                }
            }
            counter++;
        }

        //public override void OnEndOfDay()
        //{
        //    int idx = 0;
        //    var IBMTickets = Transactions.GetOrderTickets(filter: t => (t.Symbol == "IBM" && t.OrderId == 3)).Last();
        //    foreach (var orderTicket in IBMTickets)
        //    {
        //        Console.ForegroundColor = ConsoleColor.Green;
        //        Console.WriteLine(string.Format("{0} {1} CONSOLE called {2} times || {3}", Time.ToLongDateString(), Time.ToLongTimeString(), idx, orderTicket.ToString()));
        //        Console.ForegroundColor = ConsoleColor.Red;
        //        //Log(string.Format("LOG called {0} times || {1}", idx, orderTicket.ToString()));
        //        Console.ResetColor();
        //        idx++;
        //    }
        //}

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            #region Logging stuff
            string orderStatusLog;

            switch (orderEvent.Status)
            {
                case OrderStatus.New:
                    orderStatusLog = " was created.";
                    break;
                case OrderStatus.Submitted:
                    orderStatusLog = " was submitted.";
                    break;
                case OrderStatus.PartiallyFilled:
                    orderStatusLog = string.Format(" was partially filled with {0} shares of {1}, at ${2}.",
                        orderEvent.FillQuantity,
                        Transactions.GetOrderById(orderEvent.OrderId).Quantity,
                        orderEvent.FillPrice
                        );
                    break;
                case OrderStatus.Filled:
                    orderStatusLog = string.Format(" was filled at ${0}.",
                        orderEvent.FillPrice
                        );
                    break;
                case OrderStatus.Canceled:
                    orderStatusLog = " was canceled.";
                    break;
                case OrderStatus.None:
                    orderStatusLog = " doesn't give a #$@!";
                    break;
                case OrderStatus.Invalid:
                    orderStatusLog = " is invalid.";
                    break;
                default:
                    orderStatusLog = "!";
                    break;
            }


            string newLine = string.Format("{0} : {1} {2} Order Id {3} of {4}",
                Time,
                orderEvent.Direction,
                Transactions.GetOrderById(orderEvent.OrderId).Type,
                orderEvent.OrderId,
                orderEvent.Symbol
                ) + orderStatusLog;
            toFile.AppendLine(newLine);

            #endregion
        }

        public override void OnEndOfAlgorithm()
        {
            string filePath = @"C:\Users\JJ\Desktop\MA y señales\ITrend Debug\onOrder.txt";
            //File.Create(filePath);
            if (File.Exists(filePath))
                File.Delete(filePath);
            File.AppendAllText(filePath, toFile.ToString());
        }
        #endregion

        #region Methods
        private int WaveLength(int counter)
        {
            int waveLength = 30;
            if (counter <= 100) waveLength = 30;
            else if (counter > 100 && counter <= 300) waveLength = 60;
            else if (counter > 300 && counter <= 400) waveLength = 20;
            else if (counter > 400 && counter <= 600) waveLength = 40;
            else waveLength = 15;
            return waveLength;
        }

        private double sinewave(double t, double waveLength, double Vp, double fo, double Phase, double Vdc)
        {
            var pi = Math.PI;
            return Vp * Math.Sin(2 * pi * fo * t / waveLength + Phase * pi / 180) + Vdc;

        }

        private IndicatorDataPoint idp(DateTime time, decimal value)
        {
            return new IndicatorDataPoint(time, value);
        }
        # endregion

    }
}