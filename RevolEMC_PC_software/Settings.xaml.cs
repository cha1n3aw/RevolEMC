using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace RevolEMC
{
	public class SettingsList
    {
		public bool _autoip = false;
		public bool invertdir = false;
		public bool autoip
		{
			get => _autoip;
            set { _autoip = value; if (value) { UDPSocket tmp = new UDPSocket(); tmp.setIP("192.168.1.100", "255.255.255.0", "192.168.1.1"); } }
		}
		public long stepsperrevolution = 400;
    }
	public partial class Settings : Window
	{
		SettingsList sl = new SettingsList();
		MainWindow.SettingsDelegate sd;

		public Settings(MainWindow.SettingsDelegate sender, SettingsList _sl)
		{
			sl = _sl;
			InitializeComponent();
			stepsperRevolution.Text = sl.stepsperrevolution.ToString();
			autoIP.IsChecked = sl.autoip;
			invertDir.IsChecked = sl.invertdir;
			sd = sender;
		}

		private void SaveSettings_Click(object sender, RoutedEventArgs e)
		{
			sl.stepsperrevolution = long.Parse(stepsperRevolution.Text);
			sl.autoip = (bool)autoIP.IsChecked;
			sl.invertdir = (bool)invertDir.IsChecked;
			sd(sl);
			Close();
		}

		private void stepsperRevolution_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}
	}
}
