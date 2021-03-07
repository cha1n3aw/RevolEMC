using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Configuration;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RevolEMC
{
    public partial class MainWindow : Window
    {
        private bool paused = false;
        private bool running = false;
        private int prevpercent = 0;
        private long steps = 0;
        private long prevtotalsteps = 0;
        private long _totalsteps = 0;
        private long totalsteps
        {
            get => _totalsteps;
            set
            {
                _totalsteps = value % (settingslist.stepsperrevolution);
                angle = (double)_totalsteps / settingslist.stepsperrevolution * 360.0;
            }
        }
        private bool _rotating = false;
        private bool rotating
        {
            get => _rotating;
            set
            {
                while (_rotating != value)
                Dispatcher.Invoke(new Action(delegate
                {
                    if (value)
                    {
                        _rotating = value;
                        rad1.IsEnabled = rad2.IsEnabled = rad3.IsEnabled = false;
                        statusLabel.Text = "Вращение";
                        if (steps >= 0 && Anim.By != 360)
                        {
                            Anim.By = 360;
                            Anim1.By = 360;
                        }
                        else if (steps <= 0 && Anim.By != -360)
                        {
                            Anim.By = -360;
                            Anim1.By = -360;
                        }
                        myStoryboard.Begin(this, true);
                        myStoryboard1.Begin(this, true);
                    }
                    else
                    {
                        _rotating = value;
                        rad1.IsEnabled = rad2.IsEnabled = rad3.IsEnabled = true;
                        statusLabel.Text = "Ожидание";
                        myStoryboard.Pause(this);
                        myStoryboard1.Pause(this);
                    }
                }));
            }
        }
        private double _angle = 0;
        private double angle
        {
            get => _angle;
            set
            {
                value = value % 360.0;
                if (value >= 0.0) _angle = value;
                else _angle = 360.0 + value;
                Output.Dispatcher.Invoke(new Action(delegate { Output.Content = _angle.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "°"; }));
            }
        }
        private readonly UDPSocket server = new UDPSocket();
        private readonly UDPSocket client = new UDPSocket();
        private readonly List<Grid> grids = new List<Grid>();
        private Thread alg;
        private XamlRadialProgressBar.RadialProgressBar progressBar;
        public delegate void SettingsDelegate(SettingsList settings);
        SettingsList settingslist = new SettingsList();

        public MainWindow()
        {
            Init();
            server.Server(8888);
            client.Client("192.168.1.1", 8888);
            InitializeComponent();
            client.ReceivedData += HandleRecievedData;
            KeyDown += new KeyEventHandler(KeyboardControlPressed);
            KeyUp += new KeyEventHandler(KeyboardControlReleased);
            testbtnAdd_Click(new object(), new RoutedEventArgs());
            PlayPauseAlg.Visibility = Visibility.Collapsed;
            tab1.IsSelected = true;
            rad2.IsChecked = true;
            angle = 0.0;
            myStoryboard.SpeedRatio = 5;
            myStoryboard1.SpeedRatio = 2;
            rotating = false;
        }

        private Thread SettingsThread(List<KeyValuePair<string, string>> settingslist)
        {
            Thread settingsthread = new Thread(() => SetSetting(settingslist)) { IsBackground = false };
            settingsthread.Start();
            return settingsthread;
        }
        private static void SetSetting(List<KeyValuePair<string, string>> settingslist)
        {
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);
            foreach (KeyValuePair<string, string> pair in settingslist) configuration.AppSettings.Settings[pair.Key].Value = pair.Value;
            configuration.Save(ConfigurationSaveMode.Full, true);
            ConfigurationManager.RefreshSection("appSettings");
        }
        private List<KeyValuePair<string, string>> SettingsList()
        {
            var settingsList = new List<KeyValuePair<string, string>>()
            {
                new KeyValuePair<string, string>("StepsPerRevolution", settingslist.stepsperrevolution.ToString()),
                new KeyValuePair<string, string>("AutoIP", settingslist.autoip.ToString())
            };
            return settingsList;
        }
        private void Init()
        {
            settingslist.autoip = Convert.ToBoolean(ConfigurationManager.AppSettings["AutoIP"]);
            settingslist.stepsperrevolution = Convert.ToInt32(ConfigurationManager.AppSettings["StepsPerRevolution"]);
        }

        private void KeyboardControlReleased(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Right:
                    { RotateCW_PreviewMouseLeftButtonUp(new object(), null); }
                    break;
                case Key.Left:
                    { RotateCCW_PreviewMouseLeftButtonUp(new object(), null); }
                    break;
            }
        }

        private void KeyboardControlPressed(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Right:
                    { if (!rotating) RotateCW_PreviewMouseLeftButtonDown(new object(), null); }
                    break;
                case Key.Left:
                    { if (!rotating) RotateCCW_PreviewMouseLeftButtonDown(new object(), null); }
                    break;
                case Key.Down:
                    { PlayPause_Click(new object(), new RoutedEventArgs()); }
                    break;
            }
        }

        private void HandleRecievedData(object sender, UDPSocket.Data data)
        {
            if (data.action == 'M' && rotating)
            {
                long elapsedsteps = steps - data.steps;
                int percentage = (int)(100 * Math.Abs(elapsedsteps) / Math.Abs(steps));
                if (prevpercent != percentage && progressBar != null)
                    progressBar.Dispatcher.Invoke(new Action(delegate
                    {
                        if (tab2.IsSelected)
                        {
                            progressBar.Value = percentage;
                            prevpercent = percentage;
                        }
                    }));
                totalsteps = prevtotalsteps + elapsedsteps;
            }
            else if (data.action == 'P') totalsteps = prevtotalsteps + steps - data.steps;
            else if (data.action == 'R') totalsteps = prevtotalsteps + steps - data.steps;
            else if (data.action == 'S')
            {
                totalsteps = prevtotalsteps + steps - data.steps;
                if (progressBar != null) tab2.Dispatcher.Invoke(new Action(delegate
                {
                    if (tab2.IsSelected) Reset_Click(new object(), new RoutedEventArgs());
                }));
                running = false;
                rotating = false;
                paused = false;
            }
            else if(data.action == 'F')
            {
                totalsteps = prevtotalsteps + steps;
                if (progressBar != null) tab2.Dispatcher.Invoke(new Action(delegate
                    {
                        if (tab2.IsSelected) progressBar.Value = 100;
                    }));
                rotating = false;
            }
        }

        private void Rotation(long _steps)
        {
            prevtotalsteps = totalsteps;
            steps = _steps;
            rotating = true;
            client.Send(_steps.ToString());
        }

        private void ForceStop()
        {
            client.Send("S");
        }

        private void ForceStop_Click(object sender, RoutedEventArgs e)
        {
            if (alg != null)
            {
                alg.Abort();
                alg = null;
            }
            ForceStop();
            steps = 0;
            prevpercent = 0;
            prevtotalsteps = 0;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            ForceStop();
            if (tab1.IsSelected)
            {
                angleBox.Text = "0.0°";
                speedBox.Text = "1.0";
            }
            else
            {
                if (progressBar != null) progressBar.Value = 0;
                progressBar = null;
                foreach (Grid gr in grids) gr.Dispatcher.Invoke(new Action(delegate
                {
                    foreach (UIElement child in gr.Children) if (((string)child.GetType().GetProperty("Name").GetValue(child, null)).Contains("dynProgress")) child.GetType().GetProperty("Value").SetValue(child, 0);
                }));
            }
        }

        private void btn1_Click(object sender, RoutedEventArgs e)
        {
            tab1.IsSelected = true;
            Style PressedStyle = FindResource("PressedButton") as Style;
            btn1.Style = PressedStyle;
            Style ReleasedStyle = FindResource("SimpleButton") as Style;
            btn2.Style = ReleasedStyle;
            PlayPauseAlg.Visibility = Visibility.Collapsed;

        }

        private void btn2_Click(object sender, RoutedEventArgs e)
        {
            tab2.IsSelected = true;
            Style PressedStyle = FindResource("PressedButton") as Style;
            btn2.Style = PressedStyle;
            Style ReleasedStyle = FindResource("SimpleButton") as Style;
            btn1.Style = ReleasedStyle;
            PlayPauseAlg.Visibility = Visibility.Visible;
        }

        private void rad1_Checked(object sender, RoutedEventArgs e)
        {
            angleBox.IsEnabled = true;
            minusAngle.IsEnabled = true;
            plusAngle.IsEnabled = true;
            PlayPause.IsEnabled = false;
        }

        private void rad2_Checked(object sender, RoutedEventArgs e)
        {
            angleBox.IsEnabled = false;
            minusAngle.IsEnabled = false;
            plusAngle.IsEnabled = false;
            PlayPause.IsEnabled = false;
        }

        private void rad3_Checked(object sender, RoutedEventArgs e)
        {
            angleBox.IsEnabled = false;
            minusAngle.IsEnabled = false;
            plusAngle.IsEnabled = false;
            PlayPause.IsEnabled = false;
        }

        private void btnHome_Click(object sender, RoutedEventArgs e)
        {
            if (totalsteps != 0) Rotation(0 - totalsteps);
        }

        private void btnSetHome_Click(object sender, RoutedEventArgs e)
        {
            totalsteps = 0;
            prevtotalsteps = 0;
            steps = 0;
            angle = 0.0;
        }

        private void minusAngle_Click(object sender, RoutedEventArgs e)
        {
            double OutVal = double.Parse(angleBox.Text.Trim('°'), System.Globalization.CultureInfo.InvariantCulture);
            OutVal -= 1;
            angleBox.Text = OutVal.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + "°";
        }

        private void plusAngle_Click(object sender, RoutedEventArgs e)
        {
            double OutVal = double.Parse(angleBox.Text.Trim('°'), System.Globalization.CultureInfo.InvariantCulture);
            OutVal += 1;
            angleBox.Text = OutVal.ToString("00.0", System.Globalization.CultureInfo.InvariantCulture) + "°";
        }

        private void angleBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            string text = angleBox.Text;
            angleBox.Dispatcher.BeginInvoke(new Action(() => 
            {
                if (new Regex(@"^([0-9]{0,3}\.?[0-9]°)$|^([0-9]{1,3}\.?[0-9]?°)$").IsMatch(text)) //^-?[0-9]{0,3}\.?[0-9]?°$
                {
                    text = text.Trim('°');
                    if (text != string.Empty && text != "-" && text != "." && double.Parse(text, System.Globalization.CultureInfo.InvariantCulture) > 360.0) angleBox.Text = "360.0°";
                }
                else angleBox.Undo();
            }));
        }

        private void minusSpeed_Click(object sender, RoutedEventArgs e)
        {
            double OutVal = double.Parse(speedBox.Text, System.Globalization.CultureInfo.InvariantCulture);
            OutVal -= 0.1;
            speedBox.Text = OutVal.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void plusSpeed_Click(object sender, RoutedEventArgs e)
        {
            double OutVal = double.Parse(speedBox.Text, System.Globalization.CultureInfo.InvariantCulture);
            OutVal += 0.1;
            speedBox.Text = OutVal.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void speedBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            Regex regex = new Regex(@"^([0-9]{0,2}\.?[0-9])$|^([0-9]{1,3}\.?[0-9]?)$"); //^[0-9]{0,2}.[0-9]?$
            speedBox.Dispatcher.BeginInvoke(new Action(() => 
            {
                if (regex.IsMatch(speedBox.Text)) client.Send($"V{(int)(settingslist.stepsperrevolution * double.Parse(speedBox.Text, System.Globalization.CultureInfo.InvariantCulture) / 60)}");
                else speedBox.Undo(); 
            }));
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (rotating)
            {
                client.Send("P");
                rotating = false;
            }
            else
            {
                client.Send("R");
                rotating = true;
            }
        }

        private void PlayPauseAlg_Click(object sender, RoutedEventArgs e)
        {
            if (!running && !rotating)
            {
                Dispatcher.Invoke(new Action(delegate
                {
                    if (progressBar != null) progressBar.Value = 0;
                    progressBar = null;
                    foreach (Grid gr in grids) foreach (UIElement child in gr.Children) if (((string)child.GetType().GetProperty("Name").GetValue(child, null)).Contains("dynProgress")) child.GetType().GetProperty("Value").SetValue(child, 0);
                }));
                alg = new Thread(ExecuteAlg) { Priority = ThreadPriority.Highest };
                alg.Start();
            }
            else if (running && paused && alg != null)
            {
                client.Send("R");
                paused = false;
            }
            else if (running && !paused && alg != null)
            {
                client.Send("P");
                paused = true;
            }
        }

        private async void ExecuteAlg()
        {
            rotating = false;
            paused = false;
            running = true;
            List<long> angles = new List<long>();
            List<int> timings = new List<int>();
            List<XamlRadialProgressBar.RadialProgressBar> bars = new List<XamlRadialProgressBar.RadialProgressBar>();
            bool secondschecked = false;
            foreach (Grid gr in grids)
                gr.Dispatcher.Invoke(new Action(delegate
                {
                    secondschecked = (bool)Seconds.IsChecked;
                    foreach (UIElement child in gr.Children)
                        if (((string)child.GetType().GetProperty("Name").GetValue(child, null)).Contains("dynTxtAngle"))
                            angles.Add((long)(settingslist.stepsperrevolution * double.Parse(child.GetType().GetProperty("Text").GetValue(child, null).ToString().Trim('°'), System.Globalization.CultureInfo.InvariantCulture) / 360));
                        else if (((string)child.GetType().GetProperty("Name").GetValue(child, null)).Contains("dynProgress")) bars.Add((XamlRadialProgressBar.RadialProgressBar)child);
                        else if (((string)child.GetType().GetProperty("Name").GetValue(child, null)).Contains("dynTxtTiming")) timings.Add(int.Parse(child.GetType().GetProperty("Text").GetValue(child, null).ToString()) * 1000);
                }));
            bool zero = false;
            FromZero.Dispatcher.Invoke(new Action(delegate { zero = (bool)FromZero.IsChecked; }));
            if (zero)
            {
                btnHome_Click(new object(), new RoutedEventArgs());
                while (rotating) await Task.Delay(50);
            }
            foreach (long a in angles)
            {
                progressBar = bars[angles.IndexOf(a)];
                Rotation(a);
                while (rotating) await Task.Delay(50);
                if (secondschecked) Thread.Sleep(timings[angles.IndexOf(a)]);
                else Thread.Sleep(timings[angles.IndexOf(a)] * 60);
                while (paused) await Task.Delay(50);
            }
            running = false;
            rotating = false;
            Dispatcher.Invoke(new Action(delegate
            {
                if (progressBar != null) progressBar.Value = 0;
                progressBar = null;
                foreach (Grid gr in grids) foreach (UIElement child in gr.Children) if (((string)child.GetType().GetProperty("Name").GetValue(child, null)).Contains("dynProgress")) child.GetType().GetProperty("Value").SetValue(child, 0);
            }));
        }

        private void RotateCW_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (rad2.IsChecked == true) PlayPause.IsEnabled = false;
            else PlayPause.IsEnabled = true;
            if (rad1.IsChecked == false) Rotation(32000);
            else if (!rotating && angleBox.Text != "°") Rotation((long)(settingslist.stepsperrevolution * double.Parse(angleBox.Text.Trim('°'), System.Globalization.CultureInfo.InvariantCulture) / 360));
        }

        private void RotateCW_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (rad2.IsChecked == true) { ForceStop(); rotating = false; }
        }

        private void RotateCCW_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (rad2.IsChecked == true) PlayPause.IsEnabled = false;
            else PlayPause.IsEnabled = true;
            if (rad1.IsChecked == false) Rotation(-32000);
            else if (!rotating && angleBox.Text != "°") Rotation(0 - (long)(settingslist.stepsperrevolution * double.Parse(angleBox.Text.Trim('°'), System.Globalization.CultureInfo.InvariantCulture) / 360));
        }

        private void RotateCCW_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (rad2.IsChecked == true) { ForceStop(); rotating = false; }
        }

        private void RefreshGrids()
        {
            Scroll.Children.Clear();
            int i = 1;
            foreach (Grid gr in grids)
            {
                foreach (UIElement child in gr.Children) if (((string)child.GetType().GetProperty("Name").GetValue(child, null)).Contains("dynLabel")) child.SetValue(ContentProperty, $"{i}.");
                Scroll.Children.Add(gr);
                i++;
            }
        }

        private void RemoveGrid(object sender, RoutedEventArgs e)
        {
            foreach (Grid gr in grids.ToList()) if (gr.Children.Contains((UIElement)sender)) grids.RemoveAt(grids.FindIndex(x => x == gr));
            RefreshGrids();
        }

        private void dynAngleBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string text = sender.GetType().GetProperty("Text").GetValue(sender, null).ToString();
            ((TextBox)sender).Dispatcher.BeginInvoke(new Action(() =>
            {
                if (new Regex(@"^(-?[0-9]{0,3}\.?[0-9]°)$|^(-?[0-9]{1,3}\.?[0-9]?°)$").IsMatch(text)) //^-?[0-9]{0,3}\.?[0-9]?°$
                {
                    text = text.Trim('°');
                    if (text != string.Empty && text != "-" && text != "." && double.Parse(text, System.Globalization.CultureInfo.InvariantCulture) > 360.0)
                    {
                        PropertyInfo fieldPropertyInfo = sender.GetType().GetProperty("Text");
                        fieldPropertyInfo.GetValue(sender, null);
                        fieldPropertyInfo.SetValue(sender, "360.0°", null);
                    }
                }
                else ((TextBox)sender).Undo();
            }));
        }

        private void dynTimingBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).Dispatcher.BeginInvoke(new Action(() => { if (!new Regex(@"^[0-9]{1,3}$").IsMatch(sender.GetType().GetProperty("Text").GetValue(sender, null).ToString())) ((TextBox)sender).Undo(); }));
        }

        private void testbtnAdd_Click(object sender, RoutedEventArgs e)
        {
            int[] widths = new int[] { 40, 70, 40, 70, 40 };
            Grid grid = new Grid() { Name = $"dynGrid{grids.Count}", Width = 260, Height = 50, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
            for (int i = 0; i < 5; i++) grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(widths[i]) });

            Label dynLabel = new Label() { Name = $"dynLabel{grids.Count}", Content = $"{grids.Count + 1}.", Style = FindResource("dynamicLabel") as Style };
            Grid.SetColumn(dynLabel, 0);
            grid.Children.Add(dynLabel);

            TextBox dynTxtAngle = new TextBox() { Name = $"dynTxtAngle{grids.Count}", Text = "0.0°", Style = FindResource("dynamicTextBox") as Style };
            dynTxtAngle.TextChanged += dynAngleBox_TextChanged;
            Grid.SetColumn(dynTxtAngle, 1);
            grid.Children.Add(dynTxtAngle);

            XamlRadialProgressBar.RadialProgressBar dynProgress = new XamlRadialProgressBar.RadialProgressBar() { Name = $"dynProgress{grids.Count}", ArcWidth = 5, Value = 0, Width = 35, Height = 35, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Background = Brushes.Transparent, Foreground = Brushes.Black, Maximum = 100 };
            Grid.SetColumn(dynProgress, 2);
            grid.Children.Add(dynProgress);

            TextBox dynTxtSpeed = new TextBox() { Name = $"dynTxtTiming{grids.Count}", Text = "0", Style = FindResource("dynamicTextBox") as Style };
            dynTxtSpeed.TextChanged += dynTimingBox_TextChanged;
            Grid.SetColumn(dynTxtSpeed, 3);
            grid.Children.Add(dynTxtSpeed);

            if (grids.Count > 0)
            {
                Button dynBtn = new Button() { Name = $"dynBtn{grids.Count}", ClickMode = ClickMode.Press, Style = FindResource("dynamicRemoveBtn") as Style };
                dynBtn.Click += RemoveGrid;
                Grid.SetColumn(dynBtn, 4);
                grid.Children.Add(dynBtn);
            }

            Thickness margin = grid.Margin;
            margin.Left = 10;
            grid.Margin = margin;

            grids.Add(grid);
            RefreshGrids();
            scroll.ScrollToEnd();
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            About about = new About();
            about.ShowDialog();
        }

        private void SetSettings(SettingsList settings)
        {
            settingslist = settings;
            SettingsThread(SettingsList());
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Settings settings = new Settings(new SettingsDelegate(SetSettings), settingslist);
            settings.ShowDialog();
        }
    }
}