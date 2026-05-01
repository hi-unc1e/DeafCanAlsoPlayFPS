using CommunityToolkit.Mvvm.ComponentModel;
using DeafAlsoPlayFps.Services;
using DeafAlsoPlayFps.Views;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DeafAlsoPlayFps.ViewModel
{
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly Dispatcher _dispatcher;
        public static int toolId = 11;

        private readonly AudioCaptureService _audioCaptureService;
        private AudioVisualizerWindow? _audioVisualizerWindow;

        private bool _isAudioVisualizerVisible = false;
        private bool _isLoadingSettings;
        public MainViewModel()
        {
            LoadSettings();
            _dispatcher = Dispatcher.CurrentDispatcher;
            _audioCaptureService = new AudioCaptureService();
            _audioCaptureService.AudioLevelChanged += OnAudioLevelChanged;
        }

        [ObservableProperty]
        private bool _switchOn;

        // 音频可视化参数 - 直接存储在MainViewModel中
        [ObservableProperty]
        private double _audioSensitivity = 2.0;

        [ObservableProperty]
        private double _channelSeparation = 2.0;

        [ObservableProperty]
        private double _gainBoost = 2.0;

        [ObservableProperty]
        private bool _directionRingEnabled = true;

        [ObservableProperty]
        private double _directionSideThresholdDb = 10.0;

        [ObservableProperty]
        private int _selectedDisplayIndex;

        [ObservableProperty]
        private Visibility _adjustWindowVisibility = Visibility.Collapsed;
        partial void OnSelectedDisplayIndexChanged(int value)
        {
            DisplayMode displayMode = (DisplayMode)value;

            SyncParametersToVisualizerWindow();
            SaveAudioSettings();
        }
        // 当参数变化时，更新AudioVisualizerViewModel
        partial void OnAudioSensitivityChanged(double value)
        {
            if (_audioVisualizerWindow?.DataContext is AudioVisualizerViewModel vm)
            {
                vm.Sensitivity = value;
            }
            SaveAudioSettings();
        }

        partial void OnChannelSeparationChanged(double value)
        {
            if (_audioVisualizerWindow?.DataContext is AudioVisualizerViewModel vm)
            {
                vm.ChannelSeparation = value;
            }
            SaveAudioSettings();
        }

        partial void OnGainBoostChanged(double value)
        {
            if (_audioVisualizerWindow?.DataContext is AudioVisualizerViewModel vm)
            {
                vm.GainBoost = value;
            }
            SaveAudioSettings();
        }

        partial void OnDirectionRingEnabledChanged(bool value)
        {
            if (_audioVisualizerWindow?.DataContext is AudioVisualizerViewModel vm)
            {
                vm.DirectionRingEnabled = value;
            }
            SaveAudioSettings();
        }

        partial void OnDirectionSideThresholdDbChanged(double value)
        {
            if (_audioVisualizerWindow?.DataContext is AudioVisualizerViewModel vm)
            {
                vm.DirectionSideThresholdDb = value;
            }
            SaveAudioSettings();
        }

        // 当 SwitchOn 属性改变时触发的方法
        partial void OnSwitchOnChanged(bool value)
        {
            _logger.Info($"SwitchOn changed to: {value}");

            // 保存到设置
            if (SettingsHelper.Instance?.Settings == null)
            {
                _logger.Error("SettingsHelper.Instance.Settings is null");
                return;
            }
            SettingsHelper.Instance.Settings.MainSwitch = value;
            SettingsHelper.Instance.SaveSettings();

            // 控制音频可视化
            if (value)
            {
                StartAudioVisualization();
            }
            else
            {
                StopAudioVisualization();
            }

        }
        public void ToggleAudioVisualizerVisibility()
        {
            if (_isAudioVisualizerVisible)
            {
                _audioVisualizerWindow?.HideVisualizer();
            }
            else
            {
                _audioVisualizerWindow?.ShowVisualizer();
            }
            _isAudioVisualizerVisible = !_isAudioVisualizerVisible;
        }

        public void ShowVisualizer()
        {
            _audioVisualizerWindow?.ShowVisualizer();
        }
        public void HideVisualizer()
        {
            _audioVisualizerWindow?.HideVisualizer();
        }
        private void StartAudioVisualization()
        {
            try
            {
                // 创建可视化窗口
                if (_audioVisualizerWindow == null)
                {
                    _audioVisualizerWindow = new AudioVisualizerWindow();

                    // 立即同步当前的参数值到新创建的窗口
                    SyncParametersToVisualizerWindow();
                }

                // 启动音频捕获
                _audioCaptureService.StartCapture();

                // 显示可视化窗口
                _audioVisualizerWindow.ShowVisualizer();

                _logger.Info("音频可视化已启动");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "启动音频可视化失败");
            }
        }

        // 同步参数到可视化窗口的辅助方法
        private void SyncParametersToVisualizerWindow()
        {
            if (_audioVisualizerWindow?.DataContext is AudioVisualizerViewModel vm)
            {
                vm.Sensitivity = AudioSensitivity;
                vm.ChannelSeparation = ChannelSeparation;
                vm.GainBoost = GainBoost;
                vm.DisplayMode = (DisplayMode)SelectedDisplayIndex;
                vm.DirectionRingEnabled = DirectionRingEnabled;
                vm.DirectionSideThresholdDb = DirectionSideThresholdDb;
                _logger.Info($"参数已同步到可视化窗口: 灵敏度={AudioSensitivity:F2}, 分离度={ChannelSeparation:F2}, 增益={GainBoost:F2}, 方向圆环={DirectionRingEnabled}, 纯左右阈值={DirectionSideThresholdDb:F1}dB");
            }
            else
            {
                _logger.Warn("无法同步参数到可视化窗口：DataContext不是AudioVisualizerViewModel类型");
            }
        }

        private void StopAudioVisualization()
        {
            try
            {
                // 停止音频捕获
                _audioCaptureService.StopCapture();

                // 隐藏可视化窗口
                _audioVisualizerWindow?.HideVisualizer();

                _logger.Info("音频可视化已停止");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "停止音频可视化失败");
            }
        }

        private void OnAudioLevelChanged(float leftLevel, float rightLevel)
        {
            try
            {
                // 更新可视化窗口的音频级别
                _audioVisualizerWindow?.UpdateAudioLevels(leftLevel, rightLevel);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "更新音频级别显示失败");
            }
        }

        [ObservableProperty]
        private bool _isWinModeShuffle;

        [ObservableProperty]
        private bool _isLossModeShuffle;

        private void LoadSettings()
        {
            _isLoadingSettings = true;
            try
            {
                var settings = SettingsHelper.Instance?.Settings;
                if (settings == null)
                {
                    return;
                }

                _switchOn = settings.MainSwitch;
                _audioSensitivity = settings.AudioSensitivity;
                _channelSeparation = settings.ChannelSeparation;
                _gainBoost = settings.GainBoost;
                _directionRingEnabled = settings.DirectionRingEnabled;
                _directionSideThresholdDb = Math.Max(10.0, Math.Min(24.0, settings.DirectionSideThresholdDb));
                _selectedDisplayIndex = (int)settings.DisplayMode;
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private void SaveAudioSettings()
        {
            if (_isLoadingSettings || SettingsHelper.Instance?.Settings == null)
            {
                return;
            }

            SettingsHelper.Instance.Settings.AudioSensitivity = AudioSensitivity;
            SettingsHelper.Instance.Settings.ChannelSeparation = ChannelSeparation;
            SettingsHelper.Instance.Settings.GainBoost = GainBoost;
            SettingsHelper.Instance.Settings.DirectionRingEnabled = DirectionRingEnabled;
            SettingsHelper.Instance.Settings.DirectionSideThresholdDb = DirectionSideThresholdDb;
            SettingsHelper.Instance.Settings.DisplayMode = (DisplayMode)SelectedDisplayIndex;
            SettingsHelper.Instance.SaveSettings();
        }

        public void Dispose()
        {
            try
            {
                _audioCaptureService?.Dispose();
                _audioVisualizerWindow?.Close();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "释放资源失败");
            }
        }
    }
}
