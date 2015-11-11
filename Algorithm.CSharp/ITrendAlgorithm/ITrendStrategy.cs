using QuantConnect.Indicators;
using System;

namespace QuantConnect.Algorithm.CSharp
{

    public class ITrendStrategy : BaseStrategy
    {
        #region Fields

        private decimal _tolerance;
        private decimal _revertPCT;
        private RevertPositionCheck _checkRevertPosition;

        #region made public for debug

        public bool TriggerCrossOverITrend = false;
        public bool TriggerCrossUnderITrend = false;
        public bool ExitFromLong = false;
        public bool ExitFromShort = false;

        Indicator _price;
        public InstantaneousTrend ITrend;
        public Momentum ITrendMomentum;
        public RollingWindow<decimal> MomentumWindow;

        #endregion made public for debug


        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ITrendStrategy"/> class.
        /// </summary>
        /// <param name="period">The period of the Instantaneous trend.</param>
        public ITrendStrategy(Indicator price, int period, decimal tolerance = 0.001m, decimal revetPct = 1.0015m,
            RevertPositionCheck checkRevertPosition = RevertPositionCheck.vsTrigger)
        {
            _price = price;
            ITrend = new InstantaneousTrend(period).Of(price);
            ITrendMomentum = new Momentum(2).Of(ITrend);
            MomentumWindow = new RollingWindow<decimal>(2);

            Position = StockState.noInvested;
            EntryPrice = null;
            ActualSignal = OrderSignal.doNothing;

            _tolerance = tolerance;
            _revertPCT = revetPct;
            _checkRevertPosition = checkRevertPosition;

            Sig9 sig9 = new Sig9();

            ITrendMomentum.Updated += (object sender, IndicatorDataPoint updated) =>
            {
                if (ITrendMomentum.IsReady) MomentumWindow.Add(ITrendMomentum.Current.Value);
                if (MomentumWindow.IsReady) CheckSignal();
            };
        }


        #endregion Constructors

        #region Methods

        /// <summary>
        /// Checks If the strategy throws a operation signal.
        /// </summary>
        /// <returns>An OrderSignal with the proper actualSignal to operate.</returns>
        public override void CheckSignal()
        {
            TriggerCrossOverITrend = MomentumWindow[1] < 0 &&
                                     MomentumWindow[0] > 0 &&
                                     Math.Abs(MomentumWindow[0] - MomentumWindow[1]) >= _tolerance;

            TriggerCrossUnderITrend = MomentumWindow[1] > 0 &&
                                      MomentumWindow[0] < 0 &&
                                      Math.Abs(MomentumWindow[0] - MomentumWindow[1]) >= _tolerance;

            if (_checkRevertPosition == RevertPositionCheck.vsTrigger)
            {
                ExitFromLong = (EntryPrice != null) ? ITrend + ITrendMomentum < EntryPrice / _revertPCT : false;
                ExitFromShort = (EntryPrice != null) ? ITrend + ITrendMomentum > EntryPrice * _revertPCT : false;
            }
            else if (_checkRevertPosition == RevertPositionCheck.vsClosePrice)
            {
                ExitFromLong = (EntryPrice != null) ? _price < EntryPrice / _revertPCT : false;
                ExitFromShort = (EntryPrice != null) ? _price > EntryPrice * _revertPCT : false;
            }

            OrderSignal actualSignal;

            switch (Position)
            {
                case StockState.noInvested:
                    if (TriggerCrossOverITrend) actualSignal = OrderSignal.goLongLimit;
                    else if (TriggerCrossUnderITrend) actualSignal = OrderSignal.goShortLimit;
                    else actualSignal = OrderSignal.doNothing;
                    break;

                case StockState.longPosition:
                    if (TriggerCrossUnderITrend) actualSignal = OrderSignal.closeLong;
                    else if (ExitFromLong) actualSignal = OrderSignal.revertToShort;
                    else actualSignal = OrderSignal.doNothing;
                    break;

                case StockState.shortPosition:
                    if (TriggerCrossOverITrend) actualSignal = OrderSignal.closeShort;
                    else if (ExitFromShort) actualSignal = OrderSignal.revertToLong;
                    else actualSignal = OrderSignal.doNothing;
                    break;

                default: actualSignal = OrderSignal.doNothing;
                    break;
            }
            ActualSignal = actualSignal;
        }

        public void Reset()
        {
            // Not resetting the ITrend increases returns
            ITrend.Reset();
            ITrendMomentum.Reset();
            MomentumWindow.Reset();
        }

        #endregion Methods
    }
}
