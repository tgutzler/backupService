using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ServerApi.Options
{
    public class ApiOptions
    {
        public int MultipartBoundaryLengthLimit { get; set; }
        public int ValueCountLimit { get; set; }
    }

}
