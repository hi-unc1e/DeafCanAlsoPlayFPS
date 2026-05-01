using Newtonsoft.Json;
using System.IO;
using System.Reflection;
using Common;
using System.Windows;
namespace DeafAlsoPlayFps
{
    internal class SettingsHelper
    {
        public GlobalStates? Settings { get; set; }
        // 私有静态变量来保存单例实例
        private static readonly SettingsHelper instance = new SettingsHelper();

        // 私有构造函数，防止外部实例化
        private SettingsHelper()
        {
            try
            {
                LoadSettings();
            }
            catch (Exception e)
            {
                this.Settings = new GlobalStates();
            }
        }

        // 公共静态属性来获取单例实例
        public static SettingsHelper Instance
        {
            get
            {
                return instance;
            }
        }
        public void LoadSettings()
        {
            try
            {
                this.Settings = DataPersistence.LoadData<GlobalStates>();
                if (this.Settings == null)
                {
                    this.Settings = new GlobalStates();
                }
            }
            catch (Exception e)
            {
                this.Settings = new GlobalStates();
            }
        }

        public void SaveSettings()
        {
            try
            {
                DataPersistence.SaveData(this.Settings);
            }
            catch
            {

            }
        }
    }
    public enum GameType
    {
        CS2,
        LOL,
        Valorant,
        Overwatch,
    }
    internal class GlobalStates
    {

        public GlobalStates()
        {
            // 获取主显示器尺寸
            var screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
            var screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;
            var sideHeight = 380;
            var topWidth = 380;
            // 将窗口放置在屏幕左侧中央
            var sideTop = (screenHeight - sideHeight) / 2;
            var sideWidth = 50;
            // 将窗口放置在屏幕顶部中央
            var topLeft = (screenWidth - topWidth) / 2;

            // 初始化默认值
            LeftChannelPosition = new Point(0, sideTop);
            RightChannelPosition = new Point(screenWidth - sideWidth - 10, sideTop);
            TopWindowPosition = new Point(topLeft, 20);
        }

        // 私有字段
        private readonly object _lock = new();

        private int _gameId = -1;
        private GameType _currentGame = GameType.CS2;
        private int[] _hotkey = { 119, 120 };
        public DisplayMode DisplayMode = DisplayMode.All;
        // json 忽略
        [JsonIgnore]
        public int SwitchValue = -1;
        //public bool AllowShow = true;
        // 线程安全的属性实现
        public int GameId
        {
            get { lock (_lock) { return _gameId; } }
            set
            {
                lock (_lock)
                {
                    _gameId = value;
                }
            }
        }
        public GameType CurrentGame
        {
            get { lock (_lock) { return _currentGame; } }
            set { lock (_lock) { _currentGame = value; } }
        }

        public int HotkeySwitch
        {
            get { lock (_lock) { return _hotkey[0]; } }
            set { lock (_lock) { _hotkey[0] = value; } }
        }
        public int HotkeyAdjust
        {
            get { lock (_lock) { return _hotkey[1]; } }
            set { lock (_lock) { _hotkey[1] = value; } }
        }
        //json 序列化时忽略 IsGameRunning
        [JsonIgnore]
        public volatile bool IsGameRunning = false;
        public volatile bool MainSwitch = true;

        public Point LeftChannelPosition { get; set; }
        public Point RightChannelPosition { get; set; }
        public Point TopWindowPosition { get; set; }

        public double AudioSensitivity { get; set; } = 1.0;
        public double ChannelSeparation { get; set; } = 1.0;
        public double GainBoost { get; set; } = 1.0;
        public bool DirectionRingEnabled { get; set; } = true;
        public double DirectionSideThresholdDb { get; set; } = 15.0;
    }
}
