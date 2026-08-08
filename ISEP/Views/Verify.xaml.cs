using Acr.UserDialogs;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ISEP.Views
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Verify : ContentPage
    {
        public static string payerNames { get; set; }
        public static string lgaAreas { get; set; }
        public static string paymentRefs { get; set; }
        public static string lgas { get; set; }
        public static string amounts { get; set; }
        public static string payerIds { get; set; }
        public static string streets { get; set; }
        public static string addresss { get; set; }
        public static string bizDemandIds { get; set; }
        public static string phones { get; set; }
        public static string dates { get; set; }

        public static string amtLefts { get; set; }
        public static string actualAmts { get; set; }
        public static string amtPaids { get; set; }
        public static string demandNoticeCategorys { get; set; }
        public static string taxNames { get; set; }
        public static string recordedBys { get; set; }
        public static string paymentStatuss { get; set; }

        public Verify()
        {
            InitializeComponent();

            rrrbtn.IsVisible = false;

            if (Verify.payerIds == null)
            {

                UserDialogs.Instance.Toast("RRR Verification Failed Can't Find Details Of The RRR Provided ", TimeSpan.FromSeconds(10));

                assestmentstack.IsVisible = false;
            }
            else if (Verify.payerIds != null)
            {
                assestmentstack.IsVisible = true;
                payername.Text = Verify.payerNames;
                paymentRef.Text = Verify.paymentRefs;
                phone.Text = Verify.phones;
                amount.Text = Verify.actualAmts;
                balance.Text = Verify.amtLefts;
                payerId.Text = Verify.payerIds;
                demandNoticeCategory.Text = Verify.demandNoticeCategorys;
                date.Text = Verify.dates;
                taxName.Text = Verify.taxNames;
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
        private async void CallVerifyFare()
        {
            using (IProgressDialog progress = UserDialogs.Instance.Progress("Connecting To ISEP, Please Wait.....", null, null, true, MaskType.Gradient))
            {
                for (int i = 0; i < 100; i++)
                {
                    progress.PercentComplete = i;
                    await Task.Delay(60);
                }

                if (refno.Text == null)
                {
                    await DisplayActionSheet("NOTIFICATION", "Kindly Fill in the right details", "THANK YOU");
                    await Navigation.PushModalAsync(new Views.Verify());
                }

                //PIN FORCE CHANGE
                else if (refno.Text != null)
                {
                    string url = "https://borno.osoftpay.net/api/GPayments/VerifyRef?RefNo=" + refno.Text;
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
                                    VerifyFareIdResponse result = JsonConvert.DeserializeObject<VerifyFareIdResponse>
                                        (json);

                                    if (result != null)
                                    {
                                        payerNames = result.payerName;
                                        lgaAreas = result.lgaArea;
                                        paymentRefs = result.paymentRef;
                                        lgas = result.lga;
                                        actualAmts = result.actualAmt;
                                        amtLefts = result.amtLeft;
                                        amtPaids = result.amtPaid;
                                        paymentStatuss = result.paymentStatus;
                                        payerIds = result.payerId;
                                        streets = result.street;
                                        addresss = result.address;
                                        bizDemandIds = result.bizDemandId;
                                        dates = result.date;
                                        demandNoticeCategorys = result.demandNoticeCategory;
                                        phones = result.phone;
                                        recordedBys = result.recordedBy;
                                        taxNames = result.taxName;

                                        await Navigation.PushAsync(new Views.Verify());
                                    }
                                    else
                                    {

                                        UserDialogs.Instance.Toast("Network error kindly try again or contact support", TimeSpan.FromSeconds(10));

                                    }
                                }
                            }
                        }

                    }
                    catch (Exception exe)
                    {
                        await DisplayAlert("NOTIFICATION", "Check your Internet", "TRY AGAIN");
                        exe.ToString();
                    }
                }


            }

        }
        private void TapGestureRecognizer_Tapped_2(object sender, EventArgs e)
        {
            CallVerifyFare();
        }
        internal class VerifyFareIdResponse
        {
            public string payerName { get; set; }
            public string lgaArea { get; set; }
            public string paymentRef { get; set; }
            public string lga { get; set; }
            public string amtLeft { get; set; }
            public string actualAmt { get; set; }
            public string amtPaid { get; set; }
            public string payerId { get; set; }
            public string street { get; set; }
            public string address { get; set; }
            public string bizDemandId { get; set; }
            public string phone { get; set; }
            public string date { get; set; }
            public string demandNoticeCategory { get; set; }
            public string taxName { get; set; }
            public string recordedBy { get; set; }
            public string paymentStatus { get; set; }







        }
        private async void paymentbtn_Clicked(object sender, EventArgs e)
        {
            if (Verify.payerNames == null)
            {
                await DisplayAlert("NOTIFICATION", "Kindly fill in all details before you proceed", "TRY AGAIN");

            }
            else
            {
                await Navigation.PushAsync(new Views.Payment());
            }

        }

        private void refno_Focused(object sender, FocusEventArgs e)
        {
            rrrbtn.IsVisible = true;
        }
    }
}