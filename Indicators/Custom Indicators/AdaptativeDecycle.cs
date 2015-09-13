using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Indicators
{
    public class AdaptativeDecycle : Decycle
    {
        int adaptativePeriod;

        public int AdaptativePeriod
        {
            get { return base.period; }
            set
            { 
                base.period = value;
                adaptativePeriod = value;
                base.alpha = (decimal)((Math.Cos(2 * Math.PI / (double)value) + Math.Sin(2 * Math.PI / (double)value) - 1) /
                Math.Cos(2 * Math.PI / (double)value));
            }
        }

        public AdaptativeDecycle()
            : base("AdaptativeDecycle", 3)
        { }


    }
}
