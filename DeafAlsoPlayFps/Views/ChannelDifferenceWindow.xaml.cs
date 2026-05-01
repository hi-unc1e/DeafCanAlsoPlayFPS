using System;
using System.Windows;
using DeafAlsoPlayFps.ViewModel;
using NLog;

namespace DeafAlsoPlayFps.Views
{
    public partial class ChannelDifferenceWindow : Window
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private ChannelDifferenceViewModel _viewModel;

        public ChannelDifferenceWindow()
        {
            InitializeComponent();
            _viewModel = new ChannelDifferenceViewModel();
            DataContext = _viewModel;
            var settings = SettingsHelper.Instance?.Settings;
            if (settings != null)
            {
                ApplyDirectionSettings(settings.DirectionRingEnabled, settings.DirectionSideThresholdDb);
            }

            // 设置窗口位置到屏幕顶部中央
            SetWindowPositionTop();

            Loaded += ChannelDifferenceWindow_Loaded;
        }

        private void SetWindowPositionTop()
        {
            try
            {
                // 获取主显示器尺寸
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;

                Point? pos = SettingsHelper.Instance?.Settings?.TopWindowPosition;
                if (pos == null || !pos.HasValue)
                {
                    // 将窗口放置在屏幕顶部中央
                    this.Left = (screenWidth - this.Width) / 2;
                    this.Top = 20; // 距离顶部20像素
                }
                else
                {
                    // 使用保存的位置
                    this.Left = pos.Value.X;
                    this.Top = pos.Value.Y;
                }

                _logger.Info($"声道差值窗口位置设置为: ({this.Left}, {this.Top})");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "设置声道差值窗口位置失败");
            }
        }

        private void ChannelDifferenceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Common.Utils.Window.MakeWindowTransparent(this);
                Common.Utils.Window.HideFromAltTab(this);
                _logger.Info("声道差值窗口已加载");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "加载声道差值窗口失败");
            }
        }

        public void UpdateChannelDifference(float leftLevel, float rightLevel)
        {
            _viewModel?.UpdateChannelDifference(leftLevel, rightLevel);
        }
        public void ApplyDirectionSettings(bool enabled, double sideThresholdDb)
        {
            _viewModel.DirectionRingEnabled = enabled;
            _viewModel.SideThresholdDb = sideThresholdDb;
            this.Height = enabled ? 172 : 96;
            MainContainer.Height = enabled ? 152 : 76;
        }
        public void UpdatePosition(double left, double top)
        {
            this.Left = left;
            this.Top = top;
        }
        public void Show(bool show)
        {
            if (show)
                this.Show();
            else
                this.Hide();
        }
    }
}
