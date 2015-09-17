using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuantConnect.Algorithm.CSharp.IntradayBuyLow
{
    /// <summary>
    /// This class implement a strategy basd in the following rules:
    ///     - Read the last M days of trading data. For each day, find the downwards 
    ///       and upwards "runs" in the smoothed data.
    ///     - A downward run is a drop with an upward turn around at the end and vice-versa.
    ///     - Find the largest N runs for each day. 
    ///     - Average the drop size over the M days to find the drop size to be used by the model.
    /// </summary>
    public partial class BuyLowStrategy
    {
        # region Fields
        private int runsPerDay;
        private int previousDaysN;

        private bool firstDay;
        private decimal previousValue;
        private DateTime previousValueDate;
        
        private decimal accumulatedRun;
        private decimal valuePrevToRun;
        
        List<decimal> intraDayRuns = new List<decimal>();

        private Queue<decimal> dailyDownwardRuns = new Queue<decimal>();
        private Queue<decimal> dailyUpwardRuns = new Queue<decimal>();
        
        private decimal downwardRunsThreshold;
        private decimal upwardRunsThreshold;

        private bool isReady;
        private bool isDownReady;
        private bool isUpReady;
        
        private bool turnAround;
        private int orderSignal;
        # endregion

        public bool IsReady
        {
          get { return isReady; }
          set { isReady = value; }
        }

        public bool TurnAround
        {
          get { return turnAround; }
          set { turnAround = value; }
        }

        public int OrderSignal
        {
          get { return orderSignal; }
          set { orderSignal = value; }
        }

        // Constructor
        public BuyLowStrategy(int PreviousDaysN, int RunsPerDay)
        {
            this.previousDaysN = PreviousDaysN;
            this.runsPerDay = RunsPerDay;
            
            // Initializing the fields
            isReady = false;
            turnAround = false;
            orderSignal = 0;
            firstDay = true;
        }

        public BuyLowStrategy(int PreviousDaysN, int RunsPerDay, string symbol)
        {
            this.previousDaysN = PreviousDaysN;
            this.runsPerDay = RunsPerDay;

            // Initializing the fields
            isDownReady = true;
            isUpReady = true;
            isReady = true;
            turnAround = false;
            orderSignal = 0;
            firstDay = true;

            // Initializing the previous days runs and the thresholds

            dailyDownwardRuns = WarmUp(symbol, FieldName.downwardRuns);
            dailyUpwardRuns = WarmUp(symbol, FieldName.upwardRuns);

            downwardRunsThreshold = dailyDownwardRuns.Average();
            upwardRunsThreshold = dailyUpwardRuns.Average();
        }

        // Add new serie value
        public void AddSerieValue(DateTime timeStamp, decimal serieValue)
        {
            bool sameDay;
            DateTime actualValueDate = timeStamp.Date;

            if (firstDay)
            {
                // Day initialization
                InitializeDay(timeStamp, serieValue);
                firstDay = false;
            }

            sameDay = actualValueDate == previousValueDate.Date;

            if (sameDay)
            {
                // If is the same day, I'll add the value to the actual day and keep going
                SameDay(timeStamp, serieValue);
            }
            else
            {
                // If is a new day, etimate the means runs for the day.
                NewDay();
                InitializeDay(timeStamp, serieValue);

            }
        }

        private void InitializeDay(DateTime timeStamp, decimal serieValue)
        {
            previousValueDate = timeStamp;
            valuePrevToRun = serieValue;
            previousValue = serieValue;

            accumulatedRun = 0;

            turnAround = false;
            firstDay = false;
        }


        private void SameDay(DateTime timeStamp, decimal serieValue)
        {
            decimal valueDiff;
            decimal brokenRun;

            decimal previousAccum = accumulatedRun;

            valueDiff = serieValue - previousValue;

            /* If the accumulated differences and the new difference has the same signal,
             then keep accumulating*/
            if (valueDiff * previousAccum >= 0)
            {
                accumulatedRun = previousAccum + valueDiff;
                turnAround = false;
            }
            // If not:
            else
            {
                // the accumulation is the las difference
                accumulatedRun = valueDiff;
                // there's a turn around
                turnAround = true;
                // estimate the run's lenght and set the denominator for the next run.
                brokenRun = previousAccum / valuePrevToRun;
                valuePrevToRun = previousValue;

                // add the new run the today's runs
                intraDayRuns.Add(brokenRun);

                // check if the run triggers a order.
                CheckOrders(brokenRun);
            }

            //update the serie and date.
            previousValue = serieValue;
            previousValueDate = timeStamp;
        }


        private void NewDay()
        {
            decimal dayMeanDownwardRun;
            decimal dayMeanUpwardRun;

            // if there's some run data yesterday, then estimate the top runs average 
            if (intraDayRuns.Count != 0)
            {
                // Run two queries to obtain the requier daily run mean
                dayMeanDownwardRun = (from run in intraDayRuns
                                      where run < 0
                                      orderby run ascending
                                      select run).Take(runsPerDay).Average();

                dayMeanUpwardRun = (from run in intraDayRuns
                                    where run > 0
                                    orderby run descending
                                    select run).Take(runsPerDay).Average();

                dailyDownwardRuns.Enqueue(dayMeanDownwardRun);
                dailyUpwardRuns.Enqueue(dayMeanUpwardRun);
                intraDayRuns.Clear();
                // If there are the required days, then the strategy is ready
                if (dailyUpwardRuns.Count == previousDaysN) isDownReady = true;
                if (dailyDownwardRuns.Count == previousDaysN) isUpReady = true;
                if (isDownReady || isUpReady) isReady = true;
                // Keep only the required days
                if (dailyDownwardRuns.Count > previousDaysN)
                {
                    dailyDownwardRuns.Dequeue();
                    dailyUpwardRuns.Dequeue();
                }

                downwardRunsThreshold = dailyDownwardRuns.Average();
                upwardRunsThreshold = dailyUpwardRuns.Average();
            }
        }


        private void CheckOrders(decimal brokenRun)
        {
            if (this.isReady)
            {
                orderSignal = 0; // Do nothing

                if (brokenRun < downwardRunsThreshold && isDownReady)
                {
                    // Long order
                    orderSignal = 1;
                }
                else if (brokenRun > upwardRunsThreshold && isUpReady)
                {
                    // Short order
                    orderSignal = -1;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\nPrevious days downward runs,\n");
            foreach (var run in dailyDownwardRuns)
            {
                sb.Append(run + "m,");
            }
            sb.Append("\nPrevious days upward runs,\n");
            foreach (var run in dailyDownwardRuns)
            {
                sb.Append(run + "m,");
            }
            return sb.ToString();
        }
    }
}
