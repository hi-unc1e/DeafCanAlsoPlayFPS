using System;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NLog;

namespace DeafAlsoPlayFps.ViewModel
{
    public partial class ChannelDifferenceViewModel : ObservableObject, IDisposable
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _smoothingTimer;
        private const double MaxBarWidth = 170.0; // 每侧最大宽度
        private const double BarAttackFactor = 0.45; // 声音出现时快速响应
        private const double BarDecayFactor = 0.18; // 声音消失后约 300ms 衰减
        private const double CenterPosition = 180.0; // 中心位置
        private const double CenterThresholdDb = 1.5;
        private const double AudibleThreshold = 0.003;
        private static readonly TimeSpan CueHoldDuration = TimeSpan.FromMilliseconds(150);
        [ObservableProperty]
        private double _leftBarWidth = 0;
        
        [ObservableProperty]
        private double _leftBarPosition = CenterPosition; // 中心位置
        
        [ObservableProperty]
        private double _rightBarWidth = 0;
        
        [ObservableProperty]
        private string _differenceText = "平衡";

        [ObservableProperty]
        private string _directionText = "等待声音";

        [ObservableProperty]
        private double _centerAlignmentOpacity = 0;

        [ObservableProperty]
        private bool _directionRingEnabled = true;

        [ObservableProperty]
        private double _sideThresholdDb = 10.0;

        [ObservableProperty]
        private double _frontDirectionOpacity = 0;

        [ObservableProperty]
        private double _backDirectionOpacity = 0;

        [ObservableProperty]
        private double _leftDirectionOpacity = 0;

        [ObservableProperty]
        private double _rightDirectionOpacity = 0;

        [ObservableProperty]
        private double _leftUpDirectionOpacity = 0;

        [ObservableProperty]
        private double _rightUpDirectionOpacity = 0;

        [ObservableProperty]
        private double _leftDownDirectionOpacity = 0;

        [ObservableProperty]
        private double _rightDownDirectionOpacity = 0;

        private double _targetLeftWidth = 0;
        private double _targetLeftPosition = CenterPosition;
        private double _targetRightWidth = 0;
        private string _targetDifferenceText = "平衡";
        private string _targetDirectionText = "等待声音";
        private double _targetCenterAlignmentOpacity = 0;
        private double _targetFrontDirectionOpacity = 0;
        private double _targetBackDirectionOpacity = 0;
        private double _targetLeftDirectionOpacity = 0;
        private double _targetRightDirectionOpacity = 0;
        private double _targetLeftUpDirectionOpacity = 0;
        private double _targetRightUpDirectionOpacity = 0;
        private double _targetLeftDownDirectionOpacity = 0;
        private double _targetRightDownDirectionOpacity = 0;

        private double _levelBaseline = 0.0;
        private double _previousTotalLevel = 0.0;
        private DateTime _barHoldUntilUtc = DateTime.MinValue;

        public ChannelDifferenceViewModel()
        {
            // 创建平滑动画定时器
            _smoothingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // 约60FPS
            };
            _smoothingTimer.Tick += SmoothingTimer_Tick;
            _smoothingTimer.Start();
        }

        public void UpdateChannelDifference(float leftLevel, float rightLevel)
        {
            try
            {
                var now = DateTime.UtcNow;
                var totalLevel = Math.Max(leftLevel, rightLevel);

                if (totalLevel < AudibleThreshold)
                {
                    if (now <= _barHoldUntilUtc)
                    {
                        return;
                    }

                    UpdateDirectionCue(leftLevel, rightLevel);
                    _targetLeftWidth = 0;
                    _targetLeftPosition = CenterPosition;
                    _targetRightWidth = 0;
                    _targetCenterAlignmentOpacity = 0;
                    _targetDifferenceText = "等待声音";
                    return;
                }

                UpdateDirectionCue(leftLevel, rightLevel);
                _barHoldUntilUtc = now + CueHoldDuration;

                var left = Math.Max(leftLevel, AudibleThreshold);
                var right = Math.Max(rightLevel, AudibleThreshold);
                var levelDiffDb = 20.0 * Math.Log10(right / left);
                var absDiffDb = Math.Abs(levelDiffDb);
                var sideThresholdDb = Math.Max(CenterThresholdDb + 1.0, SideThresholdDb);

                if (absDiffDb <= CenterThresholdDb)
                {
                    _targetLeftWidth = 0;
                    _targetLeftPosition = CenterPosition;
                    _targetRightWidth = 0;
                    _targetCenterAlignmentOpacity = 0.85;
                    _targetDifferenceText = "对准";
                }
                else if (levelDiffDb < 0)
                {
                    var intensity = GetBarIntensity(absDiffDb, sideThresholdDb);
                    _targetLeftWidth = intensity * MaxBarWidth;
                    _targetLeftPosition = CenterPosition - _targetLeftWidth;
                    _targetRightWidth = 0;
                    _targetCenterAlignmentOpacity = 0;
                    _targetDifferenceText = $"L {absDiffDb:F0}dB";
                }
                else
                {
                    var intensity = GetBarIntensity(absDiffDb, sideThresholdDb);
                    _targetLeftWidth = 0;
                    _targetLeftPosition = CenterPosition;
                    _targetRightWidth = intensity * MaxBarWidth;
                    _targetCenterAlignmentOpacity = 0;
                    _targetDifferenceText = $"R {absDiffDb:F0}dB";
                }
                
                System.Diagnostics.Debug.WriteLine($"声道差值: L={leftLevel:F3}, R={rightLevel:F3}, dB={levelDiffDb:F1}, 文本={_targetDifferenceText}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "更新声道差值失败");
            }
        }

        private static double GetBarIntensity(double absDiffDb, double sideThresholdDb)
        {
            var normalized = (absDiffDb - CenterThresholdDb) / (sideThresholdDb - CenterThresholdDb);
            normalized = Math.Max(0.0, Math.Min(1.0, normalized));
            return Math.Sqrt(normalized);
        }

        private void UpdateDirectionCue(float leftLevel, float rightLevel)
        {
            const double idleOpacity = 0;

            _targetFrontDirectionOpacity = idleOpacity;
            _targetBackDirectionOpacity = idleOpacity;
            _targetLeftDirectionOpacity = idleOpacity;
            _targetRightDirectionOpacity = idleOpacity;
            _targetLeftUpDirectionOpacity = idleOpacity;
            _targetRightUpDirectionOpacity = idleOpacity;
            _targetLeftDownDirectionOpacity = idleOpacity;
            _targetRightDownDirectionOpacity = idleOpacity;

            var totalLevel = Math.Max(leftLevel, rightLevel);
            if (totalLevel < AudibleThreshold)
            {
                _targetDirectionText = "等待声音";
                _previousTotalLevel = totalLevel;
                return;
            }

            var left = Math.Max(leftLevel, AudibleThreshold);
            var right = Math.Max(rightLevel, AudibleThreshold);
            var levelDiffDb = 20.0 * Math.Log10(right / left);
            var absDiffDb = Math.Abs(levelDiffDb);
            var sideThresholdDb = Math.Max(CenterThresholdDb + 1.0, SideThresholdDb);
            var activeOpacity = 0.55 + Math.Min(1.0, totalLevel * 1.8) * 0.35;
            var isFrontCue = IsFrontCue(totalLevel);

            // dB 差比线性声道差更接近人耳对响度差的感知。
            if (absDiffDb <= CenterThresholdDb)
            {
                if (isFrontCue)
                {
                    _targetFrontDirectionOpacity = activeOpacity;
                    _targetDirectionText = $"前 {(totalLevel * 100):F0}%";
                }
                else
                {
                    _targetBackDirectionOpacity = activeOpacity;
                    _targetDirectionText = $"后 {(totalLevel * 100):F0}%";
                }
            }
            else if (levelDiffDb <= -sideThresholdDb)
            {
                _targetLeftDirectionOpacity = activeOpacity;
                _targetDirectionText = $"左 {absDiffDb:F0}dB";
            }
            else if (levelDiffDb < -CenterThresholdDb)
            {
                if (isFrontCue)
                {
                    _targetLeftUpDirectionOpacity = activeOpacity;
                    _targetDirectionText = $"左上 {absDiffDb:F0}dB";
                }
                else
                {
                    _targetLeftDownDirectionOpacity = activeOpacity;
                    _targetDirectionText = $"左下 {absDiffDb:F0}dB";
                }
            }
            else if (levelDiffDb >= sideThresholdDb)
            {
                _targetRightDirectionOpacity = activeOpacity;
                _targetDirectionText = $"右 {absDiffDb:F0}dB";
            }
            else
            {
                if (isFrontCue)
                {
                    _targetRightUpDirectionOpacity = activeOpacity;
                    _targetDirectionText = $"右上 {absDiffDb:F0}dB";
                }
                else
                {
                    _targetRightDownDirectionOpacity = activeOpacity;
                    _targetDirectionText = $"右下 {absDiffDb:F0}dB";
                }
            }
        }

        private bool IsFrontCue(double totalLevel)
        {
            if (_levelBaseline <= 0)
            {
                _levelBaseline = totalLevel;
                _previousTotalLevel = totalLevel;
                return true;
            }

            var rising = totalLevel - _previousTotalLevel;
            var strongerThanBaseline = totalLevel >= _levelBaseline * 0.92;
            _levelBaseline = _levelBaseline * 0.94 + totalLevel * 0.06;
            _previousTotalLevel = totalLevel;

            return rising >= -0.004 && strongerThanBaseline;
        }

        private void SmoothingTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // 平滑动画处理
                var leftWidthDiff = _targetLeftWidth - LeftBarWidth;
                var leftPosDiff = _targetLeftPosition - LeftBarPosition;
                var rightWidthDiff = _targetRightWidth - RightBarWidth;

                LeftBarWidth = SmoothBarValue(LeftBarWidth, _targetLeftWidth, leftWidthDiff);
                LeftBarPosition = SmoothBarValue(LeftBarPosition, _targetLeftPosition, leftPosDiff);
                RightBarWidth = SmoothBarValue(RightBarWidth, _targetRightWidth, rightWidthDiff);

                // 更新文本（不需要平滑）
                DifferenceText = _targetDifferenceText;
                DirectionText = _targetDirectionText;
                CenterAlignmentOpacity = SmoothOpacity(CenterAlignmentOpacity, _targetCenterAlignmentOpacity);
                FrontDirectionOpacity = SmoothOpacity(FrontDirectionOpacity, _targetFrontDirectionOpacity);
                BackDirectionOpacity = SmoothOpacity(BackDirectionOpacity, _targetBackDirectionOpacity);
                LeftDirectionOpacity = SmoothOpacity(LeftDirectionOpacity, _targetLeftDirectionOpacity);
                RightDirectionOpacity = SmoothOpacity(RightDirectionOpacity, _targetRightDirectionOpacity);
                LeftUpDirectionOpacity = SmoothOpacity(LeftUpDirectionOpacity, _targetLeftUpDirectionOpacity);
                RightUpDirectionOpacity = SmoothOpacity(RightUpDirectionOpacity, _targetRightUpDirectionOpacity);
                LeftDownDirectionOpacity = SmoothOpacity(LeftDownDirectionOpacity, _targetLeftDownDirectionOpacity);
                RightDownDirectionOpacity = SmoothOpacity(RightDownDirectionOpacity, _targetRightDownDirectionOpacity);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "声道差值平滑动画处理失败");
            }
        }

        private static double SmoothOpacity(double current, double target)
        {
            var diff = target - current;
            return Math.Abs(diff) > 0.01 ? current + diff * 0.25 : target;
        }

        private static double SmoothBarValue(double current, double target, double diff)
        {
            if (Math.Abs(diff) <= 0.5)
            {
                return target;
            }

            var factor = target > current ? BarAttackFactor : BarDecayFactor;
            return current + diff * factor;
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
                _logger.Info("ChannelDifferenceViewModel已释放资源");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "释放ChannelDifferenceViewModel资源时发生错误");
            }
        }
    }
}
