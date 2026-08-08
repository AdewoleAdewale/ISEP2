using ISEP.Model;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ISEP.ViewModels
{
    public class OnboardingPageViewModel : INotifyPropertyChanged
    {
        readonly IList<sliderImage> source;
        public ObservableCollection<sliderImage> sliderImage { get; private set; }


        public OnboardingPageViewModel()
        {
            source = new List<sliderImage>();
            CreateMonkeyCollection();

        }

        void CreateMonkeyCollection()
        {
            source.Add(new sliderImage
            {
                ImageUrl = "https://img.freepik.com/free-vector/unemployment-insurance-abstract-concept-vector-illustration-unemployment-benefits-lost-job-tired-stressed-businessman-claim-form-workers-compensation-paper-work-interview-abstract-metaphor_335657-1355.jpg?w=826&t=st=1701351225~exp=1701351825~hmac=0c1257b18d494558ba7a38dab72e69922807d38cdedaa91c88c91dbb6621c446"
            });
            source.Add(new sliderImage
            {
                ImageUrl = "https://img.freepik.com/free-vector/sexual-education-abstract-concept-vector-illustration-sexual-health-teaching-sex-education-lesson-school-human-sexuality-emotional-relations-responsibilities-abstract-metaphor_335657-1458.jpg?w=826&t=st=1701351208~exp=1701351808~hmac=61158c0fefa15d3ff73fb4fdf74fa06d33502255758cff46c77ab71f035dbcca"
            });
            source.Add(new sliderImage
            {
                ImageUrl = "https://img.freepik.com/free-vector/net-income-calculating-abstract-concept-vector-illustration-salary-calculation-net-income-formula-take-home-pay-corporate-accounting-calculating-earnings-profit-estimation-abstract-metaphor_335657-2236.jpg?w=826&t=st=1701351278~exp=1701351878~hmac=bb65792828c5d27578e6df08cdcdc5e4b5068ff0169d92b619d4d77af283e5d5"
            });
            sliderImage = new ObservableCollection<sliderImage>(source);
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }



}
