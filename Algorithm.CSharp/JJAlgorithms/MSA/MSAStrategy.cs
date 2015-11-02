using QuantConnect.Indicators;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This class implement a strategy based in the following rules:
    ///     - Read the last M days of trading data. For each day, find the downwards
    ///       and upwards runs in the smoothed data.
    ///     - A downward run is a drop with an upward turnaround at the end and vice-versa.
    ///     - Find the largest N runs for each day.
    ///     - Average the runs over the M days to find the threshold to be used by the strategy.
    /// </summary>
    public class MSAStrategy : BaseStrategy
    {
        // How many runs will be used to estimate the daily mean.
        private int _runsPerDay;

        // The actual run.
        private decimal _actualRun;

        // Flag indicating there is a turnaround in the smoothed series.
        private bool _turnAround;

        // The upward threshold equal to the mean of the N previous daily means.
        private decimal _upwardRunThreshold;

        // The downward threshold equal to the mean of the N previous daily means.
        private decimal _downwardRunThreshold;

        // Today upward and downward runs
        private List<decimal> _todayRuns;

        // Previous N daily upward runs mean.
        private RollingWindow<decimal> _previousDaysUpwardRuns;

        // Previous N daily downward runs mean.
        private RollingWindow<decimal> _previousDaysDownwardRuns;

        // The smoothed prices series used to estimate the runs.
        private Indicator _smoothedSeries;

        // Smoothed prices rate of change.
        private RateOfChange _smoothedSeriesROC;

        // Smoothed prices rate of change rolling window.
        private RollingWindow<IndicatorDataPoint> _SSROCRW;

        /// <summary>
        /// Gets a value indicating whether this instance is ready.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is ready; otherwise, <c>false</c>.
        /// </value>
        public bool IsReady
        {
            get { return _previousDaysDownwardRuns.IsReady && _previousDaysUpwardRuns.IsReady; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MSAStrategy"/> class.
        /// </summary>
        /// <param name="smoothedSeries">The smoothed series.</param>
        /// <param name="previousDaysN">How many daily means will be used to estimate the thresholds.</param>
        /// <param name="runsPerDay">How many runs will be used to estimate the daily mean.</param>
        public MSAStrategy(Indicator smoothedSeries, int previousDaysN, int runsPerDay)
        {
            _smoothedSeries = smoothedSeries;
            _smoothedSeriesROC = new RateOfChange(1).Of(_smoothedSeries);
            _SSROCRW = new RollingWindow<IndicatorDataPoint>(2);

            _todayRuns = new List<decimal>();
            _previousDaysDownwardRuns = new RollingWindow<decimal>(previousDaysN);
            _previousDaysUpwardRuns = new RollingWindow<decimal>(previousDaysN);

            _runsPerDay = runsPerDay;
            _actualRun = 1m;
            _turnAround = false;

            ActualSignal = OrderSignal.doNothing;
            Position = StockState.noInvested;

            _smoothedSeriesROC.Updated += (object sender, IndicatorDataPoint updated) =>
                {
                    if (_smoothedSeriesROC.IsReady) _SSROCRW.Add(updated);
                    if (_SSROCRW.IsReady) RunStrategy();
                    if (_SSROCRW.IsReady && IsReady) CheckSignal();
                };
        }

        /// <summary>
        /// Runs the strategy, calling the respective methods.
        /// </summary>
        private void RunStrategy()
        {
            // Check if the last observation is in the same day than the previous one.
            if (_SSROCRW[0].Time.Day == _SSROCRW[1].Time.Day)
            {
                SameDayMethod();
            }
            else
            {
                NewDayMethod();
            }
        }

        /// <summary>
        /// During the trading time, this method estimated the runs and flags if there is a turnaround.
        /// </summary>
        private void SameDayMethod()
        {
            // If both momentum PCT have the same sing, keep adding the PCT changes.
            if (_SSROCRW[1].Value * _SSROCRW[0].Value > 0)
            {
                _actualRun *= (1m + _SSROCRW[0].Value);
            }
            // If both momentums has different signs, then a turnaround is detected.
            else if (_SSROCRW[1].Value * _SSROCRW[0].Value < 0)
            {
                // The run is added to today's runs.
                _todayRuns.Add(_actualRun);
                // The actual run is reseted.
                _actualRun = 1m;
                // The turnaround is flagged.
                _turnAround = true;
            }
        }

        /// <summary>
        /// Each new day, this method estimates the last day mean upward and downward run, fills the
        /// respective rolling windows and estimates the thresholds.
        /// </summary>
        private void NewDayMethod()
        {
            // Save the last run.
            _todayRuns.Add(_actualRun);

            // Estimate the daily upward and downward mean.
            var todayMeanDownwardRun = (from run in _todayRuns
                                        where run < 1
                                        orderby run ascending
                                        select run).Take(_runsPerDay).Average();

            var todayMeanUpwardRun = (from run in _todayRuns
                                      where run > 1
                                      orderby run descending
                                      select run).Take(_runsPerDay).Average();

            // Adds yesterday mean to the previous days runs.
            _previousDaysDownwardRuns.Add(todayMeanDownwardRun);
            _previousDaysUpwardRuns.Add(todayMeanUpwardRun);

            // If the strategy is ready, estimate the new thresholds.
            if (IsReady)
            {
                _upwardRunThreshold = _previousDaysUpwardRuns.Average();
                _downwardRunThreshold = _previousDaysDownwardRuns.Average();
            }

            StrategyDailyReset();
        }

        /// <summary>
        /// Checks the strategy status and updates the ActualSignal property if needed.
        /// </summary>
        public override void CheckSignal()
        {
            var actualSignal = OrderSignal.doNothing;
            // If a turnaround is flagged.
            if (_turnAround)
            {
                // Pick the last run.
                var lastRun = _todayRuns.Last();
                // Determine the signal.
                if (lastRun > 1 && lastRun > _upwardRunThreshold) actualSignal = OrderSignal.goShort;
                else if (lastRun < 1 && lastRun < _downwardRunThreshold) actualSignal = OrderSignal.goLong;
                // Once processed, unflag the turnaround.
                _turnAround = false;
            }
            ActualSignal = actualSignal;
        }

        /// <summary>
        /// Resets strategy before start a new day.
        /// </summary>
        private void StrategyDailyReset()
        {
            // Reset the actual run.
            _actualRun = 1m;

            // Resets _todayRuns.
            _todayRuns.Clear();

            _turnAround = false;

            // Reset the indicators.
            _smoothedSeries.Reset();
            _smoothedSeriesROC.Reset();
            _SSROCRW.Reset();
        }
    }
}