﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flyhero_client
{
    class MeasurementData
    {
        public double Roll { get; set; }
        public double Pitch { get; set; }
        public double Yaw { get; set; }
        public int Throttle { get; set; }
        public int FL { get; set; }
        public int BL { get; set; }
        public int FR { get; set; }
        public int BR { get; set; }
    }
}
