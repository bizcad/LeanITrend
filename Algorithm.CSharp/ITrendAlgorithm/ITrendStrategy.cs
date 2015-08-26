using QuantConnect.Indicators;
using System;

namespace QuantConnect.Algorithm.CSharp.ITrendAlgorithm
{
    public enum StockState
    {
        shortPosition,  // The Portfolio has short position in this bar.
        longPosition,   // The Portfolio has short position in this bar.
        noInvested,     // The Portfolio hasn't any position in this bar.
        orderSent       // An order has been sent in this same bar, skip analysis.
    };

    public enum OrderSignal
    {
        goShort, goLong,                // Entry to the market orders.
        closeShort, closeLong,          // Exit from the market orders.
        revertToShort, revertToLong,    // Reverse a position when in the wrong side of the trade.
        doNothing
    };

    public class ITrendStrategy
    {
        #region Fields

        private decimal _tolerance = 0.005m;
        private decimal _revertPCT = 1.0015m;

        private Nullable<decimal> _entryPrice = null;
        private StockState _position = StockState.noInvested;

        #region made public for debug

        public bool TriggerCrossOverITrend = false;
        public bool TriggerCrossUnderITrend = false;
        public bool ExitFromLong = false;
        public bool ExitFromShort = false;

        public InstantaneousTrend ITrend;
        public Momentum ITrendMomentum;
        public RollingWindow<decimal> MomentumWindow;

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
        public ITrendStrategy(int period)
        {
            ITrend = new InstantaneousTrend(period);
            ITrendMomentum = new Momentum(2).Of(ITrend);
            MomentumWindow = new RollingWindow<decimal>(2);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ITrendStrategy"/> class.
        /// </summary>
        /// <param name="period">The period of the Instantaneous trend.</param>
        /// <param name="tolerance">The tolerance coefficient.</param>
        /// <param name="revetPct">TODO: nice description.</param>
        public ITrendStrategy(int period, decimal tolerance, decimal revetPct)
            : this(period)
        {
            _tolerance = tolerance;
            _revertPCT = revetPct;
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Checks If the strategy throws a operation signal.
        /// </summary>
        /// <returns>An enum OrderSignal with the proper order to operate.</returns>
        public OrderSignal CheckSignal()
        {
            MomentumWindow.Add(ITrendMomentum.Current.Value);
            if (!MomentumWindow.IsReady) return OrderSignal.doNothing;

            TriggerCrossOverITrend = MomentumWindow[1] + _tolerance < 0 && MomentumWindow[0] - _tolerance > 0;
            TriggerCrossUnderITrend = MomentumWindow[1] - _tolerance > 0 && MomentumWindow[0] + _tolerance < 0;

            ExitFromLong = (_entryPrice != null) ? ITrend + ITrendMomentum < _entryPrice / _revertPCT : false;
            ExitFromShort = (_entryPrice != null) ? ITrend + ITrendMomentum > _entryPrice * _revertPCT : false;

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
            ITrend.Reset();
            ITrendMomentum.Reset();
            MomentumWindow.Reset();
        }

        #endregion Methods
    }
}