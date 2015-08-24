using QuantConnect.Indicators;
using System;

namespace QuantConnect.Algorithm.CSharp.ITrendAlgorithm
{
    internal enum StockStatus
    {
        shortPosition,  // The Portfolio has short position in this bar.
        longPosition,   // The Portfolio has short position in this bar.
        noInvested,     // The Portfolio hasn't any position in this bar.
        orderSent       // An order has been sent in this same bar, skip analysis.
    };

    internal enum OrderSignal
    {
        goShort, goLong,                // Entry to the market orders.
        closeShort, closeLong,          // Exit from the market orders.
        revertToShort, revertToLong,    // Reverse a position when in the wrong side of the trade.
        doNothing
    };

    public class ITrendStrategy
    {
        #region Fields

        private InstantaneousTrend ITrend;
        private Momentum ITrendMomentum;
        private RollingWindow<Momentum> Trigger;

        private decimal _tolerance = 0.001m;
        private decimal _revertPCT = 1.0015m;

        private Nullable<decimal> _entryPrice;
        private StockStatus _position;

        public Nullable<decimal> EntryPrice
        {
            get { return _entryPrice; }
            set { _entryPrice = value; }
        }

        public StockStatus Position
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
            Trigger = new RollingWindow<Momentum>(2);
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
            Trigger.Add(ITrendMomentum);
            if (!Trigger.IsReady) return OrderSignal.doNothing;

            bool TriggerCrossITrendFromBelow = Trigger[1] + _tolerance < 0 && Trigger[0] - _tolerance > 0;
            bool TriggerCrossITrendFromAbove = Trigger[1] + _tolerance > 0 && Trigger[0] - _tolerance < 0;

            bool ExitFromLong = (_entryPrice != null) ? ITrendMomentum + ITrend < _entryPrice / _revertPCT : false;
            bool ExitFromShort = (_entryPrice != null) ? ITrendMomentum + ITrend > _entryPrice * _revertPCT : false;

            OrderSignal order;

            switch (Position)
            {
                case StockStatus.noInvested:
                    if (TriggerCrossITrendFromBelow) order = OrderSignal.goLong;
                    else if (TriggerCrossITrendFromAbove) order = OrderSignal.goShort;
                    else order = OrderSignal.doNothing;
                    break;

                case StockStatus.longPosition:
                    if (TriggerCrossITrendFromAbove) order = OrderSignal.closeLong;
                    else if (ExitFromLong) order = OrderSignal.revertToShort;
                    else order = OrderSignal.doNothing;
                    break;

                case StockStatus.shortPosition:
                    if (TriggerCrossITrendFromBelow) order = OrderSignal.closeShort;
                    else if (ExitFromShort) order = OrderSignal.revertToLong;
                    else order = OrderSignal.doNothing;
                    break;

                default: order = OrderSignal.doNothing;
                    break;
            }
            return order;
        }

        #endregion Methods
    }
}