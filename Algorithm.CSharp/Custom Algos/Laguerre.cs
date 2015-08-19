using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp.Custom_Algos
{
    class Laguerre
    {
        decimal factor;                       // the factor constant
        decimal tolerance = 0.5m;
        decimal[] previousL = new decimal[4]; // contains the previous values of the L series
        decimal[] actualL = new decimal[4];   // contains the previous values of the L series
        decimal previousLaguerre;
        decimal actualLaguerre;
        decimal actualFIR;
        decimal previousFIR;
        int signal = 0;

        public decimal PreviousLaguerre
        {
            get { return previousLaguerre; }
            set { previousLaguerre = value; }
        }

        public decimal ActualLaguerre
        {
            get { return actualLaguerre; }
        }

        public decimal ActualFIR
        {
            get { return actualFIR; }
        }

        RollingWindow<decimal> windowFIR = new RollingWindow<decimal>(4);

        public bool IsReady()
        {
            return windowFIR.IsReady;
        }

        public int Signal
        {
            get { return signal; }
        }
        
        /*===================
         *=   Constructor   =
         *===================*/
        public Laguerre(double Factor)
        {
            factor = new decimal(Factor);
        }


        /// <summary>
        /// Adds the last value and estimate the Laguerre and FIR indicators.
        /// </summary>
        /// <param name="lastValue">The last value.</param>
        /// <returns>Void.</returns>
        public void Add(decimal lastValue)
        {
            if (windowFIR.Count == 0)
            {
                for (int i = 0; i < previousL.Length; i++) previousL[i] = lastValue;
                previousLaguerre = lastValue;
                previousFIR = lastValue;
                windowFIR.Add(lastValue);
            }
            CalculateNextValue(lastValue);
            CheckCross(previousLaguerre, actualLaguerre, previousFIR, actualFIR);
            previousLaguerre = actualLaguerre;
            previousFIR = actualFIR;
            for (int i = 0; i < actualL.Length; i++) previousL[i] = actualL[i];
        }

        private void CalculateNextValue(decimal lastValue)
        {
            // Estimate L0
            actualL[0] = (1m - factor) * lastValue + factor * previousL[0];
            // Estimate L1 to L3
            for (int i = 1; i < 4; i++)
            {
                actualL[i] = - factor * actualL[i - 1] + previousL[i - 1] + factor * previousL[i];
            }
            actualLaguerre = (actualL[3] + 2 * actualL[2] + 2 * actualL[1] + actualL[0]) / 6;
            

            // Update the Fir window.
            windowFIR.Add(lastValue);
            if (windowFIR.IsReady)
            {
                actualFIR = (windowFIR[3] + 2 * windowFIR[2] + 2 * windowFIR[1] + windowFIR[0]) / 6;
            }
            else
            {
                actualFIR = lastValue;
            }
        }

        private void CheckCross(decimal prevLaguerre, decimal actLaguerre, decimal prevFir, decimal actFIR)
        {
            decimal prevOscilator = prevLaguerre - prevFir;
            decimal actualOscilator = actLaguerre - actFIR;
            
            if (prevOscilator - tolerance > 0 && actualOscilator + tolerance < 0) signal = 1;
            else if (prevOscilator + tolerance < 0 && actualOscilator - tolerance > 0) signal = -1;
            else signal = 0;

        }

    }
}
