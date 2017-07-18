using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace flyhero_client
{
    public enum DataType
    {
        Accel_X = 1 << 15, Accel_Y = 1 << 14, Accel_Z = 1 << 13, Gyro_X = 1 << 12, Gyro_Y = 1 << 11, Gyro_Z = 1 << 10,
        Temperature = 1 << 9, Roll = 1 << 8, Pitch = 1 << 7, Yaw = 1 << 6, Throttle = 1 << 5,
        Accel_All = Accel_X | Accel_Y | Accel_Z,
        Gyro_All = Gyro_X | Gyro_Y | Gyro_Z,
        Euler_All = Roll | Pitch | Yaw
    };
}
