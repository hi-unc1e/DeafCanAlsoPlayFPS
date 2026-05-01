using DeafAlsoPlayFps.ViewModel;
using DeafAlsoPlayFps.Views;
using Linearstar.Windows.RawInput;
using Linearstar.Windows.RawInput.Native;
using NLog;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;

namespace DeafAlsoPlayFps
{
    public enum DisplayMode
    {
        All,
        TopOnly,
        SidesOnly
    }
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public class SliderValueToPositionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 4 &&
                values[0] is double currentValue &&
                values[1] is double minValue &&
                values[2] is double maxValue &&
                values[3] is double sliderWidth)
            {
                if (maxValue <= minValue) return 0;

                double ratio = (currentValue - minValue) / (maxValue - minValue);
                double position = ratio * sliderWidth - 10; // 减去TextBlock宽度的一半
                return position; // 限制在边界内
            }
            return 0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }
    }

    public class ProgressToWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var value = (double)values[0];
            var max = (double)values[1];
            var trackWidth = (double)values[2];
            var ratio = (value / max);
            ratio -= (1 - ratio) * 0.05;
            //trackWidth = trackWidth - ((1 - ratio) * 10);
            // 计算填充比例 (当前值 / 最大值)
#if DEBUG
            Debug.WriteLine($"ProgressToWidthConverter ratio:{ratio}, trackWidth:{trackWidth}");
#endif
            return ratio * trackWidth;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public partial class MainWindow : Window
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private MainViewModel _viewModel = new ViewModel.MainViewModel();
        private LayoutAdjustWindow? _layoutAdjustWindow;
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = _viewModel;
        }
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            InputMethod.SetIsInputMethodEnabled(this, false);
            InputMethod.SetInputScope(this, new InputScope());

            var windowInteropHelper = new WindowInteropHelper(this);
            var hwnd = windowInteropHelper.Handle;

            // Get the devices that can be handled with Raw Input.
            var devices = RawInputDevice.GetDevices();

            // register the keyboard device and you can register device which you need like mouse
            RawInputDevice.RegisterDevice(HidUsageAndPage.Keyboard, RawInputDeviceFlags.InputSink, hwnd);
            //RawInputDevice.RegisterDevice(HidUsageAndPage.Mouse,
            //    RawInputDeviceFlags.ExInputSink, hwnd);

            HwndSource source = HwndSource.FromHwnd(hwnd);
            source.AddHook(Hook);
        }
        private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {

            const int WM_INPUT = 0x00FF;
            const int WM_SYSKEYDOWN = 0x0104;
            const int WM_SYSKEYUP = 0x0105;
            const int VK_F10 = 0x79;

            // 拦截 F10 的系统消息，防止激活菜单栏
            if (msg == WM_SYSKEYDOWN || msg == WM_SYSKEYUP)
            {
                int vkCode = wparam.ToInt32();
                if (vkCode == VK_F10)
                {
                    handled = true; // 阻止 F10 的系统行为
                    return IntPtr.Zero;
                }
            }
            // You can read inputs by processing the WM_INPUT message.
            if (msg == WM_INPUT)
            {
                // Create an RawInputData from the handle stored in lParam.
                var data = RawInputData.FromHandle(lparam);

                // You can identify the source device using Header.DeviceHandle or just Device.
                var sourceDeviceHandle = data.Header.DeviceHandle;
                var sourceDevice = data.Device;

                if (_viewModel.AdjustWindowVisibility == Visibility.Visible)
                {
                    return IntPtr.Zero; // 如果布局调整窗口打开，忽略输入
                }

                // The data will be an instance of either RawInputMouseData, RawInputKeyboardData, or RawInputHidData.
                // They contain the raw input data in their properties.
                switch (data)
                {
                    //{Key: 50, ScanCode: 3, Flags: None}
                    //{ Key: 50, ScanCode: 3, Flags: Up}
                    //{ X: 0, Y: 0, Flags: None, Buttons: LeftButtonDown, Data: 0}
                    case RawInputMouseData mouse:
                        Debug.WriteLine(mouse.Mouse);
                        if (mouse.Mouse.Buttons == RawMouseButtonFlags.LeftButtonDown)
                        {
                            //NativeWrapper.StopAutoRun();
                        }
                        else if (mouse.Mouse.Buttons == RawMouseButtonFlags.LeftButtonUp)
                        {
                            // 鼠标左键抬起
                            //NativeWrapper.StartAutoRunAsync();
                        }
                        else if (mouse.Mouse.Flags == RawMouseFlags.None)
                        {
                            // 鼠标移动
                        }
                        break;
                    case RawInputKeyboardData keyboard:
                        {
                            if (keyboard.Keyboard.Flags != RawKeyboardFlags.None)
                            {
                                return IntPtr.Zero; // Ignore key up events
                            }
                            if (SettingsHelper.Instance == null || SettingsHelper.Instance.Settings == null)
                            {
                                return IntPtr.Zero; // Ignore if settings are not initialized
                            }
                            if (this.HotKeySwitchBorder.IsMouseOver)
                            {
                                SettingsHelper.Instance.Settings.HotkeySwitch = keyboard.Keyboard.VirutalKey;
                                HotKeySwitchText.Text = Common.Utils.Input.GetKeyName(keyboard.Keyboard.VirutalKey);
                                if (keyboard.Keyboard.VirutalKey == SettingsHelper.Instance.Settings.HotkeyAdjust)
                                {
                                    SettingsHelper.Instance.Settings.HotkeyAdjust = 0;
                                    HotKeyAdjustText.Text = "未设置";
                                }
                                return IntPtr.Zero;
                            }
                            if (this.HotKeyAdjustBorder.IsMouseOver)
                            {
                                SettingsHelper.Instance.Settings.HotkeyAdjust = keyboard.Keyboard.VirutalKey;
                                HotKeyAdjustText.Text = Common.Utils.Input.GetKeyName(keyboard.Keyboard.VirutalKey);
                                if (keyboard.Keyboard.VirutalKey == SettingsHelper.Instance.Settings.HotkeySwitch)
                                {
                                    SettingsHelper.Instance.Settings.HotkeySwitch = 0;
                                    HotKeySwitchText.Text = "未设置";
                                }
                                return IntPtr.Zero;
                            }

                            if (this.DataContext is MainViewModel viewModel && viewModel.SwitchOn != true)
                            {
                                _logger.Debug("Switch is off, ignoring keyboard input.");
                                return IntPtr.Zero;
                            }

                            if (keyboard.Keyboard.VirutalKey == SettingsHelper.Instance.Settings.HotkeySwitch)
                            {
                                _logger.Debug("HotkeySwitch pressed.");
                                if (keyboard.Keyboard.Flags == RawKeyboardFlags.None)
                                {
                                    _viewModel.ToggleAudioVisualizerVisibility();
                                }
                            }
                            if (keyboard.Keyboard.VirutalKey == SettingsHelper.Instance.Settings.HotkeyAdjust)
                            {
                                _logger.Debug("HotkeyAjust pressed.");
                                if (keyboard.Keyboard.Flags == RawKeyboardFlags.None)
                                {
                                    ShowLayoutAdjustWindow();
                                }
                            }
                        }
                        break;
                    case RawInputHidData hid:
                        Debug.WriteLine(hid.Hid);
                        break;
                }
            }

            return IntPtr.Zero;
        }
        private void ShowLayoutAdjustWindow()
        {
            _viewModel.HideVisualizer();
            if (_layoutAdjustWindow == null)
            {
                _layoutAdjustWindow = new()
                {
                    DataContext = _viewModel
                };
                _layoutAdjustWindow.Closed += LayoutWindow_Closed;
            }

            _layoutAdjustWindow.Show();
            BringWindowToFront(_layoutAdjustWindow, true);
        }

        private void LayoutWindow_Closed(object? sender, EventArgs e)
        {
            _layoutAdjustWindow = null;

            if (this.MainSwitch.IsChecked == true)
            {
                _viewModel.ShowVisualizer();
            }
            //TODO: update positions in ViewModel
        }
        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 检查是否为左键点击
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // 拖动窗口
                DragMove();
            }
        }
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsHelper.Instance == null || SettingsHelper.Instance?.Settings == null)
            {
                _logger.Error("SettingsHelper.Instance or Settings is null, cannot save settings.");
                return;
            }
            SettingsHelper.Instance.Settings.AudioSensitivity = _viewModel.AudioSensitivity;
            SettingsHelper.Instance.Settings.ChannelSeparation = _viewModel.ChannelSeparation;
            SettingsHelper.Instance.Settings.GainBoost = _viewModel.GainBoost;
            SettingsHelper.Instance.Settings.DirectionRingEnabled = _viewModel.DirectionRingEnabled;
            SettingsHelper.Instance.Settings.DirectionSideThresholdDb = _viewModel.DirectionSideThresholdDb;
            SettingsHelper.Instance.Settings.DisplayMode = (DisplayMode)_viewModel.SelectedDisplayIndex;
            SettingsHelper.Instance.Settings.MainSwitch = _viewModel.SwitchOn;
            SettingsHelper.Instance.SaveSettings();
            this.Close();
        }

        private void ResetAudioSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.AudioSensitivity = 1.0;
                    vm.ChannelSeparation = 1.0;
                    vm.GainBoost = 1.0;
                    vm.DirectionRingEnabled = true;
                    vm.DirectionSideThresholdDb = 15.0;
                }
            }
            catch (Exception ex)
            {
                // 记录错误
                System.Diagnostics.Debug.WriteLine($"重置音频设置失败: {ex.Message}");
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var instance = SettingsHelper.Instance;
            if (instance == null || instance.Settings == null)
            {
                _logger.Error("SettingsHelper.Instance or Settings is null, cannot load settings.");
                System.Windows.MessageBox.Show("Settings not initialized. Please check your configuration.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            //如果是最小化也要恢复窗口
            BringWindowToFront(this);
        }
        public void BringWindowToFront(Window window, bool topmost = false)
        {
            // 方法1：设置窗口状态和显示
            //window.WindowState = WindowState.Normal;
            window.Show();
            window.ShowInTaskbar = true;

            // 方法2：短暂设置 Topmost 然后取消（推荐）
            window.Topmost = true;
            window.Activate();
            window.Focus();

            // 使用 Dispatcher 在下一个 UI 循环中取消 Topmost
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                window.Topmost = topmost;
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 释放ViewModel资源
                if (_viewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }

                _logger.Info("主窗口已关闭，资源已释放");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "释放主窗口资源失败");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        private void StyledButton_Click(object sender, RoutedEventArgs e)
        {
            CloseButton_Click(sender, e);
        }
    }
}
