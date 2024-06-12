using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PEMDAS
{
    public class CalculationHistory
    {
        public int Id { get; set; }
        public string Expression { get; set; }
        public string Result { get; set; }
        public DateTime Timestamp { get; set; }
        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }

}
