using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;

namespace DeafAlsoPlayFps.ViewModel
{
    public partial class AudioVisualizerViewModel : ObservableObject, IDisposable
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _smoothingTimer;
        private const double MaxBarHeight = 370.0; // 能量条最大高度
        private const double SmoothingFactor = 0.8; // 平滑系数，值越大下降越慢
        private const double MinThreshold = 0.01; // 最小阈值，低于此值认为无声音

        // 事件委托，用于通知声道窗口更新
        public event Action<float, float>? LevelsUpdated;

        public event Action<DisplayMode>? DisplayModeChanged;
        public event Action<bool, double>? DirectionSettingsChanged;

        [ObservableProperty]
        private DisplayMode _displayMode = DisplayMode.All;

        partial void OnDisplayModeChanged(DisplayMode value)
        {
            // 通知外部更新显示模式
            DisplayModeChanged?.Invoke(value);
            _logger.Info($"设置显示模式: {value}");
        }
        [ObservableProperty]
        private int _leftChannelX = 100;
        [ObservableProperty]
        private int _leftChannelY = 100;
        [ObservableProperty]
        private int _rightChannelX = 100;
        [ObservableProperty]
        private int _rightChannelY = 100;
        [ObservableProperty]
        private int _topWindowX = 100;
        [ObservableProperty]
        private int _topWindowY = 100;


        [ObservableProperty]
        private double _leftChannelHeight = 0;

        [ObservableProperty]
        private double _rightChannelHeight = 0;

        // 添加可调节参数
        [ObservableProperty]
        private double _sensitivity = 1.0; // 灵敏度：1.0 = 正常，> 1.0 = 更敏感

        [ObservableProperty]
        private double _channelSeparation = 1.0; // 声道分离度：1.0 = 正常，> 1.0 = 放大左右差异

        [ObservableProperty]
        private double _gainBoost = 1.0; // 增益提升：1.0 = 正常，> 1.0 = 整体放大

        [ObservableProperty]
        private bool _directionRingEnabled = true;

        [ObservableProperty]
        private double _directionSideThresholdDb = 15.0;

        partial void OnDirectionRingEnabledChanged(bool value)
        {
            DirectionSettingsChanged?.Invoke(value, DirectionSideThresholdDb);
        }

        partial void OnDirectionSideThresholdDbChanged(double value)
        {
            DirectionSettingsChanged?.Invoke(DirectionRingEnabled, value);
        }

        private double _targetLeftHeight = 0;
        private double _targetRightHeight = 0;

        public AudioVisualizerViewModel()
        {
            // 创建平滑动画定时器
            _smoothingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // 约60FPS
            };
            _smoothingTimer.Tick += SmoothingTimer_Tick;
            _smoothingTimer.Start();
        }

        public void SetDisplayMode(DisplayMode mode)
        {
            try
            {
                // 通知外部更新显示模式
                DisplayModeChanged?.Invoke(mode);
                _logger.Info($"设置显示模式: {mode}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "设置显示模式失败");
            }
        }

        public void UpdateLevels(float leftLevel, float rightLevel)
        {
            try
            {
                // 应用增益提升
                leftLevel = Math.Min(1.0f, leftLevel * (float)GainBoost);
                rightLevel = Math.Min(1.0f, rightLevel * (float)GainBoost);

                // 应用声道分离算法
                if (ChannelSeparation > 1.0)
                {
                    // 计算左右声道的平均值作为基准
                    float average = (leftLevel + rightLevel) / 2.0f;

                    // 计算每个声道与平均值的差异
                    float leftDiff = leftLevel - average;
                    float rightDiff = rightLevel - average;

                    // 放大差异并重新计算声道值
                    leftLevel = average + leftDiff * (float)ChannelSeparation;
                    rightLevel = average + rightDiff * (float)ChannelSeparation;

                    // 确保值在合理范围内
                    leftLevel = Math.Max(0.0f, Math.Min(1.0f, leftLevel));
                    rightLevel = Math.Max(0.0f, Math.Min(1.0f, rightLevel));
                }

                // 应用灵敏度调整
                leftLevel = Math.Min(1.0f, leftLevel * (float)Sensitivity);
                rightLevel = Math.Min(1.0f, rightLevel * (float)Sensitivity);

                // 添加调试输出
                System.Diagnostics.Debug.WriteLine($"处理后音频级别 - 左: {leftLevel:F4}, 右: {rightLevel:F4} (分离度: {ChannelSeparation:F2}, 增益: {GainBoost:F2}, 灵敏度: {Sensitivity:F2})");

                // 将音频级别转换为能量条高度
                _targetLeftHeight = ConvertLevelToHeight(leftLevel);
                _targetRightHeight = ConvertLevelToHeight(rightLevel);

                // 通知声道窗口更新
                LevelsUpdated?.Invoke(leftLevel, rightLevel);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "更新音频级别失败");
            }
        }

        private double ConvertLevelToHeight(float level)
        {
            // 确保级别在0-1范围内
            level = Math.Max(0, Math.Min(1, level));

            // 应用对数缩放以更好地表示音频动态范围
            double logLevel = level > MinThreshold ? Math.Log10(level * 9 + 1) : 0;

            // 转换为像素高度
            return logLevel * MaxBarHeight;
        }

        private void SmoothingTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                LeftChannelHeight = _targetLeftHeight;
                RightChannelHeight = _targetRightHeight;
                return;
                // 使用指数平滑算法实现自然的上升和下降效果
                var newLeftHeight = _leftChannelHeight;
                var newRightHeight = _rightChannelHeight;

                UpdateChannelHeight(ref newLeftHeight, _targetLeftHeight);
                UpdateChannelHeight(ref newRightHeight, _targetRightHeight);

                // 更新属性（这会触发自动的PropertyChanged通知）
                LeftChannelHeight = newLeftHeight;
                RightChannelHeight = newRightHeight;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "平滑动画处理失败");
            }
        }

        private void UpdateChannelHeight(ref double currentHeight, double targetHeight)
        {
            double difference = targetHeight - currentHeight;

            if (Math.Abs(difference) > 0.5) // 避免无限小的变化
            {
                if (difference > 0)
                {
                    // 上升时快速响应
                    currentHeight += difference * 0.3;
                }
                else
                {
                    // 下降时使用平滑系数
                    currentHeight += difference * (1 - SmoothingFactor);
                }
            }
            else
            {
                currentHeight = targetHeight;
            }
        }

        public void Dispose()
        {
            try
            {
                if (_smoothingTimer != null)
                {
                    _smoothingTimer.Stop();
                    _smoothingTimer.Tick -= SmoothingTimer_Tick;
                }
                _logger.Info("AudioVisualizerViewModel已释放资源");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "释放AudioVisualizerViewModel资源时发生错误");
            }
        }
    }
}
