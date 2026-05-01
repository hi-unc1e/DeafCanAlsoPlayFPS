using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DeafAlsoPlayFps.ViewModel;
using NLog;

namespace DeafAlsoPlayFps.Views
{
    public partial class AudioVisualizerWindow : Window
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private AudioVisualizerViewModel _viewModel;

        // 独立的左右声道窗口
        private LeftChannelWindow _leftChannelWindow;
        private RightChannelWindow _rightChannelWindow;
        private ChannelDifferenceWindow _channelDifferenceWindow;

        public AudioVisualizerWindow()
        {
            InitializeComponent();
            _viewModel = new AudioVisualizerViewModel();
            DataContext = _viewModel; // 添加这行！

            // 创建左右声道窗口和差值窗口
            _leftChannelWindow = new LeftChannelWindow();
            _rightChannelWindow = new RightChannelWindow();
            _channelDifferenceWindow = new ChannelDifferenceWindow();

            // 订阅ViewModel的更新事件
            _viewModel.LevelsUpdated += OnLevelsUpdated;
            _viewModel.DisplayModeChanged += OnDisplayModeChanged;
            _viewModel.DirectionSettingsChanged += OnDirectionSettingsChanged;

            Loaded += AudioVisualizerWindow_Loaded;
            Closing += AudioVisualizerWindow_Closing;
        }

        private void UpdateWindowsPosition()
        {
            var settings = SettingsHelper.Instance?.Settings;
            if (settings == null)
            {
                _logger.Warn("SettingsHelper.Instance.Settings is null, cannot update window positions.");
                return;
            }
            if (_leftChannelWindow != null)
            {
                _leftChannelWindow.Left = settings.LeftChannelPosition.X;
                _leftChannelWindow.Top = settings.LeftChannelPosition.Y;
            }
            if (_rightChannelWindow != null)
            {
                _rightChannelWindow.Left = settings.RightChannelPosition.X;
                _rightChannelWindow.Top = settings.RightChannelPosition.Y;
            }
            if (_channelDifferenceWindow != null)
            {
                _channelDifferenceWindow.Left = settings.TopWindowPosition.X;
                _channelDifferenceWindow.Top = settings.TopWindowPosition.Y;
            }
        }

        private void OnDisplayModeChanged(DisplayMode mode)
        {
            try
            {
                // 根据显示模式更新窗口显示状态
                switch (mode)
                {
                    case DisplayMode.All:
                        _leftChannelWindow?.Show();
                        _rightChannelWindow?.Show();
                        _channelDifferenceWindow?.Show();
                        break;
                    case DisplayMode.TopOnly:
                        _leftChannelWindow?.Hide();
                        _rightChannelWindow?.Hide();
                        _channelDifferenceWindow?.Show();
                        break;
                    case DisplayMode.SidesOnly:
                        _leftChannelWindow?.Show();
                        _rightChannelWindow?.Show();
                        _channelDifferenceWindow?.Hide();
                        break;
                }
                UpdateWindowsPosition();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "更新显示模式失败");
            }
        }
        private void OnLevelsUpdated(Point left, Point right, Point top)
        {
            try
            {
                if (_leftChannelWindow == null ||
                    _rightChannelWindow == null ||
                    _channelDifferenceWindow == null)
                {
                    _logger.Warn("窗口未初始化，无法更新显示");
                    return;
                }
                // 更新左右声道窗口的显示
                _leftChannelWindow.Left = left.X;
                _leftChannelWindow.Top = left.Y;
                _rightChannelWindow.Left = right.X;
                _rightChannelWindow.Top = right.Y;

                _channelDifferenceWindow.Top = top.Y;
                _channelDifferenceWindow.Left = top.X;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "更新窗口位置失败");
            }
        }

        private void OnLevelsUpdated(float leftLevel, float rightLevel)
        {
            try
            {
                // 更新左右声道窗口的显示
                _leftChannelWindow?.UpdateLevel(leftLevel);
                _rightChannelWindow?.UpdateLevel(rightLevel);
                // 更新声道差值窗口的显示
                _channelDifferenceWindow?.UpdateChannelDifference(leftLevel, rightLevel);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "更新声道窗口失败");
            }
        }

        private void OnDirectionSettingsChanged(bool enabled, double sideThresholdDb)
        {
            _channelDifferenceWindow?.ApplyDirectionSettings(enabled, sideThresholdDb);
        }

        private void AudioVisualizerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 隐藏管理器窗口自身
                this.Hide();

                // 显示左右声道窗口和差值窗口
                _leftChannelWindow?.Show();
                _rightChannelWindow?.Show();
                _channelDifferenceWindow?.Show();

                _logger.Info("声音可视化窗口管理器已加载，所有显示窗口已显示");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "加载声音可视化窗口失败");
            }
        }

        private void AudioVisualizerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 关闭所有窗口
                _leftChannelWindow?.Close();
                _rightChannelWindow?.Close();
                _channelDifferenceWindow?.Close();

                // 取消订阅事件
                if (_viewModel != null)
                {
                    _viewModel.LevelsUpdated -= OnLevelsUpdated;
                    _viewModel.DisplayModeChanged -= OnDisplayModeChanged;
                    _viewModel.DirectionSettingsChanged -= OnDirectionSettingsChanged;
                    _viewModel.Dispose();
                }

                _logger.Info("声音可视化窗口管理器正在关闭");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "关闭声音可视化窗口时发生错误");
            }
        }

        public void UpdateLevels(float leftLevel, float rightLevel)
        {
            _viewModel?.UpdateLevels(leftLevel, rightLevel);
        }

        public void HideVisualizer()
        {
            try
            {
                _leftChannelWindow?.Hide();
                _rightChannelWindow?.Hide();
                _channelDifferenceWindow?.Hide();
                _logger.Info("隐藏音频可视化窗口");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "隐藏音频可视化窗口失败");
            }
        }
        public void ShowVisualizer()
        {
            try
            {
                // 显示所有窗口
                OnDisplayModeChanged(_viewModel.DisplayMode);
                _logger.Info("所有音频可视化窗口已显示");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "显示音频可视化窗口失败");
            }
        }
        public void UpdateAudioLevels(float leftLevel, float rightLevel)
        {
            UpdateLevels(leftLevel, rightLevel);
        }
    }
}
