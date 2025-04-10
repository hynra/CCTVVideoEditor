using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CCTVVideoEditor.Views;

namespace CCTVVideoEditor
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window m_window;

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new Window();
            m_window.Title = "CCTV Video Editor";

            // Set the window content to the main page
            Frame rootFrame = new Frame();
            rootFrame.Navigate(typeof(MainPage));
            m_window.Content = rootFrame;

            m_window.Activate();
        }
    }
}