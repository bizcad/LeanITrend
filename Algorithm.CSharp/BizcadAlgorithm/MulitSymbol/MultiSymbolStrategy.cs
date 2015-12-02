using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.CSharp
{
    class MultiSymbolStrategy : BaseStrategy
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
        public SimpleMovingAverage sma;
        public Momentum SMAMomentum;
        public RollingWindow<decimal> MomentumWindow;

        #endregion made public for debug


        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ITrendStrategy"/> class.
        /// </summary>
        /// <param name="period">The period of the Instantaneous trend.</param>
        public MultiSymbolStrategy(Indicator price, int period, decimal tolerance = 0.001m, decimal revetPct = 1.0015m,
            RevertPositionCheck checkRevertPosition = RevertPositionCheck.vsTrigger)
        {
            _price = price;
            sma = new SimpleMovingAverage(period).Of(price);
            SMAMomentum = new Momentum(2).Of(sma);
            MomentumWindow = new RollingWindow<decimal>(2);

            Position = StockState.noInvested;
            EntryPrice = null;
            ActualSignal = OrderSignal.doNothing;

            _tolerance = tolerance;
            _revertPCT = revetPct;
            _checkRevertPosition = checkRevertPosition;

            

            SMAMomentum.Updated += (object sender, IndicatorDataPoint updated) =>
            {
                if (SMAMomentum.IsReady) MomentumWindow.Add(SMAMomentum.Current.Value);
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
                ExitFromLong = (EntryPrice != null) ? sma + SMAMomentum < EntryPrice / _revertPCT : false;
                ExitFromShort = (EntryPrice != null) ? sma + SMAMomentum > EntryPrice * _revertPCT : false;
            }
            else if (_checkRevertPosition == RevertPositionCheck.vsClosePrice)
            {
                ExitFromLong = (EntryPrice != null) ? _price < EntryPrice / _revertPCT : false;
                ExitFromShort = (EntryPrice != null) ? _price > EntryPrice * _revertPCT : false;
            }

            OrderSignal actualSignal;
            if(TriggerCrossOverITrend || TriggerCrossUnderITrend)
                Debug.WriteLine("here");
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
            sma.Reset();
            SMAMomentum.Reset();
            MomentumWindow.Reset();
        }

        #endregion Methods
 
    }
}
