using QuantConnect.Algorithm.CSharp.Common;
using QuantConnect.Indicators;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp.JJAlgorithms.MultiStrategyAlgo
{

    public class SimpleMomentumStrategy : BaseStrategy
    {
        #region Fields

        private RevertPositionCheck _checkRevertPosition;
        private Nullable<decimal> _entryPrice = null;
        private StockState _position = StockState.noInvested;
        private decimal _revertPCT;
        private decimal _tolerance;
        
        private bool ExitFromLong = false;
        private bool ExitFromShort = false;
        private bool TriggerCrossOverITrend = false;
        private bool TriggerCrossUnderITrend = false;

        #region made public for debug
        public RollingWindow<decimal> MomentumWindow;
        public IndicatorBase<IndicatorDataPoint> Trend;
        public Momentum TrendMomentum;
        #endregion made public for debug

        public Nullable<decimal> EntryPrice
        {
            get { return _entryPrice; }
            set { _entryPrice = value; }
        }

        public StockState Position
        {
            get { return _position; }
            set { _position = value; }
        }

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ITrendStrategy"/> class.
        /// </summary>
        /// <param name="period">The period of the Instantaneous trend.</param>
        public SimpleMomentumStrategy(IndicatorBase<IndicatorDataPoint> trend,
            RollingWindow<IndicatorDataPoint> priceSeries,
            decimal tolerance = 0.001m, decimal revetPct = 1.0015m,
            RevertPositionCheck checkRevertPosition = RevertPositionCheck.vsClosePrice)
        {
            Trend = trend;
            TrendMomentum = new Momentum(2);
            MomentumWindow = new RollingWindow<decimal>(2);
            _tolerance = tolerance;
            _revertPCT = revetPct;
            _checkRevertPosition = checkRevertPosition;
            InitializeTrend(priceSeries);
        }

        #endregion Constructors

        #region Methods

        private void InitializeTrend(RollingWindow<IndicatorDataPoint> priceSeries)
        {
            Trend.Reset();
            foreach (var dataPoint in priceSeries.OrderBy(p => p.Time))
            {
                Trend.Update(dataPoint);
                TrendMomentum.Update(Trend.Current);
                MomentumWindow.Add(TrendMomentum.Current.Value);
            }
        }

        /// <summary>
        /// Checks If the strategy throws a operation signal.
        /// </summary>
        /// <returns>An enum OrderSignal with the proper order to operate.</returns>
        public override OrderSignal CheckSignal(decimal close)
        {
            // If the injected rolling window isn't enought to fully initialize the strategy, the return will be doNothing
            if (!MomentumWindow.IsReady) return OrderSignal.doNothing;

            TriggerCrossOverITrend = MomentumWindow[1] < 0 && MomentumWindow[0] > 0 &&
                Math.Abs(MomentumWindow[0] - MomentumWindow[1]) >= _tolerance;
            TriggerCrossUnderITrend = MomentumWindow[1] > 0 && MomentumWindow[0] < 0 &&
                Math.Abs(MomentumWindow[0] - MomentumWindow[1]) >= _tolerance;

            if (_checkRevertPosition == RevertPositionCheck.vsTrigger)
            {
                ExitFromLong = (_entryPrice != null) ? Trend + TrendMomentum < _entryPrice / _revertPCT : false;
                ExitFromShort = (_entryPrice != null) ? Trend + TrendMomentum > _entryPrice * _revertPCT : false;
            }
            else if (_checkRevertPosition == RevertPositionCheck.vsClosePrice)
            {
                ExitFromLong = (_entryPrice != null) ? close < _entryPrice / _revertPCT : false;
                ExitFromShort = (_entryPrice != null) ? close > _entryPrice * _revertPCT : false;
            }

            OrderSignal order;

            switch (Position)
            {
                case StockState.noInvested:
                    if (TriggerCrossOverITrend) order = OrderSignal.goLong;
                    else if (TriggerCrossUnderITrend) order = OrderSignal.goShort;
                    else order = OrderSignal.doNothing;
                    break;

                case StockState.longPosition:
                    if (TriggerCrossUnderITrend) order = OrderSignal.closeLong;
                    else if (ExitFromLong) order = OrderSignal.revertToShort;
                    else order = OrderSignal.doNothing;
                    break;

                case StockState.shortPosition:
                    if (TriggerCrossOverITrend) order = OrderSignal.closeShort;
                    else if (ExitFromShort) order = OrderSignal.revertToLong;
                    else order = OrderSignal.doNothing;
                    break;

                default: order = OrderSignal.doNothing;
                    break;
            }
            return order;
        }

        public void Reset()
        {
            Trend.Reset();
            TrendMomentum.Reset();
            MomentumWindow.Reset();
        }

        #endregion Methods
    }
}