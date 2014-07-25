using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hudl.StackDriver
{
    public enum MetricType
    {
        /// <summary>
        /// Total metrics will just report the count within the 60 second period
        /// </summary>
        Total,

        /// <summary>
        /// PerSecond metrics will report the count/60s, or per-second avg
        /// </summary>
        PerSecond,
    }
}
