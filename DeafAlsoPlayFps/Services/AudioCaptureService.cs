using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NLog;

namespace DeafAlsoPlayFps.Services
{
    public class AudioCaptureService : IDisposable
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        private WasapiLoopbackCapture? _capture;
        private bool _isCapturing;
        private CancellationTokenSource? _cancellationTokenSource;

        public event Action<float, float>? AudioLevelChanged; // 左声道, 右声道

        public bool IsCapturing => _isCapturing;

        public void StartCapture()
        {
            if (_isCapturing)
                return;
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _capture = new WasapiLoopbackCapture();
                
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += OnRecordingStopped;

                _capture.StartRecording();
                _isCapturing = true;

                _logger.Info("音频捕获已启动");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "启动音频捕获失败");
                throw;
            }
        }

        public void StopCapture()
        {
            if (!_isCapturing)
                return;

            try
            {
                _cancellationTokenSource?.Cancel();
                _capture?.StopRecording();
                _isCapturing = false;

                _logger.Info("音频捕获已停止");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "停止音频捕获失败");
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0 || _capture == null)
                return;

            try
            {
                var format = _capture.WaveFormat;
                var samples = e.BytesRecorded / (format.BitsPerSample / 8);
                var channelSamples = samples / format.Channels;

                // 添加调试日志，显示音频格式详细信息
                _logger.Debug($"音频格式: {format.Channels}声道, {format.BitsPerSample}位, {format.SampleRate}Hz, 编码: {format.Encoding}, 字节数: {e.BytesRecorded}");

                float leftLevel = 0f;
                float rightLevel = 0f;

                if (format is { Encoding: WaveFormatEncoding.IeeeFloat, BitsPerSample: 32 })
                {
                    ProcessIeeeFloatSamples(e.Buffer, e.BytesRecorded, format.Channels, out leftLevel, out rightLevel);
                }
                else switch (format.BitsPerSample)
                {
                    case 16:
                        ProcessInt16Samples(e.Buffer, e.BytesRecorded, format.Channels, out leftLevel, out rightLevel);
                        break;
                    case 32:
                        ProcessInt32Samples(e.Buffer, e.BytesRecorded, format.Channels, out leftLevel, out rightLevel);
                        break;
                }

                AudioLevelChanged?.Invoke(leftLevel, rightLevel);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "处理音频数据失败");
            }
        }

        private void ProcessIeeeFloatSamples(byte[] buffer, int bytesRecorded, int channels, out float leftLevel, out float rightLevel)
        {
            leftLevel = 0f;
            rightLevel = 0f;
            double leftSumSquares = 0;
            double rightSumSquares = 0;
            int frameCount = 0;

            // IEEE Float 32位 = 4字节每样本
            for (int i = 0; i < bytesRecorded; i += 4 * channels)
            {
                // 处理左声道
                if (i + 3 < bytesRecorded)
                {
                    // 将4个字节转换为IEEE Float
                    float leftSample = BitConverter.ToSingle(buffer, i);
                    leftSumSquares += leftSample * leftSample;
                }

                // 处理右声道
                if (channels > 1 && i + 7 < bytesRecorded)
                {
                    // 立体声：处理独立的右声道
                    float rightSample = BitConverter.ToSingle(buffer, i + 4);
                    rightSumSquares += rightSample * rightSample;
                }

                frameCount++;
            }

            // 如果是单声道，右声道使用左声道的值
            if (channels == 1)
            {
                rightSumSquares = leftSumSquares;
            }

            ConvertRmsToLevels(leftSumSquares, rightSumSquares, frameCount, out leftLevel, out rightLevel);
        }

        private void ProcessInt16Samples(byte[] buffer, int bytesRecorded, int channels, out float leftLevel, out float rightLevel)
        {
            leftLevel = 0f;
            rightLevel = 0f;
            double leftSumSquares = 0;
            double rightSumSquares = 0;
            int frameCount = 0;

            for (int i = 0; i < bytesRecorded; i += 2 * channels)
            {
                // 处理左声道
                if (i + 1 < bytesRecorded)
                {
                    short leftSample = (short)(buffer[i] | (buffer[i + 1] << 8));
                    double leftValue = leftSample / 32768.0;
                    leftSumSquares += leftValue * leftValue;
                }

                // 处理右声道
                if (channels > 1 && i + 3 < bytesRecorded)
                {
                    // 立体声：处理独立的右声道
                    short rightSample = (short)(buffer[i + 2] | (buffer[i + 3] << 8));
                    double rightValue = rightSample / 32768.0;
                    rightSumSquares += rightValue * rightValue;
                }

                frameCount++;
            }

            // 如果是单声道，右声道使用左声道的值
            if (channels == 1)
            {
                rightSumSquares = leftSumSquares;
            }

            ConvertRmsToLevels(leftSumSquares, rightSumSquares, frameCount, out leftLevel, out rightLevel);
        }

        private void ProcessInt32Samples(byte[] buffer, int bytesRecorded, int channels, out float leftLevel, out float rightLevel)
        {
            leftLevel = 0f;
            rightLevel = 0f;
            double leftSumSquares = 0;
            double rightSumSquares = 0;
            int frameCount = 0;

            for (int i = 0; i < bytesRecorded; i += 4 * channels)
            {
                // 处理左声道
                if (i + 3 < bytesRecorded)
                {
                    int leftSample = buffer[i] | (buffer[i + 1] << 8) | (buffer[i + 2] << 16) | (buffer[i + 3] << 24);
                    double leftValue = leftSample / 2147483648.0;
                    leftSumSquares += leftValue * leftValue;
                }

                // 处理右声道
                if (channels > 1 && i + 7 < bytesRecorded)
                {
                    // 立体声：处理独立的右声道
                    int rightSample = buffer[i + 4] | (buffer[i + 5] << 8) | (buffer[i + 6] << 16) | (buffer[i + 7] << 24);
                    double rightValue = rightSample / 2147483648.0;
                    rightSumSquares += rightValue * rightValue;
                }

                frameCount++;
            }

            // 如果是单声道，右声道使用左声道的值
            if (channels == 1)
            {
                rightSumSquares = leftSumSquares;
            }

            ConvertRmsToLevels(leftSumSquares, rightSumSquares, frameCount, out leftLevel, out rightLevel);
        }

        private static void ConvertRmsToLevels(double leftSumSquares, double rightSumSquares, int frameCount, out float leftLevel, out float rightLevel)
        {
            if (frameCount <= 0)
            {
                leftLevel = 0f;
                rightLevel = 0f;
                return;
            }

            // RMS 更适合方向判断；峰值容易被枪声、环境声或混响同时拉高两侧。
            const double rmsDisplayGain = 2.0;
            leftLevel = (float)Math.Min(1.0, Math.Sqrt(leftSumSquares / frameCount) * rmsDisplayGain);
            rightLevel = (float)Math.Min(1.0, Math.Sqrt(rightSumSquares / frameCount) * rmsDisplayGain);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _isCapturing = false;
            if (e.Exception != null)
            {
                _logger.Error(e.Exception, "音频录制意外停止");
            }
        }

        public void Dispose()
        {
            StopCapture();
            _cancellationTokenSource?.Dispose();
            _capture?.Dispose();
        }
    }
}
