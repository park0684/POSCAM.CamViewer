using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CamViewer.Models;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewerClient.Models.Config;

namespace CamViewer.Services
{
    /// <summary>
    /// PlayerView의 재생 요청을 실제 NVR Provider에 전달하는 서비스이다.
    ///
    /// 처리 흐름:
    /// 1. PlayerPlaybackRequest 수신
    /// 2. NVR번호별 Provider 생성
    /// 3. Provider Initialize
    /// 4. NVR Login
    /// 5. 좌/우 채널 PlayByTimeAsync 실행
    /// 6. 생성된 재생 세션 보관
    /// </summary>
    public sealed class NvrPlayerPlaybackService : IPlayerPlaybackService
    {
        private readonly INvrProviderFactory _providerFactory;

        private readonly Dictionary<int, INvrProvider> _providers;
        private readonly Dictionary<int, INvrPlaybackSession> _sessions;

        private PlayerPlaybackRequest _currentRequest;
        private DateTime? _currentPlaybackTime;
        private DateTime? _playbackClockStartedAtUtc;
        private bool _disposed;
        private PlaybackSpeed _currentSpeed;

        /// <summary>
        /// 현재 재생 상태.
        /// </summary>
        public PlaybackState CurrentState { get; private set; }

        /// <summary>
        /// NvrPlayerPlaybackService를 초기화한다.
        /// </summary>
        public NvrPlayerPlaybackService(
            INvrProviderFactory providerFactory)
        {
            if (providerFactory == null)
            {
                throw new ArgumentNullException("providerFactory");
            }

            _providerFactory = providerFactory;
            _providers = new Dictionary<int, INvrProvider>();
            _sessions = new Dictionary<int, INvrPlaybackSession>();

            CurrentState = PlaybackState.Stopped;
            _currentSpeed = new PlaybackSpeed();
        }

        /// <summary>
        /// 서비스가 이미 해제되었는지 확인한다.
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    GetType().FullName);
            }
        }

        /// <summary>
        /// NVR 재생 서비스가 보유한 세션과 Provider 리소스를 정리한다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                StopAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                // 종료 정리 중 발생한 예외는 프로그램 종료 흐름을 막지 않는다.
            }

            _disposed = true;
        }

        /// <summary>
        /// 재생 요청을 시작한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> PlayAsync(
            PlayerPlaybackRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (request == null)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_REQUIRED");
            }

            if (request.Channels == null || request.Channels.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재생할 채널 정보가 없습니다.",
                    "PLAYBACK_CHANNEL_REQUIRED");
            }

            if (_currentSpeed != PlaybackSpeed.Normal)
            {
                PlayerPlaybackResult speedResult =
                    await SetPlaybackSpeedAsync(
                        _currentSpeed,
                        cancellationToken);

                if (!speedResult.Success)
                {
                    return speedResult;
                }
            }
            PlayerPlaybackResult stopResult =
                await StopAsync(cancellationToken);

            if (!stopResult.Success)
            {
                return stopResult;
            }


            CurrentState = PlaybackState.Playing;

            foreach (PlayerChannelTarget channel in request.Channels)
            {
                PlayerPlaybackResult playChannelResult =
                    await PlayChannelAsync(
                        request,
                        channel,
                        cancellationToken);

                if (!playChannelResult.Success)
                {
                    await StopAsync(cancellationToken);
                    return playChannelResult;
                }
            }
            CurrentState = PlaybackState.Playing;


            _currentRequest = request;
            _currentPlaybackTime = request.PlayStartTime;
            _playbackClockStartedAtUtc = DateTime.UtcNow;

            return PlayerPlaybackResult.Ok(
                "NVR 재생을 시작했습니다. "
                + request.PlayStartTime.ToString("yyyy-MM-dd HH:mm:ss")
                + " ~ "
                + request.PlayEndTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>
        /// 현재 재생 위치를 추정해서 반환한다.
        /// 
        /// NVR SDK에서 현재 재생 위치를 직접 제공하지 않는 단계에서는
        /// 마지막 기준 재생시각 + 실제 경과시간으로 현재 위치를 계산한다.
        /// </summary>
        private DateTime GetEstimatedPlaybackTime()
        {
            if (!_currentPlaybackTime.HasValue)
            {
                return _currentRequest == null
                    ? DateTime.Now
                    : _currentRequest.PlayStartTime;
            }

            if (CurrentState != PlaybackState.Playing)
            {
                return _currentPlaybackTime.Value;
            }

            if (!_playbackClockStartedAtUtc.HasValue)
            {
                return _currentPlaybackTime.Value;
            }

            TimeSpan elapsed =
                DateTime.UtcNow - _playbackClockStartedAtUtc.Value;

            return _currentPlaybackTime.Value.Add(elapsed);
        }

        /// <summary>
        /// 현재 재생 중인 영상 시각.
        /// </summary>
        public DateTime? CurrentPlaybackTime
        {
            get
            {
                if (_currentRequest == null)
                {
                    return null;
                }

                return GetEstimatedPlaybackTime();
            }
        }

        /// <summary>
        /// 재생을 일시정지한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> PauseAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "일시정지할 재생 세션이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrProvider provider = GetProviderByNvrNo(item.Value.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                NvrResult result =
                    await provider.PauseAsync(
                        item.Value,
                        cancellationToken);

                if (!result.Success)
                {
                    return ToPlayerResult(result);
                }
            }

            _currentPlaybackTime = GetEstimatedPlaybackTime();
            _playbackClockStartedAtUtc = null;
            CurrentState = PlaybackState.Paused;

            return PlayerPlaybackResult.Ok("일시정지했습니다.");
        }

        /// <summary>
        /// 일시정지 상태에서 재생을 재개한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> ResumeAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재개할 재생 세션이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrProvider provider = GetProviderByNvrNo(item.Value.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                NvrResult result =
                    await provider.ResumeAsync(
                        item.Value,
                        cancellationToken);

                if (!result.Success)
                {
                    return ToPlayerResult(result);
                }
            }

            _playbackClockStartedAtUtc = DateTime.UtcNow;
            CurrentState = PlaybackState.Playing;

            return PlayerPlaybackResult.Ok("재생을 재개했습니다.");
        }

        /// <summary>
        /// 현재 재생 위치를 지정 초만큼 이동한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SeekSecondsAsync(
            int seconds,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "이동할 재생 세션이 없습니다.",
                    "PLAYBACK_NOT_STARTED");
            }

            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY");
            }

            if (!_currentPlaybackTime.HasValue)
            {
                _currentPlaybackTime = _currentRequest.PlayStartTime;
            }

            DateTime currentTime =
                GetEstimatedPlaybackTime();

            DateTime targetTime =
                currentTime.AddSeconds(seconds);

            if (targetTime < _currentRequest.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 시작 시각보다 이전으로 이동할 수 없습니다.",
                    "SEEK_BEFORE_START");
            }

            if (targetTime >= _currentRequest.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 종료 시각 이후로 이동할 수 없습니다.",
                    "SEEK_AFTER_END");
            }

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrProvider provider =
                    GetProviderByNvrNo(item.Value.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                NvrResult result =
                    await provider.SeekAsync(
                        item.Value,
                        targetTime,
                        cancellationToken);

                if (!result.Success)
                {
                    return ToPlayerResult(result);
                }
            }

            _currentPlaybackTime = targetTime;
            _playbackClockStartedAtUtc = DateTime.UtcNow;
            CurrentState = PlaybackState.Playing;

            return PlayerPlaybackResult.Ok(
                "재생 위치를 이동했습니다. "
                + targetTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>
        /// 빠른재생을 요청한다.
        /// 현재 Dahua Provider 공통 구현 전 단계에서는 미지원으로 처리한다.
        /// </summary>
        public Task<PlayerPlaybackResult> FastForwardAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_sessions.Count == 0)
            {
                return Task.FromResult(
                    PlayerPlaybackResult.Fail(
                        "빠른재생할 재생 세션이 없습니다.",
                        "PLAYBACK_NOT_STARTED"));
            }

            return Task.FromResult(
                PlayerPlaybackResult.Fail(
                    "빠른재생 기능은 아직 지원되지 않습니다.",
                    "FAST_FORWARD_NOT_SUPPORTED"));
        }

        /// <summary>
        /// 빠른 역재생을 요청한다.
        /// 현재 Dahua Provider 공통 구현 전 단계에서는 미지원으로 처리한다.
        /// </summary>
        public Task<PlayerPlaybackResult> FastReverseAsync(
            CancellationToken cancellationToken)
        {
            if (_sessions.Count == 0)
            {
                return Task.FromResult(
                    PlayerPlaybackResult.Fail(
                        "빠른 역재생할 재생 세션이 없습니다.",
                        "PLAYBACK_NOT_STARTED"));
            }

            return Task.FromResult(
                PlayerPlaybackResult.Fail(
                    "빠른 역재생 기능은 아직 지원되지 않습니다.",
                    "FAST_REVERSE_NOT_SUPPORTED"));
        }
        /// <summary>
        /// 재생을 중지하고 모든 세션/Provider 리소스를 정리한다.
        /// 일부 정리 실패가 있어도 나머지 리소스 정리는 계속 수행한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> StopAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            string warningMessage = string.Empty;

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions.ToList())
            {
                try
                {
                    INvrProvider provider =
                        GetProviderByNvrNo(item.Value.NvrNo);

                    if (provider != null)
                    {
                        await provider.StopAsync(
                            item.Value,
                            cancellationToken);
                    }

                    item.Value.Dispose();
                }
                catch (Exception ex)
                {
                    warningMessage = ex.Message;
                }
            }

            _sessions.Clear();

            foreach (KeyValuePair<int, INvrProvider> item in _providers.ToList())
            {
                try
                {
                    await item.Value.LogoutAsync(cancellationToken);
                }
                catch
                {
                    // 로그아웃 실패 시에도 Dispose는 계속 진행한다.
                }

                try
                {
                    item.Value.Dispose();
                }
                catch
                {
                    // 종료 과정에서 Dispose 실패는 무시한다.
                }
            }

            _providers.Clear();

            _currentRequest = null;
            _currentPlaybackTime = null;
            _playbackClockStartedAtUtc = null;
            CurrentState = PlaybackState.Stopped;

            if (!string.IsNullOrWhiteSpace(warningMessage))
            {
                return PlayerPlaybackResult.Ok(
                    "재생은 중지되었지만 일부 정리 중 경고가 발생했습니다. "
                    + warningMessage);
            }

            return PlayerPlaybackResult.Ok("재생을 중지했습니다.");
        }

        /// <summary>
        /// 단일 좌/우 채널 재생을 시작한다.
        /// </summary>
        private async Task<PlayerPlaybackResult> PlayChannelAsync(
            PlayerPlaybackRequest request,
            PlayerChannelTarget channel,
            CancellationToken cancellationToken)
        {
            if (channel == null || channel.NvrConfig == null)
            {
                return PlayerPlaybackResult.Fail(
                    "채널에 연결된 NVR 설정이 없습니다.",
                    "NVR_CONFIG_REQUIRED");
            }

            INvrProvider provider =
                await GetOrCreateLoggedInProviderAsync(
                    channel.NvrConfig,
                    cancellationToken);

            if (provider == null)
            {
                return PlayerPlaybackResult.Fail(
                    "NVR Provider를 생성하지 못했습니다.",
                    "NVR_PROVIDER_CREATE_FAILED");
            }

            NvrPlaybackRequest nvrRequest =
                ToNvrPlaybackRequest(
                    request,
                    channel);

            NvrResult<INvrPlaybackSession> playResult =
                await provider.PlayByTimeAsync(
                    nvrRequest,
                    cancellationToken);

            if (!playResult.Success || playResult.Data == null)
            {
                return PlayerPlaybackResult.Fail(
                    string.IsNullOrWhiteSpace(playResult.Message)
                        ? "NVR 재생 요청에 실패했습니다."
                        : playResult.Message,
                    playResult.Status.ToString());
            }

            int sessionKey =
                BuildSessionKey(
                    channel.NvrNo,
                    channel.ChannelNo,
                    (int)channel.ScreenPosition);

            _sessions[sessionKey] = playResult.Data;

            return PlayerPlaybackResult.Ok("채널 재생을 시작했습니다.");
        }

        /// <summary>
        /// NVR번호 기준으로 Provider를 생성하고 로그인한다.
        /// 이미 로그인된 Provider가 있으면 재사용한다.
        /// </summary>
        private async Task<INvrProvider> GetOrCreateLoggedInProviderAsync(
            NvrConfig nvrConfig,
            CancellationToken cancellationToken)
        {
            INvrProvider provider;

            if (_providers.TryGetValue(nvrConfig.NvrNo, out provider))
            {
                return provider;
            }

            NvrResult<INvrProvider> createResult =
                _providerFactory.Create(nvrConfig.ProviderKey);

            if (!createResult.Success || createResult.Data == null)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(createResult.Message)
                        ? "NVR Provider를 찾을 수 없습니다."
                        : createResult.Message);
            }

            provider = createResult.Data;

            NvrResult initializeResult =
                provider.Initialize();

            if (!initializeResult.Success)
            {
                provider.Dispose();

                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(initializeResult.Message)
                        ? "NVR Provider 초기화에 실패했습니다."
                        : initializeResult.Message);
            }

            NvrConnectionInfo connectionInfo =
                ToConnectionInfo(nvrConfig);

            NvrResult loginResult =
                await provider.LoginAsync(
                    connectionInfo,
                    cancellationToken);

            if (!loginResult.Success)
            {
                provider.Dispose();

                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(loginResult.Message)
                        ? "NVR 로그인에 실패했습니다."
                        : loginResult.Message);
            }

            _providers[nvrConfig.NvrNo] = provider;

            return provider;
        }

        /// <summary>
        /// 로컬 NVR 설정을 NVR Core 접속 정보로 변환한다.
        /// </summary>
        private static NvrConnectionInfo ToConnectionInfo(
            NvrConfig source)
        {
            NvrConnectionType connectionType;

            if (!Enum.TryParse(
                source.ConnectionType,
                true,
                out connectionType))
            {
                connectionType = NvrConnectionType.Sdk;
            }

            var target = new NvrConnectionInfo
            {
                NvrNo = source.NvrNo,
                ProviderKey = source.ProviderKey,
                Vendor = source.Vendor,
                ConnectionType = connectionType,
                Host = source.Host,
                Port = source.Port,
                UserId = source.UserId,
                Password = source.Password,
                ChannelCount = source.ChannelCount
            };

            if (source.ProviderSettings != null)
            {
                foreach (KeyValuePair<string, string> item in source.ProviderSettings)
                {
                    target.ProviderSettings[item.Key] = item.Value;
                }
            }

            return target;
        }

        /// <summary>
        /// Player 재생 요청을 NVR Core 재생 요청으로 변환한다.
        /// </summary>
        private static NvrPlaybackRequest ToNvrPlaybackRequest(
            PlayerPlaybackRequest request,
            PlayerChannelTarget channel)
        {
            return new NvrPlaybackRequest
            {
                CounterNo = request.CounterNo,
                NvrNo = channel.NvrNo,
                ChannelNo = channel.ChannelNo,
                ScreenPosition = (int)channel.ScreenPosition,
                SearchDateTime = request.SearchDateTime,
                StartTime = request.PlayStartTime,
                EndTime = request.PlayEndTime,
                RenderTargetHandle = channel.OutputHandle,
                AutoPlay = true
            };
        }

        /// <summary>
        /// 세션 Dictionary Key를 생성한다.
        /// </summary>
        private static int BuildSessionKey(
            int nvrNo,
            int channelNo,
            int screenPosition)
        {
            return (nvrNo * 10000)
                + (channelNo * 10)
                + screenPosition;
        }

        /// <summary>
        /// 세션의 NVR번호에 해당하는 Provider를 반환한다.
        /// </summary>
        private INvrProvider GetProviderByNvrNo(
            int nvrNo)
        {
            INvrProvider provider;

            return _providers.TryGetValue(nvrNo, out provider)
                ? provider
                : null;
        }

        /// <summary>
        /// NvrResult를 PlayerPlaybackResult로 변환한다.
        /// </summary>
        private static PlayerPlaybackResult ToPlayerResult(
            NvrResult result)
        {
            if (result == null)
            {
                return PlayerPlaybackResult.Fail(
                    "NVR 처리 결과가 없습니다.",
                    "NVR_RESULT_EMPTY");
            }

            if (result.Success)
            {
                return PlayerPlaybackResult.Ok(result.Message);
            }

            return PlayerPlaybackResult.Fail(
                string.IsNullOrWhiteSpace(result.Message)
                    ? "NVR 처리에 실패했습니다."
                    : result.Message,
                result.Status.ToString());
        }

        /// <summary>
        /// 현재 재생속도를 변경한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SetPlaybackSpeedAsync(
            PlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            _currentSpeed = speed;

            if (_sessions.Count == 0)
            {
                return PlayerPlaybackResult.Ok(
                    "재생속도가 설정되었습니다. 다음 재생부터 적용됩니다.");
            }

            foreach (KeyValuePair<int, INvrPlaybackSession> item in _sessions)
            {
                INvrProvider provider =
                    GetProviderByNvrNo(item.Value.NvrNo);

                if (provider == null)
                {
                    continue;
                }

                ProviderCapabilities capabilities =
                    provider.GetCapabilities();

                if (capabilities == null || !capabilities.CanChangeSpeed)
                {
                    return PlayerPlaybackResult.Fail(
                        "선택한 NVR Provider는 재생속도 변경을 지원하지 않습니다.",
                        "PLAYBACK_SPEED_NOT_SUPPORTED");
                }

                NvrResult result =
                    await provider.SetPlaybackSpeedAsync(
                        item.Value,
                        speed,
                        cancellationToken);

                if (!result.Success)
                {
                    return ToPlayerResult(result);
                }
            }

            return PlayerPlaybackResult.Ok(
                GetPlaybackSpeedText(speed) + "으로 재생속도를 변경했습니다.");
        }

        private static string GetPlaybackSpeedText(PlaybackSpeed speed)
        {
            switch (speed)
            {
                case PlaybackSpeed.Half:
                    return "0.5배속";

                case PlaybackSpeed.Double:
                    return "2배속";

                case PlaybackSpeed.Quad:
                    return "4배속";

                case PlaybackSpeed.Octuple:
                    return "8배속";

                default:
                    return "1배속";
            }
        }
    }
}