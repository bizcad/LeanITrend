using QuantConnect.Algorithm.CSharp;
using QuantConnect.Indicators;
using System;

namespace QuantConnect.Algorithm.CSharp.JJAlgorithms.DecycleInverseFisher
{
    public class DIFStrategy : BaseStrategy
    {
        #region Fields

        private int _decyclePeriod;
        private int _invFisherPeriod;
        private decimal _threshold;
        private decimal _tolerance;

        public Decycle DecycleTrend;
        public InverseFisherTransform InverseFisher;
        public RollingWindow<decimal> InvFisherRW;
        
        #endregion Fields

        #region Constructor

        public DIFStrategy(int DecyclePeriod = 20, int InvFisherPeriod = 40, decimal Threshold = 0.9m, decimal Tolerance = 0.001m)
        {
            _decyclePeriod = DecyclePeriod;
            _invFisherPeriod = InvFisherPeriod;
            _threshold = Threshold;
            _tolerance = Tolerance;

            this.Position = StockState.noInvested;
            this.EntryPrice = null;

            DecycleTrend = new Decycle(_decyclePeriod);
            InverseFisher = new InverseFisherTransform(_invFisherPeriod).Of(DecycleTrend);
            InvFisherRW = new RollingWindow<decimal>(2);
        }

        #endregion Constructor

        #region Overriden methods

        public override OrderSignal CheckSignal()
        {
            OrderSignal actualSignal = OrderSignal.doNothing;

            if (!InverseFisher.IsReady) return actualSignal;

            InvFisherRW.Add(InverseFisher.Current.Value);

            if (InvFisherRW.IsReady)
            {
                //bool longSignal = (InvFisherRW[1] < -_threshold) &&
                //                  (InvFisherRW[0] > -_threshold) &&
                //                  (Math.Abs(InvFisherRW[0] - InvFisherRW[1]) > _tolerance);

                //bool shortSignal = (InvFisherRW[1] > _threshold) &&
                //                   (InvFisherRW[0] < _threshold) &&
                //                   (Math.Abs(InvFisherRW[0] - InvFisherRW[1]) > _tolerance);

                bool longSignal = (InvFisherRW[1] < _threshold) &&
                                  (InvFisherRW[0] > _threshold) &&
                                  (Math.Abs(InvFisherRW[0] - InvFisherRW[1]) > _tolerance);

                bool shortSignal = (InvFisherRW[1] > -_threshold) &&
                                   (InvFisherRW[0] < -_threshold) &&
                                   (Math.Abs(InvFisherRW[0] - InvFisherRW[1]) > _tolerance);

                switch (this.Position)
                {
                    case StockState.shortPosition:
                        if (longSignal) actualSignal = OrderSignal.closeShort;
                        break;

                    case StockState.longPosition:
                        if (shortSignal) actualSignal = OrderSignal.closeLong;
                        break;

                    case StockState.noInvested:
                        if (longSignal) actualSignal = OrderSignal.goLong;
                        else if (shortSignal) actualSignal = OrderSignal.goShort;
                        break;

                    default:
                        break;
                }
            }
            return actualSignal;
        }

        #endregion Override methods

        #region Methods
        public void Reset()
        {
            this.DecycleTrend.Reset();
            this.InverseFisher.Reset();
            this.InvFisherRW.Reset();
        }
        #endregion
    }
}