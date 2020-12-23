using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;

namespace SpotifyRadio
{
    public partial class Form1 : Form
    {
        public ChromiumWebBrowser chromeBroswer;
        public Form1()
        {
            InitializeComponent();

            InitializeChromium();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void InitializeChromium()
        {
            CefSettings settings = new CefSettings();
            settings.CachePath = Path.GetFullPath("spotifyRadio/cache"); ;

            Cef.Initialize(settings);

            chromeBroswer = new ChromiumWebBrowser("https://gta-spotify-radio.web.app/login/");

            chromeBroswer.DisplayHandler = new CustomDisplayHandler();

            this.Controls.Add(chromeBroswer);

        }

        private void Form1_FormClosing(object sender, FormClosedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Error");
            Console.WriteLine("Error");
            Cef.Shutdown();
        }
    }

    public class CustomDisplayHandler : CefSharp.IDisplayHandler
    {
        public void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs)
        {
            string tokenLabel = "access_token=";
            int index = addressChangedArgs.Address.IndexOf(tokenLabel);
            if (index > 0)
            {
                Console.WriteLine(addressChangedArgs.Address.Substring(index + tokenLabel.Length));
            }
        }

        public bool OnAutoResize(IWebBrowser chromiumWebBrowser, IBrowser browser, CefSharp.Structs.Size newSize)
        {
            return false;
        }

        public bool OnConsoleMessage(IWebBrowser chromiumWebBrowser, ConsoleMessageEventArgs consoleMessageArgs)
        {
            return false;
        }

        public void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls)
        {

        }

        public void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen)
        {
        }

        public void OnLoadingProgressChange(IWebBrowser chromiumWebBrowser, IBrowser browser, double progress)
        {
        }

        public void OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs)
        {
        }

        public void OnTitleChanged(IWebBrowser chromiumWebBrowser, TitleChangedEventArgs titleChangedArgs)
        {
        }

        public bool OnTooltipChanged(IWebBrowser chromiumWebBrowser, ref string text)
        {
            return false;
        }
    }
}