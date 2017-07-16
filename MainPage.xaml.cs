using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.System.Threading;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace flyhero_client
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private int throttle = 0;
        private int RollKp = 70;
        private int RollKi = 100;
        private int RollKd = 0;
        private int PitchKp = 70;
        private int PitchKi = 100;
        private int PitchKd = 0;
        private int YawKp = 0;
        private int YawKi = 0;
        private int YawKd = 0;
        private bool yawInvert = false;
        private bool logData = false;
        private DisplayRequest displayRequest;
        private DatagramSocket socket;
        private DataWriter dw;
        private ConcurrentQueue<MeasurementData> queue;
        private ThreadPoolTimer timer;
        private bool[] logEnabled = new bool[11];

        public MainPage()
        {
            this.InitializeComponent();
            this.socket = new DatagramSocket();
            this.queue = new ConcurrentQueue<MeasurementData>();

            for (int i = 0; i < 11; i++)
                logEnabled[i] = false;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            this.displayRequest = new DisplayRequest();
            this.displayRequest.RequestActive();

            var pairs = await DatagramSocket.GetEndpointPairsAsync(new HostName("192.168.4.1"), "4789");

            //if (pairs.Count > 0 && pairs.First().LocalHostName.RawName.Equals("192.168.4.2"))
            // {
                await this.socket.BindServiceNameAsync("4789");
                this.socket.MessageReceived += Socket_MessageReceived;

                await this.socket.ConnectAsync(new HostName("192.168.4.1"), "4789");

                this.dw = new DataWriter(this.socket.OutputStream);

            //}
        }

        private void Socket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            DataReader dr = args.GetDataReader();
            dr.ByteOrder = ByteOrder.BigEndian;

            uint length = dr.UnconsumedBufferLength;

            // skip the very first 0x33 header byte
            dr.ReadByte();
            length--;

            byte[] data = new byte[length];
            dr.ReadBytes(data);

            byte crc = data[length - 1];

            byte localCrc = 0x00;
            for (int i = 0; i < length - 1; i++)
                localCrc ^= data[i];

            if (localCrc == crc)
            {
                double accelX, accelY, accelZ;
                double gyroX, gyroY, gyroZ;
                double temperature;
                double roll, pitch, yaw;
                int throttle, deltaT;

                accelX = accelY = accelZ = 0;
                gyroX = gyroY = gyroZ = 0;
                temperature = 0;
                roll = pitch = yaw = 0;
                throttle = 0;

                int parsePos = 0;
                double gDiv = Math.Pow(2, 15) / 2000; // +- 2000 deg/s FSR
                double aDiv = Math.Pow(2, 15) / 16; // +- 16 g FSR

                if (this.logEnabled[0])
                {
                    byte swap = data[parsePos + 1];
                    data[parsePos + 1] = data[parsePos];
                    data[parsePos] = swap;
                    accelX = BitConverter.ToInt16(data, parsePos);
                    accelX /= aDiv;

                    parsePos += 2;
                }
                if (this.logEnabled[1])
                {
                    byte swap = data[parsePos + 1];
                    data[parsePos + 1] = data[parsePos];
                    data[parsePos] = swap;
                    accelY = BitConverter.ToInt16(data, parsePos);
                    accelY /= aDiv;

                    parsePos += 2;
                }
                if (this.logEnabled[2])
                {
                    byte swap = data[parsePos + 1];
                    data[parsePos + 1] = data[parsePos];
                    data[parsePos] = swap;
                    accelZ = BitConverter.ToInt16(data, parsePos);
                    accelZ /= aDiv;

                    parsePos += 2;
                }
                if (this.logEnabled[3])
                {
                    byte swap = data[parsePos + 1];
                    data[parsePos + 1] = data[parsePos];
                    data[parsePos] = swap;
                    gyroX = BitConverter.ToInt16(data, parsePos);
                    gyroX /= gDiv;

                    parsePos += 2;
                }
                if (this.logEnabled[4])
                {
                    byte swap = data[parsePos + 1];
                    data[parsePos + 1] = data[parsePos];
                    data[parsePos] = swap;
                    gyroY = BitConverter.ToInt16(data, parsePos);
                    gyroY /= gDiv;

                    parsePos += 2;
                }
                if (this.logEnabled[5])
                {
                    byte swap = data[parsePos + 1];
                    data[parsePos + 1] = data[parsePos];
                    data[parsePos] = swap;
                    gyroZ = BitConverter.ToInt16(data, parsePos);
                    gyroZ /= gDiv;

                    parsePos += 2;
                }
                if (this.logEnabled[6])
                {
                    byte swap = data[parsePos + 1];
                    data[parsePos + 1] = data[parsePos];
                    data[parsePos] = swap;
                    temperature = BitConverter.ToInt16(data, parsePos);
                    temperature = temperature / 340 + 36.53;

                    parsePos += 2;
                }
                if (this.logEnabled[7])
                {
                    byte swap = data[parsePos + 3];
                    data[parsePos + 3] = data[parsePos];
                    data[parsePos] = swap;
                    swap = data[parsePos + 2];
                    data[parsePos + 2] = data[parsePos + 1];
                    data[parsePos + 1] = swap;
                    roll = BitConverter.ToSingle(data, parsePos);

                    parsePos += 4;
                }
                if (this.logEnabled[8])
                {
                    byte swap = data[parsePos + 3];
                    data[parsePos + 3] = data[parsePos];
                    data[parsePos] = swap;
                    swap = data[parsePos + 2];
                    data[parsePos + 2] = data[parsePos + 1];
                    data[parsePos + 1] = swap;
                    pitch = BitConverter.ToSingle(data, parsePos);

                    parsePos += 4;
                }
                if (this.logEnabled[9])
                {
                    byte swap = data[parsePos + 3];
                    data[parsePos + 3] = data[parsePos];
                    data[parsePos] = swap;
                    swap = data[parsePos + 2];
                    data[parsePos + 2] = data[parsePos + 1];
                    data[parsePos + 1] = swap;
                    yaw = BitConverter.ToSingle(data, parsePos);

                    parsePos += 4;
                }
                if (this.logEnabled[10])
                {
                    byte swap = data[parsePos + 1];
                    data[parsePos + 1] = data[parsePos];
                    data[parsePos] = swap;
                    throttle = BitConverter.ToUInt16(data, parsePos);

                    if (throttle != 0)
                        throttle -= 1000;

                    parsePos += 2;
                }

                byte tmpSwap = data[parsePos + 1];
                data[parsePos + 1] = data[parsePos];
                data[parsePos] = tmpSwap;
                deltaT = BitConverter.ToUInt16(data, parsePos);

                parsePos += 2;

                this.queue.Enqueue(new MeasurementData() { AccelX = accelX, AccelY = accelY, AccelZ = accelZ, GyroX = gyroX, GyroY = gyroY, GyroZ = gyroZ, Temperature = temperature, Roll = roll, Pitch = pitch, Yaw = yaw, Throttle = throttle, DeltaT = deltaT });
            }
        }

        private void rollKp_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.RollKp = (int)Math.Round(e.NewValue * 100);
        }

        private void rollKi_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.RollKi = (int)Math.Round(e.NewValue * 100);
        }

        private void rollkD_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.RollKd = (int)Math.Round(e.NewValue * 100);
        }

        private void pitchKp_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.PitchKp = (int)Math.Round(e.NewValue * 100);
        }

        private void pitchKi_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.PitchKi = (int)Math.Round(e.NewValue * 100);
        }

        private void pitchKd_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.PitchKd = (int)Math.Round(e.NewValue * 100);
        }

        private void yawKp_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.YawKp = (int)Math.Round(e.NewValue * 100);
        }

        private void yawKi_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.YawKi = (int)Math.Round(e.NewValue * 100);
        }

        private void yawKd_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.YawKd = (int)Math.Round(e.NewValue * 100);
        }

        private void PWM_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.throttle = (int)e.NewValue;
        }

        private void invert_yaw_Toggled(object sender, RoutedEventArgs e)
        {
            this.yawInvert = !this.yawInvert;
        }

        private async void calibrate_Click(object sender, RoutedEventArgs e)
        {
            UInt16 logOptions = 0;
            StringBuilder sb = new StringBuilder();

            if (this.logData)
            {
                if (this.accelXToggle.IsChecked == true)
                {
                    logOptions |= 0x400;
                    sb.Append("Accel_X;");
                    this.logEnabled[0] = true;
                }
                if (this.accelYToggle.IsChecked == true)
                {
                    logOptions |= 0x200;
                    sb.Append("Accel_Y;");
                    this.logEnabled[1] = true;
                }
                if (this.accelZToggle.IsChecked == true)
                {
                    logOptions |= 0x100;
                    sb.Append("Accel_Z;");
                    this.logEnabled[2] = true;
                }
                if (this.gyroXToggle.IsChecked == true)
                {
                    logOptions |= 0x80;
                    sb.Append("Gyro_X;");
                    this.logEnabled[3] = true;
                }
                if (this.gyroYToggle.IsChecked == true)
                {
                    logOptions |= 0x40;
                    sb.Append("Gyro_Y;");
                    this.logEnabled[4] = true;
                }
                if (this.gyroZToggle.IsChecked == true)
                {
                    logOptions |= 0x20;
                    sb.Append("Gyro_Z;");
                    this.logEnabled[5] = true;
                }
                if (this.tempToggle.IsChecked == true)
                {
                    logOptions |= 0x10;
                    sb.Append("Temperature;");
                    this.logEnabled[6] = true;
                }
                if (this.rollToggle.IsChecked == true)
                {
                    logOptions |= 0x8;
                    sb.Append("Roll;");
                    this.logEnabled[7] = true;
                }
                if (this.pitchToggle.IsChecked == true)
                {
                    logOptions |= 0x4;
                    sb.Append("Pitch;");
                    this.logEnabled[8] = true;
                }
                if (this.yawToggle.IsChecked == true)
                {
                    logOptions |= 0x2;
                    sb.Append("Yaw;");
                    this.logEnabled[9] = true;
                }
                if (this.throttleToggle.IsChecked == true)
                {
                    logOptions |= 0x1;
                    sb.Append("Throttle;");
                    this.logEnabled[10] = true;
                }
            }

            sb.Append("Time\r\n");


            byte[] message = new byte[3];
            message[0] = 0x5D;
            message[1] = (byte)(logOptions >> 8);
            message[2] = (byte)(logOptions & 0xFF);

            try
            {
                dw.WriteBytes(message);
                await dw.StoreAsync();
                this.calibrateButton.IsEnabled = false;
                this.logSwitch.IsEnabled = false;
                this.startButton.IsEnabled = true;
                this.logOptionsViewer.IsEnabled = false;
            }
            catch (Exception ex)
            {

            }

            if (this.logData)
            {
                DateTime now = DateTime.Now;
                StorageFile file = await DownloadsFolder.CreateFileAsync(String.Format("log-{0}-{1}-{2}_{3}-{4}-{5}.csv", now.Day, now.Month, now.Year, now.Hour, now.Minute, now.Second));

                Task.Run(async () =>
                {
                    var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

                    using (var outputStream = stream.GetOutputStreamAt(0))
                    {
                        using (var fileWriter = new DataWriter(outputStream))
                        {
                            fileWriter.WriteString(sb.ToString());

                            MeasurementData d;
                            int c = 0;
                            double time = 0;

                            while (true)
                            {
                                if (this.queue.Count > 0 && this.queue.TryDequeue(out d))
                                {
                                    sb.Clear();
                                    if (this.logEnabled[0])
                                    {
                                        sb.Append(d.AccelX).Append(';');
                                    }
                                    if (this.logEnabled[1])
                                    {
                                        sb.Append(d.AccelY).Append(';');
                                    }
                                    if (this.logEnabled[2])
                                    {
                                        sb.Append(d.AccelZ).Append(';');
                                    }
                                    if (this.logEnabled[3])
                                    {
                                        sb.Append(d.GyroX).Append(';');
                                    }
                                    if (this.logEnabled[4])
                                    {
                                        sb.Append(d.GyroY).Append(';');
                                    }
                                    if (this.logEnabled[5])
                                    {
                                        sb.Append(d.GyroZ).Append(';');
                                    }
                                    if (this.logEnabled[6])
                                    {
                                        sb.Append(d.Temperature).Append(';');
                                    }
                                    if (this.logEnabled[7])
                                    {
                                        sb.Append(d.Roll).Append(';');
                                    }
                                    if (this.logEnabled[8])
                                    {
                                        sb.Append(d.Pitch).Append(';');
                                    }
                                    if (this.logEnabled[9])
                                    {
                                        sb.Append(d.Yaw).Append(';');
                                    }
                                    if (this.logEnabled[10])
                                    {
                                        sb.Append(d.Throttle).Append(';');
                                    }

                                    time += d.DeltaT / 1000.0;

                                    sb.Append(time).Append("\r\n");


                                    fileWriter.WriteString(sb.ToString().Replace(',', '.'));
                                    c++;

                                    if (c % 100 == 0)
                                        await fileWriter.StoreAsync();
                                }
                            }
                        }
                    }
                    stream.Dispose();
                }).ContinueWith(async (t) =>
                {
                    MessageDialog dialog = new MessageDialog(t.Exception.Message);
                    await dialog.ShowAsync();
                });
            }
        }

        private async void start_Click(object sender, RoutedEventArgs e)
        {
            byte[] message = new byte[3];
            message[0] = 0x3D;
            message[1] = 0x7;
            message[2] = 0x1;

            try
            {
                dw.WriteBytes(message);
                await dw.StoreAsync();
                this.startButton.IsEnabled = false;

                this.timer = ThreadPoolTimer.CreatePeriodicTimer(timerHandler, new TimeSpan(0, 0, 0, 0, 30));
            }
            catch (Exception ex)
            {

            }
        }

        private async void timerHandler(ThreadPoolTimer timer)
        {
            byte[] message = new byte[22];
            message[0] = 0x5D;
            message[1] = (byte)((this.throttle >> 8) & 0xFF);
            message[2] = (byte)(this.throttle & 0xFF);

            message[3] = (byte)((this.RollKp >> 8) & 0xFF);
            message[4] = (byte)(this.RollKp & 0xFF);
            message[5] = (byte)((this.RollKi >> 8) & 0xFF);
            message[6] = (byte)(this.RollKi & 0xFF);
            message[7] = (byte)((this.RollKd >> 8) & 0xFF);
            message[8] = (byte)(this.RollKd & 0xFF);

            message[9] = (byte)((this.PitchKp >> 8) & 0xFF);
            message[10] = (byte)(this.PitchKp & 0xFF);
            message[11] = (byte)((this.PitchKi >> 8) & 0xFF);
            message[12] = (byte)(this.PitchKi & 0xFF);
            message[13] = (byte)((this.PitchKd >> 8) & 0xFF);
            message[14] = (byte)(this.PitchKd & 0xFF);

            message[15] = (byte)((this.YawKp >> 8) & 0xFF);
            message[16] = (byte)(this.YawKp & 0xFF);
            message[17] = (byte)((this.YawKi >> 8) & 0xFF);
            message[18] = (byte)(this.YawKi & 0xFF);
            message[19] = (byte)((this.YawKd >> 8) & 0xFF);
            message[20] = (byte)(this.YawKd & 0xFF);

            message[21] = (byte)(this.yawInvert ? 0x01 : 0x00);

            try
            {
                dw.WriteBytes(message);
                await dw.StoreAsync();
            }
            catch (Exception ex)
            {

            }
        }

        private void log_data_Toggled(object sender, RoutedEventArgs e)
        {
            this.logData = !this.logData;
        }
    }
}
