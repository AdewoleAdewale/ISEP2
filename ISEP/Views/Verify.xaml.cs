using Acr.UserDialogs;
using System;
using System.Threading.Tasks;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using ISEP.Services;

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

            if (payerIds != null)
            {
                assestmentstack.IsVisible = true;
                payername.Text = payerNames;
                paymentRef.Text = paymentRefs;
                phone.Text = phones;
                amount.Text = "₦" + actualAmts;
                balance.Text = "₦" + amtLefts;
                payerId.Text = payerIds;
                date.Text = dates;
                taxName.Text = taxNames;
            }
        }

        private async void CallVerifyFare()
        {
            if (string.IsNullOrWhiteSpace(refno.Text))
            {
                await DisplayAlert("NOTIFICATION", "Kindly fill in a valid RRR number.", "OK");
                return;
            }

            using (UserDialogs.Instance.Loading("Verifying Assessment...", null, null, true))
            {
                try
                {
                    string url = $"{BrandConfig.ApiBaseUrl}/api/GPayments/VerifyRef?RefNo={refno.Text.Trim()}";
                    var result = await ApiClient.GetAsync<VerifyFareIdResponse>(url);

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

                        assestmentstack.IsVisible = true;
                        payername.Text = payerNames;
                        paymentRef.Text = paymentRefs;
                        phone.Text = phones;
                        amount.Text = "₦" + actualAmts;
                        balance.Text = "₦" + amtLefts;
                        payerId.Text = payerIds;
                        date.Text = dates;
                        taxName.Text = taxNames;
                    }
                    else
                    {
                        UserDialogs.Instance.Toast("Verification failed. Please check the RRR number.");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("NOTIFICATION", "Network error. Please check your internet connection.", "TRY AGAIN");
                    System.Diagnostics.Debug.WriteLine($"Verification error: {ex.Message}");
                }
            }
        }

        private void TapGestureRecognizer_Tapped_2(object sender, EventArgs e) => CallVerifyFare();

        private async void paymentbtn_Clicked(object sender, EventArgs e)
        {
            if (payerNames == null)
            {
                await DisplayAlert("NOTIFICATION", "Kindly verify RRR details before proceeding.", "TRY AGAIN");
            }
            else
            {
                await Navigation.PushAsync(new Views.Payment());
            }
        }

        private void refno_Focused(object sender, FocusEventArgs e) => rrrbtn.IsVisible = true;

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
    }
}