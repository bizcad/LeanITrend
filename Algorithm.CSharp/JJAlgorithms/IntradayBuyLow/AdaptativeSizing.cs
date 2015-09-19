using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Algorithm.CSharp.IntradayBuyLow
{
    public partial class IntradayBuyLow : QCAlgorithm
    {
        #region Fields
        public enum RebalanceFrequency : int { Daily, Weekly, Monthly };
        
        public Dictionary<string, decimal> stockShareSize = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> previousStockProfit = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> actualStockProfit = new Dictionary<string, decimal>();
        decimal previousProfit = 0m;
        decimal totalProfit;
        decimal actualProfit;

        decimal shareSizeSum;
        
        private int counter = 0;
        #endregion

        private RebalanceFrequency frequency = RebalanceFrequency.Weekly;
        private decimal sizingAlpha = 0.2m; // How fast the rebalance adapts to the last profits
        private decimal minSize = 0.01m;    // Minimun portfolio share 
        private decimal maxSize = 0.01m;    // Maximun portfolio share 

        private void RebalanceOrderSizes(string symbol)
        {
            if (counter == 0)
            {
                foreach (string _symbol in symbols)
                {
                    totalProfit += Math.Abs(Portfolio[_symbol].Profit);
                    actualStockProfit[_symbol] = Portfolio[symbol].Profit - previousStockProfit[_symbol];
                    previousStockProfit[_symbol] = Portfolio[_symbol].Profit;
                }
                actualProfit = (totalProfit - previousProfit == 0) ? 1m : totalProfit - previousProfit;
                previousProfit = totalProfit;

                shareSizeSum = stockShareSize.Values.Sum();
            }

            stockShareSize[symbol] = Math.Min(Math.Max(sizingAlpha * actualStockProfit[symbol] / actualProfit +
                (1 - sizingAlpha) * stockShareSize[symbol] / shareSizeSum, minSize), maxSize);

            counter++;
            if (counter == symbols.Length)
            {
                counter = 0;
                actualProfit = 0;
                shareSizeSum = stockShareSize.Values.Sum();
                foreach (string _symbol in symbols)
                {
                    stockShareSize[_symbol] = stockShareSize[_symbol] * (1m - leverageBuffer) * maxLeverage / shareSizeSum;
                }
            }
        }
    }
}
