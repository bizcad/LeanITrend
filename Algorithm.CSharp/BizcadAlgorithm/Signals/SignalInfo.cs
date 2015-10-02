﻿using System;

namespace QuantConnect.Algorithm.CSharp
{
    public class SignalInfo
    {
        public int Id { get; set; }
        public Type SignalType { get; set; }
        public OrderSignal Value { get; set; }
        public Boolean IsActive { get; set; }
        public string SignalJson { get; set; }
        public string InternalState { get; set; }
        public string Comment { get; set; }
    }
}
