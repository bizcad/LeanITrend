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

        private Indicator _price;
        public Decycle DecycleTrend;
        public InverseFisherTransform InverseFisher;
        public RollingWindow<decimal> InvFisherRW;

        #endregion Fields

        #region Constructor

        public DIFStrategy(Indicator Price, int DecyclePeriod = 20, int InvFisherPeriod = 40, decimal Threshold = 0.9m, decimal Tolerance = 0.001m)
        {
            // Initialize the fields.
            _decyclePeriod = DecyclePeriod;
            _invFisherPeriod = InvFisherPeriod;
            _threshold = Threshold;
            _tolerance = Tolerance;

            // Initialize the indicators used by the Strategy.
            _price = Price;
            DecycleTrend = new Decycle(_decyclePeriod).Of(Price);
            InverseFisher = new InverseFisherTransform(_invFisherPeriod).Of(DecycleTrend);
            InvFisherRW = new RollingWindow<decimal>(2);
            
            // Fill the Inverse Fisher rolling windows at every new InverseFisher observation.
            // Once the Inverse Fisher rolling windows is ready, at every InverseFisher update, the Check signal method will be called.
            InverseFisher.Updated += (object sender, IndicatorDataPoint updated) =>
            {
                if (InverseFisher.IsReady) InvFisherRW.Add(updated);
                if (InvFisherRW.IsReady) CheckSignal();
            };
            
            Position = StockState.noInvested;
            EntryPrice = null;
            ActualSignal = OrderSignal.doNothing;
        }

        #endregion Constructor

        #region Overridden methods

        public override void CheckSignal()
        {
            OrderSignal actualSignal = OrderSignal.doNothing;
            
            #region Alternative Signals
            // This signal are faster but inaccurate. The tests works with this signals.

            //bool longSignal = (InvFisherRW[1] < -_threshold) &&
            //                  (InvFisherRW[0] > -_threshold) &&
            //                  (Math.Abs(InvFisherRW[0] - InvFisherRW[1]) > _tolerance);

            //bool shortSignal = (InvFisherRW[1] > _threshold) &&
            //                   (InvFisherRW[0] < _threshold) &&
            //                   (Math.Abs(InvFisherRW[0] - InvFisherRW[1]) > _tolerance);
            #endregion

            bool longSignal = (InvFisherRW[1] < _threshold) &&
                              (InvFisherRW[0] > _threshold) &&
                              (Math.Abs(InvFisherRW[0] - InvFisherRW[1]) > _tolerance);

            bool shortSignal = (InvFisherRW[1] > -_threshold) &&
                               (InvFisherRW[0] < -_threshold) &&
                               (Math.Abs(InvFisherRW[0] - InvFisherRW[1]) > _tolerance);
            

            switch (Position)
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
            ActualSignal = actualSignal;
        }

        #endregion Overridden methods

        #region Methods

        public void Reset()
        {
            this.DecycleTrend.Reset();
            this.InverseFisher.Reset();
            this.InvFisherRW.Reset();
        }

        #endregion Methods
    }
}