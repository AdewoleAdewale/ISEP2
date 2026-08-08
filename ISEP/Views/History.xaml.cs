using Acr.UserDialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class History : ContentPage
    {
        class HistoryData
        {
            public string payerName { get; set; }
            public string payerId { get; set; }

            public string serviceName { get; set; }

            public string payRef { get; set; }

            public decimal amount { get; set; }

            public string dateRecorded { get; set; }


        }
        class HistoryDataHeaderFooter
        {
            public List<HistoryData> HD { get; set; }
            public string Intro { get { return " You have Performed a total of " + HD.Count + " transactions within your search dates"; } }
            public string Summary { get { return " You have Performed a total of " + HD.Count + " transactions"; } }
            public decimal Size { get { return HD.Count; } }

        }
        public History()
        {
            InitializeComponent();
        }

        private async void TapGestureRecognizer_Tapped(object sender, System.EventArgs e)
        {
            using (UserDialogs.Instance.Loading("Connecting to Service, Please Wait...", null, null, true))
            {
                await Task.Delay(1500);

                string SearchStringFrom = Convert.ToString(startDatePicker.Date.ToString("MM/dd/yyyy"));
                string SearchStringTo = Convert.ToString(endDatePicker.Date.ToString("MM/dd/yyyy"));

                //call osoftpay for agent list
                string url = "https://borno.osoftpay.net/api/GPayments/gettransaction?Email=" + LoginPage.ValidUserMail + "&SearchFrom=" + SearchStringFrom + "&SearchTo=" + SearchStringTo;
                try
                {

                    using (HttpClient client = new HttpClient())
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        using (HttpResponseMessage response = client.GetAsync(url).Result)
                        {
                            using (HttpContent content = response.Content)
                            {
                                var json = content.ReadAsStringAsync().Result;
                                MemoryStream memStream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                                // convert to string
                                StreamReader reader = new StreamReader(memStream);
                                string text = reader.ReadToEnd();
                                List<HistoryData> items = JsonConvert.DeserializeObject<List<HistoryData>>(text);

                                BindingContext = new HistoryDataHeaderFooter { HD = items };

                            }
                        }
                    }

                }
                catch (Exception exe)
                {
                    UserDialogs.Instance.Toast(" Can't Load Agent History  Now Try Again Later ", TimeSpan.FromSeconds(10));

                    exe.ToString();
                }
            }
        }

        protected override bool OnBackButtonPressed()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                using (UserDialogs.Instance.Loading("Connecting to Service, Please Wait...", null, null, true))
                {
                    await Task.Delay(10);

                    await Navigation.PushAsync(new Views.Dashboard());
                }

            });
            return true;
        }

    }
}