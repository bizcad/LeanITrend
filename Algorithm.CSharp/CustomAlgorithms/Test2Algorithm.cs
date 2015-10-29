using System;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Data.Consolidators;
using System.Collections.Generic;
using MathNet.Numerics;

namespace QuantConnect.Algorithm.CSharp
{
	public class Test2Algorithm: QCAlgorithm
	{
		private Dictionary<string,TradeBarConsolidator> consolidators = new Dictionary<string, TradeBarConsolidator>();
		private string Symbol = "EWZ";
		private RollingWindow<TradeBar> _tradeBars = new RollingWindow<TradeBar>(10);
		private ExponentialMovingAverage ema;
		private List<decimal> _emaValues = new List<decimal>();
		public Test2Algorithm ()
		{
		}
		/// <summary>
		/// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
		/// </summary>
		public override void Initialize()
		{
			SetStartDate(2000, 07, 01);  //Set Start Date
			SetEndDate(2015,10, 05);    //Set End Date
			SetCash(5200);             //Set Strategy Cash
			// Find more symbols here: http://quantconnect.com/data
			AddSecurity(SecurityType.Equity, Symbol, Resolution.Daily);
			//AddSecurity(SecurityType.Equity, Symbol2, Resolution.Daily);
			ema = EMA (Symbol, 10, Resolution.Daily);
			SetBrokerageModel (QuantConnect.Brokerages.BrokerageName.InteractiveBrokersBrokerage);


		}


		/// <summary>
		/// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
		/// </summary>
		/// <param name="data">Slice object keyed by symbol containing the stock data</param>
		public void OnData(TradeBars data)
		{
			TradeBar currentBar = data [Symbol];

		
			if (!data.ContainsKey (Symbol))
				return;

			_tradeBars.Add (currentBar);
			if (!_tradeBars.IsReady)
				return;
			
			if (!ema.IsReady)
				return;
			_emaValues.Add (ema.Current.Value);
			if (_emaValues.Count > 10)
				_emaValues.RemoveAt (0);
			var slope = 0m;
			if (_emaValues.Count > 2) {

				var xVals = new double[_emaValues.Count];
				var yVals = new double[_emaValues.Count];

				// load input data for regression
				for (int i = 0; i < _emaValues.Count; i++) {
					xVals [i] = i;
					// we want the log of our y values
					yVals [i] = (double)_emaValues [i];
				}

				//http://numerics.mathdotnet.com/Regression.html

				// solves y=a + b*x via linear regression
				var fit = Fit.Line (xVals, yVals);
				var intercept = fit.Item1;
				slope = (decimal)fit.Item2;
			}
			var diff = currentBar.Close / ema.Current.Value - 1.0m;
			if (diff > 0.01m && slope > 0m) {
				
			if (!Portfolio[Symbol].Invested) {
					SetHoldings (Symbol, 1);
					Debug ("Purchased Stock");
				} 

					
			} else {
					Liquidate (Symbol);

				}

		}
	}
}

