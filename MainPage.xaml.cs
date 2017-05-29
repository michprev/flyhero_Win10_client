using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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

        public MainPage()
        {
            this.InitializeComponent();
            this.socket = new DatagramSocket();
            this.queue = new ConcurrentQueue<MeasurementData>();
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

            double roll, pitch, yaw;
            int throttle;
            int FL, BL, FR, BR;
            double rollCorrection, pitchCorrection, yawCorrection;

            roll = dr.ReadInt32() / 65536.0;
            pitch = dr.ReadInt32() / 65536.0;
            yaw = dr.ReadInt32() / 65536.0;
            throttle = dr.ReadInt32();

            if (throttle != 0)
                throttle -= 1000;

            rollCorrection = roll * this.RollKp / 100.0;
            pitchCorrection = pitch * this.PitchKp / 100.0;
            yawCorrection = yaw * this.YawKp / 100.0;

            if (!this.yawInvert)
            {
                FL = (int)(throttle + rollCorrection + pitchCorrection + yawCorrection);
                BL = (int)(throttle + rollCorrection - pitchCorrection - yawCorrection);
                FR = (int)(throttle - rollCorrection + pitchCorrection - yawCorrection);
                BR = (int)(throttle - rollCorrection - pitchCorrection + yawCorrection);
            }
            else
            {
                FL = (int)(throttle + rollCorrection + pitchCorrection - yawCorrection);
                BL = (int)(throttle + rollCorrection - pitchCorrection + yawCorrection);
                FR = (int)(throttle - rollCorrection + pitchCorrection + yawCorrection);
                BR = (int)(throttle - rollCorrection - pitchCorrection - yawCorrection);
            }

            this.queue.Enqueue(new MeasurementData() { Roll = roll, Pitch = pitch, Yaw = yaw, Throttle = throttle, FL = FL, BL = BL, FR = FR, BR = BR });
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
            byte[] message = new byte[3];
            message[0] = 0x5D;
            message[1] = (byte)(this.logData ? 0x01 : 0x00);
            message[2] = 0x5D;

            try
            {
                dw.WriteBytes(message);
                await dw.StoreAsync();
                this.calibrateButton.IsEnabled = false;
                this.logSwitch.IsEnabled = false;
                this.startButton.IsEnabled = true;
            }
            catch (Exception ex)
            {

            }

            DateTime now = DateTime.Now;
            StorageFile file = await DownloadsFolder.CreateFileAsync(String.Format("log-{0}-{1}-{2}_{3}-{4}-{5}.csv", now.Day, now.Month, now.Year, now.Hour, now.Minute, now.Second));

            if (this.logData)
            {
                Task.Run(async () =>
                {
                    var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

                    using (var outputStream = stream.GetOutputStreamAt(0))
                    {
                        using (var fileWriter = new DataWriter(outputStream))
                        {
                            fileWriter.WriteString("Roll;Pitch;Yaw;Throttle;FL;BL;FR;BR;Time\r\n");

                            MeasurementData d;
                            int c = 0;

                            while (true)
                            {
                                if (this.queue.Count > 0 && this.queue.TryDequeue(out d))
                                {
                                    fileWriter.WriteString(String.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8}\r\n", d.Roll, d.Pitch, d.Yaw, d.Throttle, d.FL, d.BL, d.FR, d.BR, c * 5).Replace('.', ','));
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
