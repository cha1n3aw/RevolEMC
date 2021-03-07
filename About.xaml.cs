using System.Windows;

namespace RevolEMC
{
	public partial class About : Window
	{
		public About()
		{
			InitializeComponent();
		}

		private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			System.Diagnostics.Process.Start(e.Uri.ToString());
		}

		private void CloseCommandHandler(object sender, RoutedEventArgs e)
		{
			this.Close();
		}
	}
}
