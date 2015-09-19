/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NodaTime;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Brokerages.Tradier;
using QuantConnect.Data.Market;
using QuantConnect.Indicators;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Forex;

namespace QuantConnect.Algorithm
{
    public class BrokerSimulator
    {
        private IAlgorithm _algorithm;
        private bool _locked;
        private bool _quit;
        private bool _sentNoDataError;
        private IBrokerage _brokerage;

        public ConcurrentDictionary<int, ProformaOrderTicket> _orderTickets;
        public RollingWindow<TradeBars> PricesWindow;

        private int _maxOrders = 10000;
        private int _orderId;                   // The next order id

        public BrokerSimulator(IAlgorithm algorithm)
        {
            _algorithm = algorithm;
            _orderTickets = new ConcurrentDictionary<int, ProformaOrderTicket>();
            PricesWindow = new RollingWindow<TradeBars>(2);

        }

        #region "Buy and Sell"
        /// <summary>
        /// Buy Stock (Alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">int Quantity of the asset to trade</param>
        /// <seealso cref="Buy(string, double)"/>
        public ProformaOrderTicket Buy(string symbol, int quantity)
        {
            return Order(symbol, Math.Abs(quantity));
        }

        /// <summary>
        /// Buy Stock (Alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">double Quantity of the asset to trade</param>
        /// <seealso cref="Buy(string, int)"/>
        public ProformaOrderTicket Buy(string symbol, double quantity)
        {
            return Order(symbol, Math.Abs(quantity));
        }

        /// <summary>
        /// Buy Stock (Alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">decimal Quantity of the asset to trade</param>
        /// <seealso cref="Order(string, double)"/>
        public ProformaOrderTicket Buy(string symbol, decimal quantity)
        {
            return Order(symbol, Math.Abs(quantity));
        }

        /// <summary>
        /// Buy Stock (Alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">float Quantity of the asset to trade</param>
        /// <seealso cref="Buy(string, double)"/>
        public ProformaOrderTicket Buy(string symbol, float quantity)
        {
            return Order(symbol, Math.Abs(quantity));
        }

        /// <summary>
        /// Sell stock (alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">int Quantity of the asset to trade</param>
        /// <seealso cref="Sell(string, double)"/>
        public ProformaOrderTicket Sell(string symbol, int quantity)
        {
            return Order(symbol, Math.Abs(quantity) * -1);
        }

        /// <summary>
        /// Sell stock (alias of Order)
        /// </summary>
        /// <param name="symbol">String symbol to sell</param>
        /// <param name="quantity">Quantity to order</param>
        /// <returns>int Order Id.</returns>
        public ProformaOrderTicket Sell(string symbol, double quantity)
        {
            return Order(symbol, Math.Abs(quantity) * -1);
        }

        /// <summary>
        /// Sell stock (alias of Order)
        /// </summary>
        /// <param name="symbol">String symbol</param>
        /// <param name="quantity">Quantity to sell</param>
        /// <returns>int order id</returns>
        /// <seealso cref="Sell(string, double)"/>
        public ProformaOrderTicket Sell(string symbol, float quantity)
        {
            return Order(symbol, Math.Abs(quantity) * -1);
        }

        /// <summary>
        /// Sell stock (alias of Order)
        /// </summary>
        /// <param name="symbol">String symbol to sell</param>
        /// <param name="quantity">Quantity to sell</param>
        /// <returns>Int Order Id.</returns>
        public ProformaOrderTicket Sell(string symbol, decimal quantity)
        {
            return Order(symbol, Math.Abs(quantity) * -1);
        }

        /// <summary>
        /// Issue an order/trade for asset: Alias wrapper for Order(string, int);
        /// </summary>
        /// <seealso cref="Order(string, double)"/>
        public ProformaOrderTicket Order(string symbol, double quantity)
        {
            return Order(symbol, (int)quantity);
        }
        #endregion

        /// <summary>
        /// Issue an order/trade for asset: Alias wrapper for Order(string, int);
        /// </summary>
        /// <remarks></remarks>
        /// <seealso cref="Order(string, double)"/>
        public ProformaOrderTicket Order(string symbol, decimal quantity)
        {
            return Order(symbol, (int)quantity);
        }

        /// <summary>
        /// Wrapper for market order method: submit a new order for quantity of symbol using type order.
        /// </summary>
        /// <param name="symbol">Symbol of the MarketType Required.</param>
        /// <param name="quantity">Number of shares to request.</param>
        /// <param name="asynchronous">Send the order asynchrously (false). Otherwise we'll block until it fills</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <seealso cref="MarketOrder(string, int, bool, string)"/>
        public ProformaOrderTicket Order(string symbol, int quantity, string tag = "")
        {
            return MarketOrder(symbol, quantity,  tag);
        }

        /// <summary>
        /// Market order implementation: Send a market order and wait for it to be filled.
        /// </summary>
        /// <param name="symbol">Symbol of the MarketType Required.</param>
        /// <param name="quantity">Number of shares to request.</param>
        /// <param name="asynchronous">Send the order asynchrously (false). Otherwise we'll block until it fills</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>int Order id</returns>
        public ProformaOrderTicket MarketOrder(string symbol, int quantity, string tag = "")
        {
            var security = _algorithm.Securities[symbol];
            // Assume the ticket is filled at the last close price

            // Create the ticket
            ProformaOrderTicket ticket = new ProformaOrderTicket();
            ticket.OrderId = GetIncrementOrderId();
            ticket.ErrorMessage = string.Empty;
            ticket.AverageFillPrice = this.PricesWindow[0].Values.Count;
            ticket.Status = OrderStatus.Filled;
            ticket.Quantity = quantity;
            ticket.QuantityFilled = quantity;
            ticket.Security_Type = security.Type;
            ticket.Tag = tag;
            ticket.TickeOrderType = OrderType.Market;
            ticket.TicketTime = _algorithm.Time;

            _orderTickets.AddOrUpdate(ticket.OrderId, ticket);

            return ticket;
        }

        /// <summary>
        /// Market on open order implementation: Send a market order when the exchange opens
        /// </summary>
        /// <param name="symbol">The symbol to be ordered</param>
        /// <param name="quantity">The number of shares to required</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>The order ID</returns>
        public ProformaOrderTicket MarketOnOpenOrder(string symbol, int quantity, string tag = "")
        {
            var security = _algorithm.Securities[symbol];
            ProformaOrderTicket ticket = new ProformaOrderTicket();
            ticket.OrderId = GetIncrementOrderId();
            ticket.ErrorMessage = string.Empty;
            ticket.AverageFillPrice = 0;
            ticket.Status = OrderStatus.Submitted;
            ticket.Quantity = quantity;
            ticket.QuantityFilled = 0;
            ticket.Security_Type = security.Type;
            ticket.Tag = tag;
            ticket.TickeOrderType = OrderType.MarketOnOpen;
            ticket.TicketTime = _algorithm.Time;

            _orderTickets.AddOrUpdate(ticket.OrderId, ticket);

            return ticket;
        }

        /// <summary>
        /// Market on close order implementation: Send a market order when the exchange closes
        /// </summary>
        /// <param name="symbol">The symbol to be ordered</param>
        /// <param name="quantity">The number of shares to required</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>The order ID</returns>
        public ProformaOrderTicket MarketOnCloseOrder(string symbol, int quantity, string tag = "")
        {
            var security = _algorithm.Securities[symbol];

            ProformaOrderTicket ticket = new ProformaOrderTicket();
            ticket.OrderId = GetIncrementOrderId();
            ticket.ErrorMessage = string.Empty;
            ticket.AverageFillPrice = 0;
            ticket.Status = OrderStatus.Submitted;
            ticket.Quantity = quantity;
            ticket.QuantityFilled = 0;
            ticket.Security_Type = security.Type;
            ticket.Tag = tag;
            ticket.TickeOrderType = OrderType.MarketOnClose;
            ticket.TicketTime = _algorithm.Time;

            _orderTickets.AddOrUpdate(ticket.OrderId, ticket);

            return ticket;
        }

        /// <summary>
        /// Send a limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <returns>Order id</returns>
        public ProformaOrderTicket LimitOrder(string symbol, int quantity, decimal limitPrice, string tag = "")
        {
            var security = _algorithm.Securities[symbol];
            ProformaOrderTicket ticket = new ProformaOrderTicket();
            ticket.OrderId = GetIncrementOrderId();
            ticket.ErrorMessage = string.Empty;
            ticket.AverageFillPrice = 0;
            ticket.Status = OrderStatus.Submitted;
            ticket.Quantity = quantity;
            ticket.QuantityFilled = 0;
            ticket.Security_Type = security.Type;
            ticket.Tag = tag;
            ticket.TickeOrderType = OrderType.Limit;
            ticket.LimitPrice = limitPrice;
            ticket.TicketTime = _algorithm.Time;

            _orderTickets.AddOrUpdate(ticket.OrderId, ticket);

            return ticket;
        }

        /// <summary>
        /// Create a stop market order and return the newly created ticket; or negative if the order is invalid
        /// </summary>
        /// <param name="symbol">String symbol for the asset we're trading</param>
        /// <param name="quantity">Quantity to be traded</param>
        /// <param name="stopPrice">Price to fill the stop order</param>
        /// <param name="tag">Optional string data tag for the order</param>
        /// <returns>Int orderId for the new order.</returns>
        public ProformaOrderTicket StopMarketOrder(string symbol, int quantity, decimal stopPrice, string tag = "")
        {
            var security = _algorithm.Securities[symbol];
            ProformaOrderTicket ticket = new ProformaOrderTicket();
            ticket.OrderId = GetIncrementOrderId();
            ticket.ErrorMessage = string.Empty;
            ticket.AverageFillPrice = 0;
            ticket.Status = OrderStatus.Submitted;
            ticket.Quantity = quantity;
            ticket.QuantityFilled = 0;
            ticket.Security_Type = security.Type;
            ticket.Tag = tag;
            ticket.TickeOrderType = OrderType.Market;
            ticket.StopPrice = stopPrice;
            ticket.TicketTime = _algorithm.Time;

            _orderTickets.AddOrUpdate(ticket.OrderId, ticket);

            return ticket;
        }

        private ProformaOrderTicket ProformaSubmitOrderTicket(ProformaSubmitOrderRequest request)
        {
            throw new NotImplementedException();
            //// MODIFT the order request
            //request.OrderStatus = OrderStatus.Submitted;
            //request.SetOrderId(GetIncrementOrderId());

            //// Create an Order and add to the list.  We can get it by order id which is set next
            //var p = new ProformaOrder(request)
            //{
            //    OrderStatus = OrderStatus.Submitted,
            //    CurrentMarketPrice = PricesWindow[0][request.Symbol].Close
            //};
            //var order = _orders.GetOrAdd(p.OrderId, p);

            //// Create the ticket and add to list
            //var ticket = AddOrder(request);
            //ticket.OrderStatus = order.OrderStatus;
            //ticket._order = order;
            //_orderTickets.AddOrUpdate(ticket.OrderId, ticket);

            //return ticket;
        }

        /// <summary>
        /// Send a stop limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="stopPrice">Stop price for this order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <returns>Order id</returns>
        public ProformaOrderTicket StopLimitOrder(string symbol, int quantity, decimal stopPrice, decimal limitPrice, string tag = "")
        {
            var security = _algorithm.Securities[symbol];
            ProformaOrderTicket ticket = new ProformaOrderTicket();
            ticket.OrderId = GetIncrementOrderId();
            ticket.ErrorMessage = string.Empty;
            ticket.AverageFillPrice = 0;
            ticket.Status = OrderStatus.Submitted;
            ticket.Quantity = quantity;
            ticket.QuantityFilled = 0;
            ticket.Security_Type = security.Type;
            ticket.Tag = tag;
            ticket.TickeOrderType = OrderType.StopLimit;
            ticket.LimitPrice = limitPrice;
            ticket.StopPrice = stopPrice;
            ticket.TicketTime = _algorithm.Time;

            _orderTickets.AddOrUpdate(ticket.OrderId, ticket);

            return ticket;
        }
        #region PreOrderChecks
        /// <summary>
        /// Perform preorder checks to ensure we have sufficient capital, 
        /// the market is open, and we haven't exceeded maximum realistic orders per day.
        /// </summary>
        /// <returns>OrderResponse. If no error, order request is submitted.</returns>
        public OrderResponse PreOrderChecks(SubmitOrderRequest request)
        {
            var response = PreOrderChecksImpl(request);
            if (response.IsError)
            {
                _algorithm.Error(response.ErrorMessage);
            }
            return response;
        }

        /// <summary>
        /// Perform preorder checks to ensure we have sufficient capital, 
        /// the market is open, and we haven't exceeded maximum realistic orders per day.
        /// </summary>
        /// <returns>OrderResponse. If no error, order request is submitted.</returns>
        private OrderResponse PreOrderChecksImpl(SubmitOrderRequest request)
        {
            //Ordering 0 is useless.
            if (request.Quantity == 0 || string.IsNullOrEmpty(request.Symbol))
            {
                return OrderResponse.ZeroQuantity(request);
            }

            //If we're not tracking this symbol: throw error:
            if (!_algorithm.Securities.ContainsKey(request.Symbol) && !_sentNoDataError)
            {
                _sentNoDataError = true;
                return OrderResponse.Error(request, OrderResponseErrorCode.MissingSecurity, "You haven't requested " + request.Symbol + " data. Add this with AddSecurity() in the Initialize() Method.");
            }

            //Set a temporary price for validating order for market orders:
            var security = _algorithm.Securities[request.Symbol];
            var price = security.Price;

            //Check the exchange is open before sending a market on close orders
            //Allow market orders, they'll just execute when the exchange reopens
            if (request.OrderType == OrderType.MarketOnClose && !security.Exchange.ExchangeOpen)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.ExchangeNotOpen, request.OrderType + " order and exchange not open.");
            }

            if (price == 0)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.SecurityPriceZero, request.Symbol + ": asset price is $0. If using custom data make sure you've set the 'Value' property.");
            }

            if (security.Type == SecurityType.Forex)
            {
                // for forex pairs we need to verify that the conversions to USD have values as well
                string baseCurrency, quoteCurrency;
                Forex.DecomposeCurrencyPair(security.Symbol, out baseCurrency, out quoteCurrency);

                // verify they're in the portfolio
                Cash baseCash, quoteCash;
                if (!_algorithm.Portfolio.CashBook.TryGetValue(baseCurrency, out baseCash) || !_algorithm.Portfolio.CashBook.TryGetValue(quoteCurrency, out quoteCash))
                {
                    return OrderResponse.Error(request, OrderResponseErrorCode.ForexBaseAndQuoteCurrenciesRequired, request.Symbol + ": requires " + baseCurrency + " and " + quoteCurrency + " in the cashbook to trade.");
                }
                // verify we have conversion rates for each leg of the pair back into the account currency
                if (baseCash.ConversionRate == 0m || quoteCash.ConversionRate == 0m)
                {
                    return OrderResponse.Error(request, OrderResponseErrorCode.ForexConversionRateZero, request.Symbol + ": requires " + baseCurrency + " and " + quoteCurrency + " to have non-zero conversion rates. This can be caused by lack of data.");
                }
            }

            //Make sure the security has some data:
            if (!security.HasData)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.SecurityHasNoData, "There is no data for this symbol yet, please check the security.HasData flag to ensure there is at least one data point.");
            }

            //We've already processed too many orders: max 100 per day or the memory usage explodes
            if (_orderTickets.Count > _maxOrders)
            //if (_algorithm.Transactions.OrdersCount > _maxOrders)
            {
                _quit = true;
                return OrderResponse.Error(request, OrderResponseErrorCode.ExceededMaximumOrders, string.Format("You have exceeded maximum number of orders ({0}), for unlimited orders upgrade your account.", _maxOrders));
            }

            if (request.OrderType == OrderType.MarketOnClose)
            {
                // must be submitted with at least 10 minutes in trading day, add buffer allow order submission
                var latestSubmissionTime = (_algorithm.Time.Date + security.Exchange.MarketClose).AddMinutes(-10.75);
                if (_algorithm.Time > latestSubmissionTime)
                {
                    // tell the user we require an 11 minute buffer, on minute data in live a user will receive the 3:49->3:50 bar at 3:50,
                    // this is already too late to submit one of these orders, so make the user do it at the 3:48->3:49 bar so it's submitted
                    // to the brokerage before 3:50.
                    return OrderResponse.Error(request, OrderResponseErrorCode.MarketOnCloseOrderTooLate, "MarketOnClose orders must be placed with at least a 11 minute buffer before market close.");
                }
            }

            // passes all initial order checks
            return OrderResponse.Success(request);
        }
        #endregion
        /// <summary>
        /// Liquidate all holdings. Called at the end of day for tick-strategies.
        /// </summary>
        /// <param name="symbolToLiquidate">Symbols we wish to liquidate</param>
        /// <returns>Array of order ids for liquidated symbols</returns>
        /// <seealso cref="MarketOrder"/>
        public List<int> Liquidate(string symbolToLiquidate = "")
        {
            var orderIdList = new List<int>();
            symbolToLiquidate = symbolToLiquidate.ToUpper();

            foreach (var symbol in _algorithm.Securities.Keys)
            {
                //Send market order to liquidate if 1, we have stock, 2, symbol matches.
                if (!_algorithm.Portfolio[symbol].HoldStock || (symbol != symbolToLiquidate && symbolToLiquidate != "")) continue;

                var quantity = 0;
                if (_algorithm.Portfolio[symbol].IsLong)
                {
                    quantity = -_algorithm.Portfolio[symbol].Quantity;
                }
                else
                {
                    quantity = Math.Abs(_algorithm.Portfolio[symbol].Quantity);
                }

                //Liquidate at market price.
                var ticket = MarketOrder(symbol, quantity,"Liquidate Position");
                if (ticket.Status == OrderStatus.Filled)
                {
                    orderIdList.Add(ticket.OrderId);
                }
            }

            return orderIdList;
        }

        /// <summary>
        /// Maximum number of orders for the algorithm
        /// </summary>
        /// <param name="max"></param>
        public void SetMaximumOrders(int max)
        {
            if (!_locked)
            {
                _maxOrders = max;
            }
        }

        /// <summary>
        /// Alias for SetHoldings to avoid the M-decimal errors.
        /// </summary>
        /// <param name="symbol">string symbol we wish to hold</param>
        /// <param name="percentage">double percentage of holdings desired</param>
        /// <param name="liquidateExistingHoldings">liquidate existing holdings if neccessary to hold this stock</param>
        /// <seealso cref="MarketOrder"/>
        public void SetHoldings(string symbol, double percentage, bool liquidateExistingHoldings = false)
        {
            SetHoldings(symbol, (decimal)percentage, liquidateExistingHoldings);
        }

        /// <summary>
        /// Alias for SetHoldings to avoid the M-decimal errors.
        /// </summary>
        /// <param name="symbol">string symbol we wish to hold</param>
        /// <param name="percentage">float percentage of holdings desired</param>
        /// <param name="liquidateExistingHoldings">bool liquidate existing holdings if neccessary to hold this stock</param>
        /// <param name="tag">Tag the order with a short string.</param>
        /// <seealso cref="MarketOrder"/>
        public void SetHoldings(string symbol, float percentage, bool liquidateExistingHoldings = false, string tag = "")
        {
            SetHoldings(symbol, (decimal)percentage, liquidateExistingHoldings);
        }

        /// <summary>
        /// Alias for SetHoldings to avoid the M-decimal errors.
        /// </summary>
        /// <param name="symbol">string symbol we wish to hold</param>
        /// <param name="percentage">float percentage of holdings desired</param>
        /// <param name="liquidateExistingHoldings">bool liquidate existing holdings if neccessary to hold this stock</param>
        /// <param name="tag">Tag the order with a short string.</param>
        /// <seealso cref="MarketOrder"/>
        public void SetHoldings(string symbol, int percentage, bool liquidateExistingHoldings = false, string tag = "")
        {
            SetHoldings(symbol, (decimal)percentage, liquidateExistingHoldings);
        }

        /// <summary>
        /// Automatically place an order which will set the holdings to between 100% or -100% of *PORTFOLIO VALUE*.
        /// E.g. SetHoldings("AAPL", 0.1); SetHoldings("IBM", -0.2); -> Sets portfolio as long 10% APPL and short 20% IBM
        /// E.g. SetHoldings("AAPL", 2); -> Sets apple to 2x leveraged with all our cash.
        /// </summary>
        /// <param name="symbol">string Symbol indexer</param>
        /// <param name="percentage">decimal fraction of portfolio to set stock</param>
        /// <param name="liquidateExistingHoldings">bool flag to clean all existing holdings before setting new faction.</param>
        /// <param name="tag">Tag the order with a short string.</param>
        /// <seealso cref="MarketOrder"/>
        public void SetHoldings(string symbol, decimal percentage, bool liquidateExistingHoldings = false, string tag = "")
        {
            //Initialize Requirements:
            Security security;
            if (!_algorithm.Securities.TryGetValue(symbol, out security))
            {
                _algorithm.Error(symbol.ToUpper() + " not found in portfolio. Request this data when initializing the algorithm.");
                return;
            }

            //If they triggered a liquidate
            if (liquidateExistingHoldings)
            {
                foreach (var holdingSymbol in _algorithm.Portfolio.Keys)
                {
                    if (holdingSymbol != symbol && security.Holdings.AbsoluteQuantity > 0)
                    {
                        //Go through all existing holdings [synchronously], market order the inverse quantity:
                        MarketOrder(holdingSymbol, -security.Holdings.Quantity, "Set Holdings Liquidate - " + tag);
                    }
                }
            }

            //Only place trade if we've got > 1 share to order.
            var quantity = CalculateOrderQuantity(symbol, percentage);
            if (Math.Abs(quantity) > 0)
            {
                MarketOrder(symbol, quantity, tag);
            }
        }

        /// <summary>
        /// Calculate the order quantity to achieve target-percent holdings.
        /// </summary>
        /// <param name="symbol">Security object we're asking for</param>
        /// <param name="target">Target percentag holdings</param>
        /// <returns>Order quantity to achieve this percentage</returns>
        public int CalculateOrderQuantity(string symbol, double target)
        {
            return CalculateOrderQuantity(symbol, (decimal)target);
        }

        /// <summary>
        /// Calculate the order quantity to achieve target-percent holdings.
        /// </summary>
        /// <param name="symbol">Security object we're asking for</param>
        /// <param name="target">Target percentag holdings, this is an unlevered value, so 
        /// if you have 2x leverage and request 100% holdings, it will utilize half of the 
        /// available margin</param>
        /// <returns>Order quantity to achieve this percentage</returns>
        public int CalculateOrderQuantity(string symbol, decimal target)
        {
            var security = _algorithm.Securities[symbol];
            var price = security.Price;

            // can't order it if we don't have data
            if (price == 0) return 0;

            // this is the value in dollars that we want our holdings to have
            var targetPortfolioValue = target * _algorithm.Portfolio.TotalPortfolioValue;
            var quantity = security.Holdings.Quantity;
            var currentHoldingsValue = price * quantity;

            // remove directionality, we'll work in the land of absolutes
            var targetOrderValue = Math.Abs(targetPortfolioValue - currentHoldingsValue);
            var direction = targetPortfolioValue > currentHoldingsValue ? OrderDirection.Buy : OrderDirection.Sell;


            // define lower and upper thresholds for the iteration
            var lowerThreshold = targetOrderValue - price / 2;
            var upperThreshold = targetOrderValue + price / 2;

            // continue iterating while  we're still not within the specified thresholds
            var iterations = 0;
            var orderQuantity = 0;
            decimal orderValue = 0;
            while ((orderValue < lowerThreshold || orderValue > upperThreshold) && iterations < 10)
            {
                // find delta from where we are to where we want to be
                var delta = targetOrderValue - orderValue;
                // use delta value to compute a change in quantity required
                var deltaQuantity = (int)(delta / price);

                orderQuantity += deltaQuantity;

                // recompute order fees
                var order = new MarketOrder(security.Symbol, orderQuantity, _algorithm.UtcTime, type: security.Type);
                var fee = security.TransactionModel.GetOrderFee(security, order);

                orderValue = Math.Abs(order.GetValue(price)) + fee;

                // we need to add the fee in as well, even though it's not value, it's still a cost for the transaction
                // and we need to account for it to be sure we can make the trade produced by this method, imagine
                // set holdings 100% with 1x leverage, but a high fee structure, it quickly becomes necessary to include
                // otherwise the result of this function will be inactionable.

                iterations++;
            }

            // add directionality back in
            return (direction == OrderDirection.Sell ? -1 : 1) * orderQuantity;
        }


        //public ProformaSubmitOrderRequest CreateSubmitOrderRequest(OrderType orderType, Security security, int quantity, string tag, decimal stopPrice = 0m, decimal limitPrice = 0m)
        //{

        //    return new ProformaSubmitOrderRequest(orderType, security.Type, security.Symbol, quantity,
        //        stopPrice, limitPrice, DateTime.Now, tag);
        //}

        public int GetTicketCount()
        {
            return _orderTickets.Count;
        }



        /// <summary>
        /// Get a new order id, and increment the internal counter.
        /// </summary>
        /// <returns>New unique int order id.</returns>
        public int GetIncrementOrderId()
        {
            return Interlocked.Increment(ref _orderId);
        }

        /// <summary>
        /// Add an order to collection and return the unique order id or negative if an error.
        /// </summary>
        /// <param name="request">A request detailing the order to be submitted</param>
        /// <returns>New unique, increasing orderid</returns>
        public ProformaOrderTicket AddOrder(string symbol, int quantity, string tag = "")
        {
            var ticket = MarketOrder(symbol, quantity, tag);
            return ticket;
        }
        public ProformaOrderTicket PopTicket(int orderId)
        {
            ProformaOrderTicket ticket;
            _orderTickets.TryRemove(orderId, out ticket);
            return ticket;
        }

        public bool TicketExists(ProformaOrderTicket ticket)
        {
            return _orderTickets.ContainsKey(ticket.OrderId);
        }

        public void UpdateTicket(ProformaOrderTicket ticket)
        {
            _orderTickets.AddOrUpdate(ticket.OrderId, ticket);
        }

    }


}
