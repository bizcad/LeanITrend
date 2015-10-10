using System;

using QuantConnect.Data.Market;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// Calculates a Limit Price
    /// </summary>
    public class InstantTrendLimitPriceCalculator : ILimitPriceCalculator
    {
        /// <summary>
        /// Calculates the Limit Price
        /// </summary>
        /// <param name="data">The current bar</param>
        /// <param name="signal">The signal as to which side to limit</param>
        /// <param name="rangeFactor">A coefficient to decide where in the range to pick</param>
        /// <returns></returns>
        public decimal Calculate(TradeBar data, SignalInfo signalInfo, decimal rangeFactor)
        {
            decimal nLimitPrice = 0;
            if (signalInfo.Value == OrderSignal.goLongLimit)
                nLimitPrice = Math.Round(Math.Max(data.Low, (data.Close - (data.High - data.Low) * rangeFactor)), 2, MidpointRounding.ToEven);
            if (signalInfo.Value == OrderSignal.goShortLimit)
                nLimitPrice = Math.Round(Math.Min(data.High, (data.Close + (data.High - data.Low) * rangeFactor)), 2, MidpointRounding.ToEven);
            return nLimitPrice;
        }
        public decimal Calculate(TradeBar data, OrderSignal signal, decimal rangeFactor)
        {
            decimal nLimitPrice = 0;
            if (signal == OrderSignal.goLongLimit)
                nLimitPrice = Math.Round(Math.Max(data.Low, (data.Close - (data.High - data.Low) * rangeFactor)), 2, MidpointRounding.ToEven);
            if (signal == OrderSignal.goShortLimit)
                nLimitPrice = Math.Round(Math.Min(data.High, (data.Close + (data.High - data.Low) * rangeFactor)), 2, MidpointRounding.ToEven);
            return nLimitPrice;
        }
    }
}
