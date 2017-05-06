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
        private int RollKp = 0;
        private int PitchKp = 0;
        private int YawKp = 0;
        private bool yawInvert = false;
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

            DateTime now = DateTime.Now;
            StorageFile file = await DownloadsFolder.CreateFileAsync(String.Format("log-{0}-{1}-{2}_{3}-{4}-{5}.csv", now.Day, now.Month, now.Year, now.Hour, now.Minute, now.Second));

            Task.Run(async () =>
            {
                var stream = await file.OpenAsync(FileAccessMode.ReadWrite);

                using (var outputStream = stream.GetOutputStreamAt(0))
                {
                    using (var fileWriter = new DataWriter(outputStream))
                    {
                        fileWriter.WriteString("Roll;Pitch;Yaw;Throttle;Time\r\n");

                        MeasurementData d;
                        int c = 0;

                        while (true)
                        {
                            if (this.queue.Count > 0 && this.queue.TryDequeue(out d))
                            {
                                fileWriter.WriteString(String.Format("{0};{1};{2};{3};{4}\r\n", d.Roll, d.Pitch, d.Yaw, d.Throttle, c * 5).Replace('.', ','));
                                c++;

                                if (c % 100 == 0)
                                    await fileWriter.StoreAsync();
                            }
                        }
                    }
                }
                stream.Dispose();
            });
        }

        private void Socket_MessageReceived(DatagramSocket sender, DatagramSocketMessageReceivedEventArgs args)
        {
            DataReader dr = args.GetDataReader();
            dr.ByteOrder = ByteOrder.BigEndian;

            double roll, pitch, yaw;
            int throttle;

            roll = dr.ReadInt32() / 65536.0;
            pitch = dr.ReadInt32() / 65536.0;
            yaw = dr.ReadInt32() / 65536.0;
            throttle = dr.ReadInt32();

            if (throttle != 0)
                throttle -= 1000;

            this.queue.Enqueue(new MeasurementData() { Roll = roll, Pitch = pitch, Yaw = yaw, Throttle = throttle });
        }

        private void rollKp_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.RollKp = (int)Math.Round(e.NewValue * 100);
        }

        private void pitchKp_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.PitchKp = (int)Math.Round(e.NewValue * 100);
        }

        private void yawKp_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            this.YawKp = (int)Math.Round(e.NewValue * 100);
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
            byte[] message = new byte[8];
            message[0] = 0x5D;
            message[1] = (byte)((this.RollKp >> 8) & 0xFF);
            message[2] = (byte)(this.RollKp & 0xFF);
            message[3] = (byte)((this.PitchKp >> 8) & 0xFF);
            message[4] = (byte)(this.PitchKp & 0xFF);
            message[5] = (byte)((this.YawKp >> 8) & 0xFF);
            message[6] = (byte)(this.YawKp & 0xFF);
            message[7] = 0x5D;

            try
            {
                dw.WriteBytes(message);
                await dw.StoreAsync();
                this.calibrateButton.IsEnabled = false;
                this.startButton.IsEnabled = true;
            }
            catch (Exception ex)
            {

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
            byte[] message = new byte[10];
            message[0] = 0x5D;
            message[1] = (byte)((this.throttle >> 8) & 0xFF);
            message[2] = (byte)(this.throttle & 0xFF);
            message[3] = (byte)((this.RollKp >> 8) & 0xFF);
            message[4] = (byte)(this.RollKp & 0xFF);
            message[5] = (byte)((this.PitchKp >> 8) & 0xFF);
            message[6] = (byte)(this.PitchKp & 0xFF);
            message[7] = (byte)((this.YawKp >> 8) & 0xFF);
            message[8] = (byte)(this.YawKp & 0xFF);
            message[9] = (byte)(this.yawInvert ? 0x01 : 0x00);

            try
            {
                dw.WriteBytes(message);
                await dw.StoreAsync();
            }
            catch (Exception ex)
            {

            }
        }
    }
}
