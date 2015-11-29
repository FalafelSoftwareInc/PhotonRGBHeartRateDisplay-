using Microsoft.Band;
using Microsoft.Band.Sensors;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=391641

namespace WP8PhotonHeartRate
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const string PHOTONDEVICEID = "{YOURDEVICEID}";
        const string ACCESS_TOKEN = "{YOURACCESSTOKEN}";
        DispatcherTimer timer;
        DateTime start;
        int heartRate = 0;
        int lastHeartRate = 0;
        const int lowHeartRate = 60;
        const int highHeartRate = 140;
        private Object hrLock = new Object();

        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Stop();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.sensorTextBlock.Text = "Connecting to Band";
                // Get the list of Microsoft Bands paired to the phone.
                IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
                if (pairedBands.Length < 1)
                {
                    this.sensorTextBlock.Text = "This sample app requires a Microsoft Band paired to your phone. Also make sure that you have the latest firmware installed on your Band, as provided by the latest Microsoft Health app.";
                    return;
                }

                timer.Start();

                // Connect to Microsoft Band.
                using (IBandClient bandClient = await BandClientManager.Instance.ConnectAsync(pairedBands[0]))
                {
                    start = DateTime.Now;
                    this.sensorTextBlock.Text = "Reading heart rate sensor";

                    bandClient.SensorManager.HeartRate.ReadingChanged += HeartRate_ReadingChanged;
                    await bandClient.SensorManager.HeartRate.RequestUserConsentAsync();
                    await bandClient.SensorManager.HeartRate.StartReadingsAsync();

                    await Task.Delay(TimeSpan.FromMinutes(5));

                    await bandClient.SensorManager.HeartRate.StopReadingsAsync();
                    bandClient.SensorManager.HeartRate.ReadingChanged -= HeartRate_ReadingChanged;
                }
                this.sensorTextBlock.Text = "Done";
            }
            catch (Exception ex)
            {
                this.sensorTextBlock.Text = ex.ToString();
            }
        }

        private async void HeartRate_ReadingChanged(object sender, Microsoft.Band.Sensors.BandSensorReadingEventArgs<Microsoft.Band.Sensors.IBandHeartRateReading> e)
        {
            var span = (DateTime.Now - start).TotalSeconds;
            IBandHeartRateReading reading = e.SensorReading;
            string text = string.Format("Heartrate = {0}\nQuality = {1}\nTime Stamp = {2}\nTime Span = {3}\n", reading.HeartRate, reading.Quality, reading.Timestamp, span);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { this.sensorTextBlock.Text = text; }).AsTask();
            start = DateTime.Now;

            lock(hrLock)
            {
                heartRate = reading.HeartRate;
            }
        }

        private async void Timer_Tick(object sender, object e)
        {          
            if (heartRate != lastHeartRate)
            {
                lastHeartRate = heartRate;
                timer.Stop();

                int rValue = (int)ScaledValue(255, 0, highHeartRate, lowHeartRate, lastHeartRate, true);
                int gValue = 255 - rValue;

                SolidColorBrush color = new SolidColorBrush(new Color() {
                    R = (byte)rValue,
                    G = (byte)gValue,
                    B = 0 });

                await SetRGBHeartRate(color, lastHeartRate);

                timer.Start();
            }
        }

        private async Task SetRGBHeartRate(SolidColorBrush rgb, int heartRate)
        {
            string url = String.Format("https://api.particle.io/v1/devices/{0}/setRGBHR?access_token={1}", 
                PHOTONDEVICEID, 
                ACCESS_TOKEN);
            var request = WebRequest.Create(url);
            var postData = "value=" + string.Format("{0},{1},{2},{3}", 
                rgb.Color.R, 
                rgb.Color.G, 
                rgb.Color.B, 
                heartRate);
            var data = Encoding.UTF8.GetBytes(postData);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            using (var stream = await request.GetRequestStreamAsync())
            {
                stream.Write(data, 0, data.Length);
            }
            try
            {
                var response = await request.GetResponseAsync();
                var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            }
            catch { }
        }

        public static double ScaledValue(double max2, double min2, double max1, double min1, double v1, bool bindMinMax)
        {
            if (bindMinMax)
            {
                if (v1 > max1) v1 = max1;
                if (v1 < min1) v1 = min1;
            }
            var v2 = ((v1 - min1) * (max2 - min2) / (max1 - min1)) + min2;
            return v2;
        }
    }
}
