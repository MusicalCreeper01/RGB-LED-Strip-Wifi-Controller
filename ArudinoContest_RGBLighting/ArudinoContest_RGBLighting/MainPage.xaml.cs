using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Net.Http;
using System.Diagnostics;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ArudinoContest_RGBLighting{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 
    

    public sealed partial class MainPage : Page{

        public static MainPage CurrentPage;

        public MainPage(){
            this.InitializeComponent();
            CurrentPage = this;
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void RSliderChanged(object sender, RangeBaseValueChangedEventArgs e){
            updateColors();
        }

        private void GSliderChanged(object sender, RangeBaseValueChangedEventArgs e){
            updateColors();
        }

        private void BSliderChanged(object sender, RangeBaseValueChangedEventArgs e){
            updateColors();
        }

        //Visable range: 3080 - 4095 = 1015
        async void updateColors() {
            using (var client = new HttpClient()){
                var values = new Dictionary<string, string>{
                    { "r", sliderR.Value.ToString() },
                    { "g", sliderg.Value.ToString() },
                    { "b", sliderB.Value.ToString() }
                };

                TimeSpan timeout = new TimeSpan(0, 0, 0, 0, 100);

                client.Timeout = timeout;

                var content = new FormUrlEncodedContent(values);
                
                try
                {
                    var responce = await client.PostAsync(Globals.ip, content);

                    if (responce.Content.ToString().Contains("0"))
                    {
                        updateColors();
                    }
                    else {
                        Debug.WriteLine("update succeded");
                    }
                }
                catch (System.Net.Http.HttpRequestException ex) {

                }
            }
        }
    }
}
