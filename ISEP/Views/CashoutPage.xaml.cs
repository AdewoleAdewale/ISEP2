using Acr.UserDialogs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class CashoutPage : ContentPage
    {
        public CashoutPage()
        {
            InitializeComponent();

            if (LoginPage.Name == " ")
            {
                AgentSupervisor.Text = "  " + Dashboard.superAgent;
                Agentname.Text = "  " + Dashboard.agent;
                CashoutBalance.Text = " N" + Dashboard.cashoutBalance;


            }
            else if (LoginPage.Name != " ")
            {
                AgentSupervisor.Text = "  " + Dashboard.superAgent;
                Agentname.Text = "  " + LoginPage.Name;
                CashoutBalance.Text = "N" + Dashboard.cashoutBalance;
            }

        }



        private async void Button_Clicked(object sender, EventArgs e)
        {
            using (IProgressDialog progress = UserDialogs.Instance.Progress("Connecting to Service, Please Wait...", null, null, true, MaskType.Gradient))
            {
                for (int i = 0; i < 100; i++)
                {
                    progress.PercentComplete = i;
                    await Task.Delay(60);
                }


                string email = LoginPage.ValidUserMail;

                try
                {

                    Object mark = new JObject
                         {
                            { "Agent", email }
                         };

                    var json = JsonConvert.SerializeObject(mark);

                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    HttpClient client = new HttpClient();
                    client.DefaultRequestHeaders.Add("Super_Agent", LoginPage.Token);
                    client.DefaultRequestHeaders.Add("TradingPin", Password.Text);
                    var result = await client.PostAsync("https://collection.osoftpay.net/api/S_CashOutCall", content);

                    string json_response = await result.Content.ReadAsStringAsync();
                    var CashOutResponse = JsonConvert.DeserializeObject<CashOutResponse>(json_response);

                    if (CashOutResponse.status != "00")
                    {

                        await DisplayAlert("NOTIFICATION", CashOutResponse.message, "TRY AGAIN");

                        await Navigation.PushAsync(new Views.CashoutPage());

                    }
                    else
                    {
                        await DisplayAlert("NOTIFICATION", CashOutResponse.message + "For This Amount :" + CashOutResponse.details.amountReceived, "THANK YOU");
                        await Navigation.PushAsync(new Views.CashoutPage());

                    }

                }
                catch (Exception exe)
                {
                    UserDialogs.Instance.Toast(" Can't Process {DocType HTML Tracked} Try Again ", TimeSpan.FromSeconds(10));

                    exe.ToString();
                }




                ////call osoftpay for agent list
                //string url = "https://collection.osoftpay.net/api/S_CashOutCall";
                //try
                //{
                //    var nvc = new List<KeyValuePair<string, string>>();
                //    nvc.Add(new KeyValuePair<string, string>("Agent", email));

                //    var client = new HttpClient();

                //    client.DefaultRequestHeaders.Add("Super_Agent", LoginPage.Token);
                //    client.DefaultRequestHeaders.Add("TradingPin", Password.Text);
                //    var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(nvc) };

                //    var res = await client.SendAsync(req);

                //    var resultString = await res.Content.ReadAsStringAsync();
                //    var CashOutResponse = JsonConvert.DeserializeObject<CashOutResponse>(resultString);

                //    if (CashOutResponse.status != "00")
                //    {

                //        await DisplayAlert("NOTIFICATION", CashOutResponse.message, "TRY AGAIN");

                //        await Navigation.PushAsync(new Views.CashoutPage());

                //    }
                //    else
                //    {
                //        await DisplayAlert("NOTIFICATION", CashOutResponse.message + "For This Amount :" + CashOutResponse.details.amountReceived, "THANK YOU");
                //        await Navigation.PushAsync(new Views.CashoutPage());

                //    }
                //}

                //catch (Exception exe)
                //{
                //    UserDialogs.Instance.Toast(" Can't Process {DocType HTML Tracked} Try Again ", TimeSpan.FromSeconds(10));

                //    exe.ToString();
                //}

            }

        }


        internal class CashOutResponse
        {


            public string status { get; set; }
            public string message { get; set; }
            public Details details { get; set; }
        }

        internal class Details
        {
            public string superAgent { get; set; }
            public string amountReceived { get; set; }
            public string agent { get; set; }
        }

    }
}