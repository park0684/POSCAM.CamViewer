using CamViewer.Models;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewerClient.Enums;
using CamViewerClient.Models.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Services
{
    /// <summary>
    /// PlayerView의 재생 요청을 제조사별 고수준 재생 엔진에 전달한다.
    ///
    /// 핵심 구조:
    /// - NVR번호별로 Provider를 하나씩 생성하고 로그인 상태를 재사용한다.
    /// - Provider가 구현한 INvrPlaybackEngineProvider를 통해 재생 엔진을 생성한다.
    /// - 같은 NVR에 속한 좌우 채널을 하나의 NvrPlaybackGroupRequest로 묶는다.
    /// - 다중채널 Pause/Resume/Seek/Direction/Speed/Sync는
    ///   개별 INvrPlaybackSession이 아니라 INvrPlaybackGroupSession 단위로 처리한다.
    ///
    /// Dahua의 경우 하나의 NVR에 속한 좌우 채널이
    /// Dahua 공식 PlayGroup 하나로 구성되므로
    /// CamViewer 본체에서 채널별 SDK 명령을 순차 호출하지 않는다.
    /// </summary>
    public sealed class NvrPlayerPlaybackService :
        IPlayerPlaybackService
    {
        private readonly INvrProviderFactory _providerFactory;

        private readonly Dictionary<int, INvrProvider>
            _providers =
                new Dictionary<int, INvrProvider>();

        private readonly Dictionary<int, INvrPlaybackEngine>
            _playbackEngines =
                new Dictionary<int, INvrPlaybackEngine>();

        private readonly Dictionary<int, INvrPlaybackGroupSession>
            _playbackGroupSessions =
                new Dictionary<int, INvrPlaybackGroupSession>();

        private readonly SemaphoreSlim _commandGate =
            new SemaphoreSlim(
                1,
                1);

        private PlayerPlaybackRequest _currentRequest;
        private DateTime? _currentPlaybackTime;
        private DateTime? _playbackClockStartedAtUtc;

        private PlaybackSpeed _currentSpeed;
        private NvrPlaybackDirection _currentDirection;
        private PlaybackState _pausedFromState;

        private bool _disposed;

        public NvrPlayerPlaybackService(
            INvrProviderFactory providerFactory)
        {
            if (providerFactory == null)
            {
                throw new ArgumentNullException(
                    "providerFactory");
            }

            _providerFactory =
                providerFactory;

            CurrentState =
                PlaybackState.Stopped;

            _currentSpeed =
                PlaybackSpeed.Normal;

            _currentDirection =
                NvrPlaybackDirection.Forward;

            _pausedFromState =
                PlaybackState.Playing;
        }

        public PlaybackState CurrentState
        {
            get;
            private set;
        }

        public PlaybackSpeed CurrentPlaybackSpeed
        {
            get
            {
                return _currentSpeed;
            }
        }

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
        /// NVR번호별 그룹을 모두 Paused 상태로 준비한 뒤
        /// 그룹 단위로 동시에 Start한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> PlayAsync(
            PlayerPlaybackRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "재생 요청이 취소되었습니다.",
                    "PLAYBACK_CANCELLED");
            }

            try
            {
                return await PlayCoreAsync(
                    request,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await StopPlaybackGroupsOnlyCoreAsync()
                    .ConfigureAwait(false);

                return Cancelled(
                    "재생 요청이 취소되었습니다.",
                    "PLAYBACK_CANCELLED");
            }
            catch (Exception ex)
            {
                await StopPlaybackGroupsOnlyCoreAsync()
                    .ConfigureAwait(false);

                return PlayerPlaybackResult.Fail(
                    "NVR 재생 시작 중 오류가 발생했습니다. "
                    + ex.Message,
                    "PLAYBACK_START_EXCEPTION",
                    PlaybackFailureCategory.System);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlayerPlaybackResult> PauseAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "일시정지 요청이 취소되었습니다.",
                    "PLAYBACK_PAUSE_CANCELLED");
            }

            try
            {
                return await PauseCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "일시정지 요청이 취소되었습니다.",
                    "PLAYBACK_PAUSE_CANCELLED");
            }
            catch (Exception ex)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    PlayerPlaybackResult.Fail(
                        "재생 일시정지 중 오류가 발생했습니다. "
                        + ex.Message,
                        "PLAYBACK_PAUSE_EXCEPTION",
                        PlaybackFailureCategory.System))
                    .ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlayerPlaybackResult> ResumeAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "재생 재개 요청이 취소되었습니다.",
                    "PLAYBACK_RESUME_CANCELLED");
            }

            try
            {
                return await ResumeCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "재생 재개 요청이 취소되었습니다.",
                    "PLAYBACK_RESUME_CANCELLED");
            }
            catch (Exception ex)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    PlayerPlaybackResult.Fail(
                        "재생 재개 중 오류가 발생했습니다. "
                        + ex.Message,
                        "PLAYBACK_RESUME_EXCEPTION",
                        PlaybackFailureCategory.System))
                    .ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlayerPlaybackResult> SeekSecondsAsync(
            int seconds,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "영상 이동 요청이 취소되었습니다.",
                    "PLAYBACK_SEEK_CANCELLED");
            }

            try
            {
                if (_currentRequest == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "현재 재생 요청 정보가 없습니다.",
                        "PLAYBACK_REQUEST_EMPTY",
                        PlaybackFailureCategory.System);
                }

                DateTime? synchronizedTime =
                    await SyncPlaybackTimeCoreAsync(
                        cancellationToken)
                        .ConfigureAwait(false);

                DateTime currentTime =
                    synchronizedTime.HasValue
                        ? synchronizedTime.Value
                        : GetEstimatedPlaybackTime();

                return await SeekToTimeCoreAsync(
                    currentTime.AddSeconds(
                        seconds),
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "영상 이동 요청이 취소되었습니다.",
                    "PLAYBACK_SEEK_CANCELLED");
            }
            catch (Exception ex)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    PlayerPlaybackResult.Fail(
                        "영상 이동 중 오류가 발생했습니다. "
                        + ex.Message,
                        "PLAYBACK_SEEK_EXCEPTION",
                        PlaybackFailureCategory.System))
                    .ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlayerPlaybackResult> RewindAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "역재생 요청이 취소되었습니다.",
                    "REVERSE_PLAYBACK_CANCELLED");
            }

            try
            {
                return await ChangeDirectionCoreAsync(
                    NvrPlaybackDirection.Reverse,
                    true,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "역재생 요청이 취소되었습니다.",
                    "REVERSE_PLAYBACK_CANCELLED");
            }
            catch (Exception ex)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    PlayerPlaybackResult.Fail(
                        "역재생 전환 중 오류가 발생했습니다. "
                        + ex.Message,
                        "REVERSE_PLAYBACK_EXCEPTION",
                        PlaybackFailureCategory.System))
                    .ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        /// <summary>
        /// 정지는 취소 여부와 관계없이 네이티브 리소스를 끝까지 정리한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> StopAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            await _commandGate.WaitAsync(
                CancellationToken.None)
                .ConfigureAwait(false);

            try
            {
                return await StopCoreAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ResetPlaybackState();

                return PlayerPlaybackResult.Fail(
                    "재생 리소스 정리 중 오류가 발생했습니다. "
                    + ex.Message,
                    "PLAYBACK_STOP_EXCEPTION",
                    PlaybackFailureCategory.System);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        /// <summary>
        /// 속도 변경은 현재 재생 상태를 변경하지 않는다.
        /// Paused 상태에서 변경하면 계속 Paused를 유지한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SetPlaybackSpeedAsync(
            PlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "배속 변경 요청이 취소되었습니다.",
                    "PLAYBACK_SPEED_CANCELLED");
            }

            try
            {
                return await SetPlaybackSpeedCoreAsync(
                    speed,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "배속 변경 요청이 취소되었습니다.",
                    "PLAYBACK_SPEED_CANCELLED");
            }
            catch (Exception ex)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    PlayerPlaybackResult.Fail(
                        "배속 변경 중 오류가 발생했습니다. "
                        + ex.Message,
                        "PLAYBACK_SPEED_EXCEPTION",
                        PlaybackFailureCategory.System))
                    .ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<DateTime?> SyncPlaybackTimeAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return CurrentPlaybackTime;
            }

            try
            {
                return await SyncPlaybackTimeCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                return CurrentPlaybackTime;
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlaybackSyncStatus> GetPlaybackSyncStatusAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new PlaybackSyncStatus();
            }

            try
            {
                return await GetPlaybackSyncStatusCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                return new PlaybackSyncStatus();
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlayerPlaybackResult> ResyncPlaybackSessionsAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "영상 동기화 요청이 취소되었습니다.",
                    "PLAYBACK_SYNC_CANCELLED");
            }

            try
            {
                return await ResynchronizeCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "영상 동기화 요청이 취소되었습니다.",
                    "PLAYBACK_SYNC_CANCELLED");
            }
            catch (Exception ex)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 동기화 중 오류가 발생했습니다. "
                    + ex.Message,
                    "PLAYBACK_SYNC_EXCEPTION",
                    PlaybackFailureCategory.System);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlayerPlaybackResult> SeekToTimeAsync(
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "영상 이동 요청이 취소되었습니다.",
                    "PLAYBACK_SEEK_CANCELLED");
            }

            try
            {
                return await SeekToTimeCoreAsync(
                    targetTime,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "영상 이동 요청이 취소되었습니다.",
                    "PLAYBACK_SEEK_CANCELLED");
            }
            catch (Exception ex)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    PlayerPlaybackResult.Fail(
                        "영상 이동 중 오류가 발생했습니다. "
                        + ex.Message,
                        "PLAYBACK_SEEK_EXCEPTION",
                        PlaybackFailureCategory.System))
                    .ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        /// <summary>
        /// 타임라인 이동 후에는 기존 정책에 따라 1배속으로 정리한다.
        /// </summary>
        public async Task<PlayerPlaybackResult> SeekTimelineToTimeAsync(
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "타임라인 이동 요청이 취소되었습니다.",
                    "PLAYBACK_TIMELINE_SEEK_CANCELLED");
            }

            try
            {
                PlayerPlaybackResult seekResult =
                    await SeekToTimeCoreAsync(
                        targetTime,
                        cancellationToken)
                        .ConfigureAwait(false);

                if (seekResult == null
                    || !seekResult.Success)
                {
                    return seekResult
                        ?? PlayerPlaybackResult.Fail(
                            "타임라인 이동 결과가 없습니다.",
                            "PLAYBACK_TIMELINE_SEEK_RESULT_EMPTY",
                            PlaybackFailureCategory.System);
                }

                if (_currentSpeed
                    != PlaybackSpeed.Normal)
                {
                    PlayerPlaybackResult speedResult =
                        await SetPlaybackSpeedCoreAsync(
                            PlaybackSpeed.Normal,
                            cancellationToken)
                            .ConfigureAwait(false);

                    if (speedResult == null
                        || !speedResult.Success)
                    {
                        return speedResult
                            ?? PlayerPlaybackResult.Fail(
                                "타임라인 이동 후 1배속 적용 결과가 없습니다.",
                                "PLAYBACK_TIMELINE_SPEED_RESULT_EMPTY",
                                PlaybackFailureCategory.System);
                    }
                }

                return PlayerPlaybackResult.Ok(
                    "선택한 영상시간으로 이동했습니다. "
                    + NormalizeSeekTarget(
                        targetTime)
                        .ToString(
                            "yyyy-MM-dd HH:mm:ss"));
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "타임라인 이동 요청이 취소되었습니다.",
                    "PLAYBACK_TIMELINE_SEEK_CANCELLED");
            }
            catch (Exception ex)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    PlayerPlaybackResult.Fail(
                        "타임라인 이동 중 오류가 발생했습니다. "
                        + ex.Message,
                        "PLAYBACK_TIMELINE_SEEK_EXCEPTION",
                        PlaybackFailureCategory.System))
                    .ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlayerVideoSourceInfoResult> GetVideoSourceInfoAsync(
            PlayerChannelTarget channel,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel == null
                        ? ScreenPosition.Left
                        : channel.ScreenPosition,
                    "영상 원본 정보 조회가 취소되었습니다.");
            }

            try
            {
                return await GetVideoSourceInfoCoreAsync(
                    channel,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel == null
                        ? ScreenPosition.Left
                        : channel.ScreenPosition,
                    "영상 원본 정보 조회가 취소되었습니다.");
            }
            catch (Exception ex)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel == null
                        ? ScreenPosition.Left
                        : channel.ScreenPosition,
                    "영상 원본 정보 조회 중 오류가 발생했습니다. "
                    + ex.Message);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        public async Task<PlayerPlaybackResult>
            PlayForwardFromCurrentTimeAsync(
                CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            try
            {
                await _commandGate.WaitAsync(
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "정방향 전환 요청이 취소되었습니다.",
                    "FORWARD_PLAYBACK_CANCELLED");
            }

            try
            {
                return await ChangeDirectionCoreAsync(
                    NvrPlaybackDirection.Forward,
                    true,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(
                    "정방향 전환 요청이 취소되었습니다.",
                    "FORWARD_PLAYBACK_CANCELLED");
            }
            catch (Exception ex)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    PlayerPlaybackResult.Fail(
                        "정방향 전환 중 오류가 발생했습니다. "
                        + ex.Message,
                        "FORWARD_PLAYBACK_EXCEPTION",
                        PlaybackFailureCategory.System))
                    .ConfigureAwait(false);
            }
            finally
            {
                _commandGate.Release();
            }
        }

        private async Task<PlayerPlaybackResult> PlayCoreAsync(
            PlayerPlaybackRequest request,
            CancellationToken cancellationToken)
        {
            PlayerPlaybackResult validationResult =
                ValidatePlaybackRequest(
                    request);

            if (!validationResult.Success)
            {
                return validationResult;
            }

            PlaybackSpeed selectedSpeed =
                _currentSpeed;

            PlayerPlaybackResult cleanupResult =
                await StopPlaybackGroupsOnlyCoreAsync()
                    .ConfigureAwait(false);

            if (!cleanupResult.Success)
            {
                return cleanupResult;
            }

            _currentSpeed =
                selectedSpeed;

            _currentRequest =
                request;

            _currentPlaybackTime =
                request.PlayStartTime;

            _playbackClockStartedAtUtc =
                null;

            CurrentState =
                PlaybackState.Stopped;

            _currentDirection =
                NvrPlaybackDirection.Forward;

            _pausedFromState =
                PlaybackState.Playing;

            List<IGrouping<int, PlayerChannelTarget>>
                nvrGroups =
                    request.Channels
                        .Where(
                            channel =>
                                channel != null)
                        .GroupBy(
                            channel =>
                                channel.NvrNo)
                        .OrderBy(
                            group =>
                                group.Key)
                        .ToList();

            /*
             * 모든 NVR 그룹을 먼저 Paused 상태로 Open한다.
             * 모든 Open이 끝나기 전에는 어느 그룹도 Start하지 않는다.
             */
            foreach (IGrouping<int, PlayerChannelTarget> nvrGroup
                in nvrGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<PlayerChannelTarget> channels =
                    nvrGroup.ToList();

                PlayerChannelTarget firstChannel =
                    channels.FirstOrDefault();

                if (firstChannel == null
                    || firstChannel.NvrConfig == null)
                {
                    return await RecoverFromGroupCommandFailureCoreAsync(
                        PlayerPlaybackResult.Fail(
                            "NVR 그룹 설정 정보가 없습니다. "
                            + "NvrNo="
                            + nvrGroup.Key,
                            "NVR_CONFIG_REQUIRED",
                            PlaybackFailureCategory.Configuration))
                        .ConfigureAwait(false);
                }

                NvrConfig nvrConfig =
                    firstChannel.NvrConfig;

                bool mixedConfig =
                    channels.Any(
                        channel =>
                            channel.NvrConfig == null
                            || channel.NvrConfig.NvrNo
                                != nvrConfig.NvrNo
                            || !string.Equals(
                                channel.NvrConfig.ProviderKey,
                                nvrConfig.ProviderKey,
                                StringComparison.OrdinalIgnoreCase)
                            || !string.Equals(
                                channel.NvrConfig.Host,
                                nvrConfig.Host,
                                StringComparison.OrdinalIgnoreCase)
                            || channel.NvrConfig.Port
                                != nvrConfig.Port);

                if (mixedConfig)
                {
                    return await RecoverFromGroupCommandFailureCoreAsync(
                        PlayerPlaybackResult.Fail(
                            "같은 NVR번호의 채널에 서로 다른 NVR 설정이 연결되어 있습니다. "
                            + "NvrNo="
                            + nvrGroup.Key,
                            "NVR_GROUP_CONFIG_MISMATCH",
                            PlaybackFailureCategory.Configuration))
                        .ConfigureAwait(false);
                }

                NvrResult<INvrProvider> providerResult =
                    await GetOrCreateLoggedInProviderCoreAsync(
                        nvrConfig,
                        cancellationToken)
                        .ConfigureAwait(false);

                if (providerResult == null
                    || !providerResult.Success
                    || providerResult.Data == null)
                {
                    return await RecoverFromGroupCommandFailureCoreAsync(
                        ToPlayerResult(
                            providerResult,
                            "NVR_PROVIDER_CONNECTION_FAILED"))
                        .ConfigureAwait(false);
                }

                INvrProvider provider =
                    providerResult.Data;

                INvrPlaybackEngineProvider engineProvider =
                    provider
                        as INvrPlaybackEngineProvider;

                if (engineProvider == null)
                {
                    return await RecoverFromGroupCommandFailureCoreAsync(
                        PlayerPlaybackResult.Fail(
                            "현재 NVR Provider는 그룹 재생 엔진을 구현하지 않았습니다. "
                            + "ProviderKey="
                            + nvrConfig.ProviderKey,
                            "PLAYBACK_ENGINE_PROVIDER_NOT_IMPLEMENTED",
                            PlaybackFailureCategory.NotSupported))
                        .ConfigureAwait(false);
                }

                NvrResult<INvrPlaybackEngine> engineResult =
                    engineProvider.CreatePlaybackEngine();

                if (engineResult == null
                    || !engineResult.Success
                    || engineResult.Data == null)
                {
                    return await RecoverFromGroupCommandFailureCoreAsync(
                        ToPlayerResult(
                            engineResult,
                            "PLAYBACK_ENGINE_CREATE_FAILED"))
                        .ConfigureAwait(false);
                }

                INvrPlaybackEngine engine =
                    engineResult.Data;

                NvrPlaybackGroupRequest groupRequest =
                    ToPlaybackGroupRequest(
                        request,
                        nvrConfig,
                        channels,
                        request.PlayStartTime,
                        NvrPlaybackDirection.Forward,
                        ToNvrPlaybackSpeed(
                            _currentSpeed));

                NvrResult<INvrPlaybackGroupSession> openResult =
                    await engine.OpenAsync(
                        groupRequest,
                        cancellationToken)
                        .ConfigureAwait(false);

                if (openResult == null
                    || !openResult.Success
                    || openResult.Data == null)
                {
                    DisposeEngine(
                        engine);

                    return await RecoverFromGroupCommandFailureCoreAsync(
                        ToPlayerResult(
                            openResult,
                            "PLAYBACK_GROUP_OPEN_FAILED"))
                        .ConfigureAwait(false);
                }

                _playbackEngines[nvrGroup.Key] =
                    engine;

                _playbackGroupSessions[nvrGroup.Key] =
                    openResult.Data;
            }

            List<GroupCommandExecution> startExecutions =
                await ExecuteOnAllGroupsCoreAsync(
                    delegate (
                        PlaybackGroupContext context)
                    {
                        return context.Engine.StartAsync(
                            context.Session,
                            cancellationToken);
                    })
                    .ConfigureAwait(false);

            PlayerPlaybackResult startFailure =
                GetFirstCommandFailure(
                    startExecutions,
                    "PLAYBACK_GROUP_START_FAILED");

            if (startFailure != null)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    startFailure)
                    .ConfigureAwait(false);
            }

            _currentPlaybackTime =
                request.PlayStartTime;

            _playbackClockStartedAtUtc =
                DateTime.UtcNow;

            CurrentState =
                PlaybackState.Playing;

            _currentDirection =
                NvrPlaybackDirection.Forward;

            _pausedFromState =
                PlaybackState.Playing;

            try
            {
                await ExecuteOnAllGroupsCoreAsync(
                    delegate (
                        PlaybackGroupContext context)
                    {
                        return context.Engine.SynchronizeAsync(
                            context.Session,
                            CancellationToken.None);
                    })
                    .ConfigureAwait(false);
            }
            catch
            {
                /*
                 * 초기 그룹 시간 조회 지연은 재생 실패로 확대하지 않는다.
                 */
            }

            return PlayerPlaybackResult.Ok(
                "NVR 그룹 재생을 시작했습니다. "
                + request.PlayStartTime.ToString(
                    "yyyy-MM-dd HH:mm:ss")
                + " ~ "
                + request.PlayEndTime.ToString(
                    "yyyy-MM-dd HH:mm:ss"));
        }

        private async Task<PlayerPlaybackResult> PauseCoreAsync(
            CancellationToken cancellationToken)
        {
            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "일시정지할 재생 그룹이 없습니다.",
                    "PLAYBACK_NOT_STARTED",
                    PlaybackFailureCategory.System);
            }

            if (CurrentState
                == PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Ok(
                    "이미 일시정지 상태입니다.");
            }

            PlaybackState stateBeforePause =
                CurrentState;

            List<GroupCommandExecution> executions =
                await ExecuteOnAllGroupsCoreAsync(
                    delegate (
                        PlaybackGroupContext context)
                    {
                        return context.Engine.PauseAsync(
                            context.Session,
                            cancellationToken);
                    })
                    .ConfigureAwait(false);

            PlayerPlaybackResult failure =
                GetFirstCommandFailure(
                    executions,
                    "PLAYBACK_GROUP_PAUSE_FAILED");

            if (failure != null)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    failure)
                    .ConfigureAwait(false);
            }

            DateTime? actualTime =
                await QueryMasterPlaybackTimeCoreAsync(
                    CancellationToken.None)
                    .ConfigureAwait(false);

            _currentPlaybackTime =
                ClampPlaybackTime(
                    actualTime.HasValue
                        ? actualTime.Value
                        : GetEstimatedPlaybackTime());

            _playbackClockStartedAtUtc =
                null;

            _pausedFromState =
                stateBeforePause
                    == PlaybackState.Rewinding
                    ? PlaybackState.Rewinding
                    : PlaybackState.Playing;

            CurrentState =
                PlaybackState.Paused;

            return PlayerPlaybackResult.Ok(
                "재생 그룹을 일시정지했습니다.");
        }

        private async Task<PlayerPlaybackResult> ResumeCoreAsync(
            CancellationToken cancellationToken)
        {
            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재개할 재생 그룹이 없습니다.",
                    "PLAYBACK_NOT_STARTED",
                    PlaybackFailureCategory.System);
            }

            if (CurrentState
                != PlaybackState.Paused)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 상태에서는 재생을 재개할 수 없습니다.",
                    "PLAYBACK_NOT_PAUSED",
                    PlaybackFailureCategory.System);
            }

            List<GroupCommandExecution> executions =
                await ExecuteOnAllGroupsCoreAsync(
                    delegate (
                        PlaybackGroupContext context)
                    {
                        return context.Engine.ResumeAsync(
                            context.Session,
                            cancellationToken);
                    })
                    .ConfigureAwait(false);

            PlayerPlaybackResult failure =
                GetFirstCommandFailure(
                    executions,
                    "PLAYBACK_GROUP_RESUME_FAILED");

            if (failure != null)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    failure)
                    .ConfigureAwait(false);
            }

            _playbackClockStartedAtUtc =
                DateTime.UtcNow;

            if (_currentDirection
                == NvrPlaybackDirection.Reverse
                || _pausedFromState
                    == PlaybackState.Rewinding)
            {
                CurrentState =
                    PlaybackState.Rewinding;

                _currentDirection =
                    NvrPlaybackDirection.Reverse;

                return PlayerPlaybackResult.Ok(
                    "역재생을 재개했습니다.");
            }

            CurrentState =
                PlaybackState.Playing;

            _currentDirection =
                NvrPlaybackDirection.Forward;

            return PlayerPlaybackResult.Ok(
                "재생을 재개했습니다.");
        }

        private async Task<PlayerPlaybackResult> SeekToTimeCoreAsync(
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "이동할 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_EMPTY",
                    PlaybackFailureCategory.System);
            }

            DateTime normalizedTarget =
                NormalizeSeekTarget(
                    targetTime);

            List<GroupCommandExecution> executions =
                await ExecuteOnAllGroupsCoreAsync(
                    delegate (
                        PlaybackGroupContext context)
                    {
                        return context.Engine.SeekAsync(
                            context.Session,
                            normalizedTarget,
                            cancellationToken);
                    })
                    .ConfigureAwait(false);

            PlayerPlaybackResult failure =
                GetFirstCommandFailure(
                    executions,
                    "PLAYBACK_GROUP_SEEK_FAILED");

            if (failure != null)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    failure)
                    .ConfigureAwait(false);
            }

            _currentPlaybackTime =
                normalizedTarget;

            _playbackClockStartedAtUtc =
                CurrentState == PlaybackState.Playing
                || CurrentState == PlaybackState.Rewinding
                    ? (DateTime?)DateTime.UtcNow
                    : null;

            return PlayerPlaybackResult.Ok(
                "영상시간을 이동했습니다. "
                + normalizedTarget.ToString(
                    "yyyy-MM-dd HH:mm:ss"));
        }

        private async Task<PlayerPlaybackResult> ChangeDirectionCoreAsync(
            NvrPlaybackDirection direction,
            bool startWhenPaused,
            CancellationToken cancellationToken)
        {
            if (_currentRequest == null)
            {
                return PlayerPlaybackResult.Fail(
                    "현재 재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "방향을 변경할 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_EMPTY",
                    PlaybackFailureCategory.System);
            }

            DateTime? actualTime =
                await SyncPlaybackTimeCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);

            DateTime directionChangeTime =
                actualTime.HasValue
                    ? actualTime.Value
                    : GetEstimatedPlaybackTime();

            if (direction
                    == NvrPlaybackDirection.Reverse
                && directionChangeTime
                    <= _currentRequest.PlayStartTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간에서는 역재생을 시작할 수 없습니다.",
                    "REWIND_BEFORE_START",
                    PlaybackFailureCategory.NotSupported);
            }

            bool wasPaused =
                CurrentState
                    == PlaybackState.Paused;

            List<GroupCommandExecution> directionExecutions =
                await ExecuteOnAllGroupsCoreAsync(
                    delegate (
                        PlaybackGroupContext context)
                    {
                        return context.Engine.SetDirectionAsync(
                            context.Session,
                            direction,
                            cancellationToken);
                    })
                    .ConfigureAwait(false);

            PlayerPlaybackResult directionFailure =
                GetFirstCommandFailure(
                    directionExecutions,
                    direction
                        == NvrPlaybackDirection.Reverse
                            ? "REVERSE_PLAYBACK_FAILED"
                            : "FORWARD_PLAYBACK_FAILED");

            if (directionFailure != null)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    directionFailure)
                    .ConfigureAwait(false);
            }

            _currentDirection =
                direction;

            _currentPlaybackTime =
                ClampPlaybackTime(
                    directionChangeTime);

            if (wasPaused
                && startWhenPaused)
            {
                List<GroupCommandExecution> resumeExecutions =
                    await ExecuteOnAllGroupsCoreAsync(
                        delegate (
                            PlaybackGroupContext context)
                        {
                            return context.Engine.ResumeAsync(
                                context.Session,
                                cancellationToken);
                        })
                        .ConfigureAwait(false);

                PlayerPlaybackResult resumeFailure =
                    GetFirstCommandFailure(
                        resumeExecutions,
                        "PLAYBACK_DIRECTION_RESUME_FAILED");

                if (resumeFailure != null)
                {
                    return await RecoverFromGroupCommandFailureCoreAsync(
                        resumeFailure)
                        .ConfigureAwait(false);
                }
            }

            if (wasPaused
                && !startWhenPaused)
            {
                CurrentState =
                    PlaybackState.Paused;

                _playbackClockStartedAtUtc =
                    null;
            }
            else
            {
                CurrentState =
                    direction
                        == NvrPlaybackDirection.Reverse
                            ? PlaybackState.Rewinding
                            : PlaybackState.Playing;

                _playbackClockStartedAtUtc =
                    DateTime.UtcNow;
            }

            _pausedFromState =
                direction
                    == NvrPlaybackDirection.Reverse
                        ? PlaybackState.Rewinding
                        : PlaybackState.Playing;

            return PlayerPlaybackResult.Ok(
                direction
                    == NvrPlaybackDirection.Reverse
                        ? "역재생을 시작했습니다."
                        : "정방향 재생으로 전환했습니다.");
        }

        private async Task<PlayerPlaybackResult> SetPlaybackSpeedCoreAsync(
            PlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(
                typeof(PlaybackSpeed),
                speed))
            {
                return PlayerPlaybackResult.Fail(
                    "지원하지 않는 재생속도입니다.",
                    "PLAYBACK_SPEED_INVALID",
                    PlaybackFailureCategory.NotSupported);
            }

            if (_currentSpeed
                == speed)
            {
                return PlayerPlaybackResult.Ok(
                    "이미 선택한 재생속도입니다.");
            }

            if (_playbackGroupSessions.Count == 0)
            {
                _currentSpeed =
                    speed;

                return PlayerPlaybackResult.Ok(
                    "재생속도를 "
                    + GetPlaybackSpeedText(
                        speed)
                    + "로 선택했습니다.");
            }

            DateTime? actualTime =
                await SyncPlaybackTimeCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);

            DateTime capturedTime =
                actualTime.HasValue
                    ? actualTime.Value
                    : GetEstimatedPlaybackTime();

            PlaybackState stateBeforeSpeedChange =
                CurrentState;

            List<GroupCommandExecution> executions =
                await ExecuteOnAllGroupsCoreAsync(
                    delegate (
                        PlaybackGroupContext context)
                    {
                        return context.Engine.SetSpeedAsync(
                            context.Session,
                            ToNvrPlaybackSpeed(
                                speed),
                            cancellationToken);
                    })
                    .ConfigureAwait(false);

            PlayerPlaybackResult failure =
                GetFirstCommandFailure(
                    executions,
                    "PLAYBACK_GROUP_SPEED_FAILED");

            if (failure != null)
            {
                return await RecoverFromGroupCommandFailureCoreAsync(
                    failure)
                    .ConfigureAwait(false);
            }

            _currentPlaybackTime =
                ClampPlaybackTime(
                    capturedTime);

            _currentSpeed =
                speed;

            CurrentState =
                stateBeforeSpeedChange;

            _playbackClockStartedAtUtc =
                stateBeforeSpeedChange
                    == PlaybackState.Playing
                || stateBeforeSpeedChange
                    == PlaybackState.Rewinding
                    ? (DateTime?)DateTime.UtcNow
                    : null;

            return PlayerPlaybackResult.Ok(
                "재생속도를 "
                + GetPlaybackSpeedText(
                    speed)
                + "로 변경했습니다.");
        }

        private async Task<DateTime?> SyncPlaybackTimeCoreAsync(
            CancellationToken cancellationToken)
        {
            if (_currentRequest == null)
            {
                return null;
            }

            if (_playbackGroupSessions.Count == 0)
            {
                return CurrentPlaybackTime;
            }

            DateTime? masterTime =
                await QueryMasterPlaybackTimeCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);

            if (!masterTime.HasValue)
            {
                return CurrentPlaybackTime;
            }

            _currentPlaybackTime =
                ClampPlaybackTime(
                    masterTime.Value);

            _playbackClockStartedAtUtc =
                CurrentState == PlaybackState.Playing
                || CurrentState == PlaybackState.Rewinding
                    ? (DateTime?)DateTime.UtcNow
                    : null;

            return _currentPlaybackTime;
        }

        private async Task<DateTime?> QueryMasterPlaybackTimeCoreAsync(
            CancellationToken cancellationToken)
        {
            List<GroupStatusSnapshot> snapshots =
                await GetGroupStatusSnapshotsCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);

            List<GroupStatusSnapshot> successful =
                snapshots
                    .Where(
                        snapshot =>
                            snapshot.Result != null
                            && snapshot.Result.Success
                            && snapshot.Result.Data != null
                            && snapshot.Result.Data.CurrentPlaybackTime.HasValue)
                    .OrderBy(
                        snapshot =>
                            snapshot.NvrNo)
                    .ToList();

            if (successful.Count == 0)
            {
                return null;
            }

            int? leftNvrNo =
                _currentRequest == null
                    ? null
                    : _currentRequest.Channels
                        .Where(
                            channel =>
                                channel != null
                                && channel.ScreenPosition
                                    == ScreenPosition.Left)
                        .Select(
                            channel =>
                                (int?)channel.NvrNo)
                        .FirstOrDefault();

            GroupStatusSnapshot master =
                leftNvrNo.HasValue
                    ? successful.FirstOrDefault(
                        snapshot =>
                            snapshot.NvrNo
                                == leftNvrNo.Value)
                    : null;

            if (master == null)
            {
                master =
                    successful[0];
            }

            return master.Result.Data.CurrentPlaybackTime;
        }

        private async Task<PlaybackSyncStatus>
            GetPlaybackSyncStatusCoreAsync(
                CancellationToken cancellationToken)
        {
            var channelStatuses =
                new List<PlaybackChannelTimeStatus>();

            if (_currentRequest == null
                || _currentRequest.Channels == null)
            {
                return PlaybackSyncStatus.FromChannels(
                    channelStatuses);
            }

            List<GroupStatusSnapshot> snapshots =
                await GetGroupStatusSnapshotsCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);

            Dictionary<int, GroupStatusSnapshot> snapshotMap =
                snapshots
                    .GroupBy(
                        snapshot =>
                            snapshot.NvrNo)
                    .ToDictionary(
                        group =>
                            group.Key,
                        group =>
                            group.First());

            foreach (PlayerChannelTarget channel
                in _currentRequest.Channels)
            {
                if (channel == null)
                {
                    continue;
                }

                GroupStatusSnapshot snapshot;

                bool hasSnapshot =
                    snapshotMap.TryGetValue(
                        channel.NvrNo,
                        out snapshot);

                bool hasProviderTime =
                    hasSnapshot
                    && snapshot.Result != null
                    && snapshot.Result.Success
                    && snapshot.Result.Data != null
                    && snapshot.Result.Data.CurrentPlaybackTime.HasValue;

                /*
                 * 같은 NVR의 좌우 채널은 하나의 PlayGroup 공통시간을 사용한다.
                 * 따라서 채널 목록에는 같은 CurrentPlaybackTime이 들어갈 수 있다.
                 *
                 * 실제 좌우 차이는 아래에서 Provider가 측정한
                 * MaximumDriftSeconds로 덮어쓴다.
                 */
                channelStatuses.Add(
                    new PlaybackChannelTimeStatus
                    {
                        ScreenPosition =
                            channel.ScreenPosition.ToString(),

                        NvrNo =
                            channel.NvrNo,

                        ChannelNo =
                            channel.ChannelNo,

                        PlaybackTime =
                            hasProviderTime
                                ? snapshot.Result.Data.CurrentPlaybackTime
                                : null,

                        IsProviderTime =
                            hasProviderTime,

                        TimeOffsetSeconds =
                            channel.TimeOffsetSeconds
                    });
            }

            PlaybackSyncStatus status =
                PlaybackSyncStatus.FromChannels(
                    channelStatuses);

            /*
             * 핵심:
             * 기존에는 동일한 PlayGroup CurrentPlaybackTime을
             * 좌우 채널에 각각 넣었기 때문에 차이가 항상 0초였다.
             *
             * DahuaPlaybackEngine.GetStatusAsync가 각 PlaybackHandle의
             * 실제 OSD 시간을 비교하여 반환한 MaximumDriftSeconds를
             * 화면 표시용 MaxDifference로 사용한다.
             */
            List<double> providerDrifts =
                snapshots
                    .Where(
                        snapshot =>
                            snapshot.Result != null
                            && snapshot.Result.Success
                            && snapshot.Result.Data != null
                            && snapshot.Result.Data.MaximumDriftSeconds.HasValue)
                    .Select(
                        snapshot =>
                            snapshot.Result.Data.MaximumDriftSeconds.Value)
                    .ToList();

            if (providerDrifts.Count > 0)
            {
                status.MaxDifference =
                    TimeSpan.FromSeconds(
                        providerDrifts.Max());

                status.UsesProviderTime =
                    true;
            }
            else if (channelStatuses.Count >= 2)
            {
                /*
                 * 두 채널 이상인데 실제 차이를 측정하지 못한 경우에는
                 * 0초로 위장하지 않고 null을 유지한다.
                 */
                status.MaxDifference =
                    null;
            }

            return status;
        }

        private async Task<PlayerPlaybackResult> ResynchronizeCoreAsync(
            CancellationToken cancellationToken)
        {
            if (_playbackGroupSessions.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "동기화할 재생 그룹이 없습니다.",
                    "PLAYBACK_GROUP_EMPTY",
                    PlaybackFailureCategory.System);
            }

            if (_currentSpeed
                != PlaybackSpeed.Normal)
            {
                return PlayerPlaybackResult.Fail(
                    "영상 동기화는 1배속에서만 실행할 수 있습니다.",
                    "PLAYBACK_SYNC_NORMAL_SPEED_ONLY",
                    PlaybackFailureCategory.NotSupported);
            }

            /*
             * 제조사 엔진의 SynchronizeAsync는 단순 상태 조회가 아니다.
             *
             * Dahua에서는:
             * 1. 그룹 일시정지
             * 2. 각 PlaybackHandle 실제 OSD 시간 측정
             * 3. 기준 채널 시각 결정
             * 4. PlaybackHandle과 PlayGroup 전체 재구성
             * 5. 기존 재생 상태 복원
             * 순서로 하드 동기화를 수행한다.
             */
            List<GroupCommandExecution> syncExecutions =
                await ExecuteOnAllGroupsCoreAsync(
                    delegate (
                        PlaybackGroupContext context)
                    {
                        return context.Engine.SynchronizeAsync(
                            context.Session,
                            cancellationToken);
                    })
                    .ConfigureAwait(false);

            PlayerPlaybackResult syncFailure =
                GetFirstCommandFailure(
                    syncExecutions,
                    "PLAYBACK_GROUP_SYNC_FAILED");

            if (syncFailure != null)
            {
                return syncFailure;
            }

            List<GroupStatusSnapshot> snapshots =
                await GetGroupStatusSnapshotsCoreAsync(
                    cancellationToken)
                    .ConfigureAwait(false);

            List<double> measuredDrifts =
                snapshots
                    .Where(
                        snapshot =>
                            snapshot.Result != null
                            && snapshot.Result.Success
                            && snapshot.Result.Data != null
                            && snapshot.Result.Data.MaximumDriftSeconds.HasValue)
                    .Select(
                        snapshot =>
                            snapshot.Result.Data.MaximumDriftSeconds.Value)
                    .ToList();

            double? maximumMeasuredDrift =
                measuredDrifts.Count > 0
                    ? (double?)measuredDrifts.Max()
                    : null;

            List<DateTime> groupTimes =
                snapshots
                    .Where(
                        snapshot =>
                            snapshot.Result != null
                            && snapshot.Result.Success
                            && snapshot.Result.Data != null
                            && snapshot.Result.Data.CurrentPlaybackTime.HasValue)
                    .Select(
                        snapshot =>
                            snapshot.Result.Data.CurrentPlaybackTime.Value)
                    .ToList();

            if (groupTimes.Count
                < _playbackGroupSessions.Count)
            {
                return PlayerPlaybackResult.Fail(
                    "일부 NVR 그룹의 실제 재생시간을 확인하지 못했습니다.",
                    "PLAYBACK_SYNC_TIME_UNAVAILABLE",
                    PlaybackFailureCategory.Retryable);
            }

            /*
             * 하나의 NVR PlayGroup이면 제조사 엔진 내부의 하드 동기화로
             * 좌우 채널 정렬이 이미 완료됐다.
             */
            if (groupTimes.Count <= 1)
            {
                DateTime singleTime =
                    groupTimes.Count == 1
                        ? groupTimes[0]
                        : GetEstimatedPlaybackTime();

                _currentPlaybackTime =
                    ClampPlaybackTime(
                        singleTime);

                _playbackClockStartedAtUtc =
                    CurrentState == PlaybackState.Playing
                    || CurrentState == PlaybackState.Rewinding
                        ? (DateTime?)DateTime.UtcNow
                        : null;

                return PlayerPlaybackResult.Ok(
                    maximumMeasuredDrift.HasValue
                        ? "영상 동기화를 완료했습니다. "
                            + "현재 좌우 시간차 "
                            + maximumMeasuredDrift.Value.ToString("0.0")
                            + "초"
                        : "영상 동기화를 완료했지만 "
                            + "좌우 실제 시간차를 재측정하지 못했습니다.");
            }

            /*
             * 서로 다른 NVR 그룹이 여러 개면
             * 각 NVR 내부 동기화가 끝난 뒤 그룹 대표시간도 비교한다.
             */
            DateTime minimumTime =
                groupTimes.Min();

            DateTime maximumTime =
                groupTimes.Max();

            double groupDifferenceSeconds =
                Math.Abs(
                    (
                        maximumTime
                        - minimumTime
                    ).TotalSeconds);

            const double allowedDifferenceSeconds =
                1.0;

            if (groupDifferenceSeconds
                <= allowedDifferenceSeconds)
            {
                _currentPlaybackTime =
                    ClampPlaybackTime(
                        _currentDirection
                            == NvrPlaybackDirection.Reverse
                                ? minimumTime
                                : maximumTime);

                _playbackClockStartedAtUtc =
                    CurrentState == PlaybackState.Playing
                    || CurrentState == PlaybackState.Rewinding
                        ? (DateTime?)DateTime.UtcNow
                        : null;

                double displayDifference =
                    maximumMeasuredDrift.HasValue
                        ? Math.Max(
                            maximumMeasuredDrift.Value,
                            groupDifferenceSeconds)
                        : groupDifferenceSeconds;

                return PlayerPlaybackResult.Ok(
                    "영상 동기화 상태가 정상입니다. "
                    + "최대 시간차 "
                    + displayDifference.ToString("0.0")
                    + "초");
            }

            DateTime targetTime =
                _currentDirection
                    == NvrPlaybackDirection.Reverse
                        ? minimumTime
                        : maximumTime;

            PlayerPlaybackResult seekResult =
                await SeekToTimeCoreAsync(
                    targetTime,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (!seekResult.Success)
            {
                return seekResult;
            }

            return PlayerPlaybackResult.Ok(
                "여러 NVR 그룹을 동일한 영상시간으로 동기화했습니다. "
                + targetTime.ToString(
                    "yyyy-MM-dd HH:mm:ss"));
        }

        private async Task<PlayerVideoSourceInfoResult>
            GetVideoSourceInfoCoreAsync(
                PlayerChannelTarget channel,
                CancellationToken cancellationToken)
        {
            if (channel == null
                || channel.NvrConfig == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel == null
                        ? ScreenPosition.Left
                        : channel.ScreenPosition,
                    "NVR 채널 설정이 없습니다.");
            }

            NvrResult<INvrProvider> providerResult =
                await GetOrCreateLoggedInProviderCoreAsync(
                    channel.NvrConfig,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (providerResult == null
                || !providerResult.Success
                || providerResult.Data == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    providerResult == null
                        ? "NVR Provider 연결 결과가 없습니다."
                        : providerResult.Message);
            }

            INvrProvider provider =
                providerResult.Data;

            ProviderCapabilities capabilities =
                provider.GetCapabilities();

            if (capabilities == null
                || !capabilities.CanGetVideoSourceInfo)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    "현재 NVR Provider는 영상 원본 정보 조회를 지원하지 않습니다.");
            }

            INvrVideoSourceInfoProvider sourceInfoProvider =
                provider
                    as INvrVideoSourceInfoProvider;

            if (sourceInfoProvider == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    "현재 NVR Provider는 영상 원본 정보 조회 인터페이스를 구현하지 않았습니다.");
            }

            NvrResult<NvrVideoSourceInfo> result =
                await sourceInfoProvider.GetVideoSourceInfoAsync(
                    channel.ChannelNo,
                    cancellationToken)
                    .ConfigureAwait(false);

            if (result == null
                || !result.Success
                || result.Data == null)
            {
                return PlayerVideoSourceInfoResult.Fail(
                    channel.ScreenPosition,
                    result == null
                        ? "영상 원본 정보 조회 결과가 없습니다."
                        : string.IsNullOrWhiteSpace(
                            result.Message)
                            ? "영상 원본 정보 조회에 실패했습니다."
                            : result.Message);
            }

            return PlayerVideoSourceInfoResult.Ok(
                channel.ScreenPosition,
                result.Data.Width,
                result.Data.Height);
        }

        private async Task<NvrResult<INvrProvider>>
            GetOrCreateLoggedInProviderCoreAsync(
                NvrConfig nvrConfig,
                CancellationToken cancellationToken)
        {
            if (nvrConfig == null)
            {
                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.Failed,
                    "NVR 설정 정보가 없습니다.",
                    CreateError(
                        "NVR_CONFIG_REQUIRED",
                        "NVR 설정 정보가 없습니다.",
                        "GetOrCreateLoggedInProvider"));
            }

            if (string.IsNullOrWhiteSpace(
                nvrConfig.ProviderKey))
            {
                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.ProviderNotFound,
                    "NVR ProviderKey가 없습니다.",
                    CreateError(
                        "NVR_PROVIDER_KEY_REQUIRED",
                        "NVR ProviderKey가 없습니다.",
                        "GetOrCreateLoggedInProvider"));
            }

            INvrProvider existingProvider;

            if (_providers.TryGetValue(
                    nvrConfig.NvrNo,
                    out existingProvider))
            {
                if (existingProvider != null
                    && existingProvider.IsInitialized
                    && existingProvider.IsLoggedIn)
                {
                    return NvrResult<INvrProvider>.Ok(
                        existingProvider,
                        "기존 NVR Provider 연결을 재사용합니다.");
                }

                _providers.Remove(
                    nvrConfig.NvrNo);

                DisposeProvider(
                    existingProvider);
            }

            INvrProvider provider =
                null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                NvrResult<INvrProvider> createResult =
                    _providerFactory.Create(
                        nvrConfig.ProviderKey);

                if (createResult == null
                    || !createResult.Success
                    || createResult.Data == null)
                {
                    return createResult
                        ?? NvrResult<INvrProvider>.Fail(
                            NvrResultStatus.ProviderNotFound,
                            "NVR Provider 생성 결과가 없습니다.",
                            CreateError(
                                "NVR_PROVIDER_CREATE_RESULT_EMPTY",
                                "NVR Provider 생성 결과가 없습니다.",
                                "ProviderFactory.Create"));
                }

                provider =
                    createResult.Data;

                NvrResult initializeResult =
                    provider.Initialize();

                if (initializeResult == null
                    || !initializeResult.Success)
                {
                    NvrResult<INvrProvider> failure =
                        NvrResult<INvrProvider>.Fail(
                            initializeResult == null
                                ? NvrResultStatus.SdkError
                                : initializeResult.Status,
                            initializeResult == null
                                ? "NVR Provider 초기화 결과가 없습니다."
                                : initializeResult.Message,
                            initializeResult == null
                                ? CreateError(
                                    "NVR_PROVIDER_INITIALIZE_RESULT_EMPTY",
                                    "NVR Provider 초기화 결과가 없습니다.",
                                    "Provider.Initialize")
                                : initializeResult.Error);

                    DisposeProvider(
                        provider);

                    return failure;
                }

                NvrConnectionInfo connectionInfo =
                    ToConnectionInfo(
                        nvrConfig);

                NvrResult loginResult =
                    await provider.LoginAsync(
                        connectionInfo,
                        cancellationToken)
                        .ConfigureAwait(false);

                if (loginResult == null
                    || !loginResult.Success)
                {
                    NvrResult<INvrProvider> failure =
                        NvrResult<INvrProvider>.Fail(
                            loginResult == null
                                ? NvrResultStatus.LoginFailed
                                : loginResult.Status,
                            loginResult == null
                                ? "NVR 로그인 결과가 없습니다."
                                : loginResult.Message,
                            loginResult == null
                                ? CreateError(
                                    "NVR_LOGIN_RESULT_EMPTY",
                                    "NVR 로그인 결과가 없습니다.",
                                    "Provider.LoginAsync")
                                : loginResult.Error);

                    DisposeProvider(
                        provider);

                    return failure;
                }

                _providers[nvrConfig.NvrNo] =
                    provider;

                return NvrResult<INvrProvider>.Ok(
                    provider,
                    "NVR Provider 연결에 성공했습니다.");
            }
            catch (OperationCanceledException)
            {
                DisposeProvider(
                    provider);

                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.Cancelled,
                    "NVR 연결 요청이 취소되었습니다.",
                    CreateError(
                        "NVR_CONNECTION_CANCELLED",
                        "NVR 연결 요청이 취소되었습니다.",
                        "GetOrCreateLoggedInProvider"));
            }
            catch (Exception ex)
            {
                DisposeProvider(
                    provider);

                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.UnknownError,
                    "NVR Provider 연결 처리 중 오류가 발생했습니다. "
                    + ex.Message,
                    CreateError(
                        "NVR_PROVIDER_CONNECTION_EXCEPTION",
                        ex.Message,
                        "GetOrCreateLoggedInProvider"));
            }
        }

        private async Task<List<GroupCommandExecution>>
            ExecuteOnAllGroupsCoreAsync(
                Func<PlaybackGroupContext, Task<NvrResult>> command)
        {
            List<PlaybackGroupContext> contexts =
                GetPlaybackGroupContexts();

            Task<GroupCommandExecution>[] tasks =
                contexts
                    .Select(
                        async context =>
                        {
                            try
                            {
                                NvrResult result =
                                    await command(
                                        context)
                                        .ConfigureAwait(false);

                                return new GroupCommandExecution
                                {
                                    NvrNo =
                                        context.NvrNo,

                                    Result =
                                        result
                                        ?? NvrResult.Fail(
                                            NvrResultStatus.Failed,
                                            "NVR 그룹 명령 결과가 없습니다.",
                                            CreateError(
                                                "PLAYBACK_GROUP_COMMAND_RESULT_EMPTY",
                                                "NVR 그룹 명령 결과가 없습니다.",
                                                "ExecuteOnAllGroups"))
                                };
                            }
                            catch (OperationCanceledException)
                            {
                                return new GroupCommandExecution
                                {
                                    NvrNo =
                                        context.NvrNo,

                                    Result =
                                        NvrResult.Fail(
                                            NvrResultStatus.Cancelled,
                                            "NVR 그룹 명령이 취소되었습니다.",
                                            CreateError(
                                                "PLAYBACK_GROUP_COMMAND_CANCELLED",
                                                "NVR 그룹 명령이 취소되었습니다.",
                                                "ExecuteOnAllGroups"))
                                };
                            }
                            catch (Exception ex)
                            {
                                return new GroupCommandExecution
                                {
                                    NvrNo =
                                        context.NvrNo,

                                    Result =
                                        NvrResult.Fail(
                                            NvrResultStatus.UnknownError,
                                            "NVR 그룹 명령 중 오류가 발생했습니다. "
                                            + ex.Message,
                                            CreateError(
                                                "PLAYBACK_GROUP_COMMAND_EXCEPTION",
                                                ex.Message,
                                                "ExecuteOnAllGroups"))
                                };
                            }
                        })
                    .ToArray();

            GroupCommandExecution[] results =
                await Task.WhenAll(
                    tasks)
                    .ConfigureAwait(false);

            return results
                .OrderBy(
                    result =>
                        result.NvrNo)
                .ToList();
        }

        private async Task<List<GroupStatusSnapshot>>
            GetGroupStatusSnapshotsCoreAsync(
                CancellationToken cancellationToken)
        {
            List<PlaybackGroupContext> contexts =
                GetPlaybackGroupContexts();

            Task<GroupStatusSnapshot>[] tasks =
                contexts
                    .Select(
                        async context =>
                        {
                            try
                            {
                                NvrResult<NvrPlaybackGroupStatus> result =
                                    await context.Engine.GetStatusAsync(
                                        context.Session,
                                        cancellationToken)
                                        .ConfigureAwait(false);

                                return new GroupStatusSnapshot
                                {
                                    NvrNo =
                                        context.NvrNo,

                                    Result =
                                        result
                                };
                            }
                            catch (OperationCanceledException)
                            {
                                return new GroupStatusSnapshot
                                {
                                    NvrNo =
                                        context.NvrNo,

                                    Result =
                                        NvrResult<NvrPlaybackGroupStatus>.Fail(
                                            NvrResultStatus.Cancelled,
                                            "NVR 그룹 상태 조회가 취소되었습니다.",
                                            CreateError(
                                                "PLAYBACK_GROUP_STATUS_CANCELLED",
                                                "NVR 그룹 상태 조회가 취소되었습니다.",
                                                "GetStatus"))
                                };
                            }
                            catch (Exception ex)
                            {
                                return new GroupStatusSnapshot
                                {
                                    NvrNo =
                                        context.NvrNo,

                                    Result =
                                        NvrResult<NvrPlaybackGroupStatus>.Fail(
                                            NvrResultStatus.UnknownError,
                                            "NVR 그룹 상태 조회 중 오류가 발생했습니다. "
                                            + ex.Message,
                                            CreateError(
                                                "PLAYBACK_GROUP_STATUS_EXCEPTION",
                                                ex.Message,
                                                "GetStatus"))
                                };
                            }
                        })
                    .ToArray();

            GroupStatusSnapshot[] results =
                await Task.WhenAll(
                    tasks)
                    .ConfigureAwait(false);

            return results
                .OrderBy(
                    result =>
                        result.NvrNo)
                .ToList();
        }

        private List<PlaybackGroupContext>
            GetPlaybackGroupContexts()
        {
            var result =
                new List<PlaybackGroupContext>();

            foreach (KeyValuePair<int, INvrPlaybackGroupSession> item
                in _playbackGroupSessions
                    .OrderBy(
                        item =>
                            item.Key))
            {
                INvrPlaybackEngine engine;

                if (!_playbackEngines.TryGetValue(
                        item.Key,
                        out engine)
                    || engine == null
                    || item.Value == null)
                {
                    continue;
                }

                result.Add(
                    new PlaybackGroupContext
                    {
                        NvrNo =
                            item.Key,

                        Engine =
                            engine,

                        Session =
                            item.Value
                    });
            }

            return result;
        }

        private async Task<PlayerPlaybackResult>
            StopPlaybackGroupsOnlyCoreAsync()
        {
            var warnings =
                new List<string>();

            List<PlaybackGroupContext> contexts =
                GetPlaybackGroupContexts();

            foreach (PlaybackGroupContext context
                in contexts)
            {
                try
                {
                    NvrResult stopResult =
                        await context.Engine.StopAsync(
                            context.Session,
                            CancellationToken.None)
                            .ConfigureAwait(false);

                    if (stopResult == null)
                    {
                        warnings.Add(
                            "NVR "
                            + context.NvrNo
                            + " 그룹 중지 결과가 없습니다.");
                    }
                    else if (!stopResult.Success)
                    {
                        warnings.Add(
                            "NVR "
                            + context.NvrNo
                            + " 그룹 중지 실패: "
                            + stopResult.Message);
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add(
                        "NVR "
                        + context.NvrNo
                        + " 그룹 중지 예외: "
                        + ex.Message);
                }
            }

            /*
             * StopAsync가 네이티브 리소스를 정리한 후
             * 그룹 세션과 엔진의 관리 객체를 해제한다.
             */
            foreach (INvrPlaybackGroupSession session
                in _playbackGroupSessions.Values.ToList())
            {
                DisposeGroupSession(
                    session);
            }

            foreach (INvrPlaybackEngine engine
                in _playbackEngines.Values.ToList())
            {
                DisposeEngine(
                    engine);
            }

            _playbackEngines.Clear();
            _playbackGroupSessions.Clear();

            _currentRequest =
                null;

            _currentPlaybackTime =
                null;

            _playbackClockStartedAtUtc =
                null;

            CurrentState =
                PlaybackState.Stopped;

            _currentDirection =
                NvrPlaybackDirection.Forward;

            _pausedFromState =
                PlaybackState.Playing;

            if (warnings.Count > 0)
            {
                return PlayerPlaybackResult.Fail(
                    "기존 재생 그룹을 완전히 정리하지 못했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        warnings.Select(
                            warning =>
                                "- " + warning)),
                    "PLAYBACK_GROUP_CLEANUP_FAILED",
                    PlaybackFailureCategory.System);
            }

            return PlayerPlaybackResult.Ok(
                "기존 재생 그룹을 정리했습니다.");
        }

        private async Task<PlayerPlaybackResult> StopCoreAsync()
        {
            var warnings =
                new List<string>();

            PlayerPlaybackResult groupCleanupResult =
                await StopPlaybackGroupsOnlyCoreAsync()
                    .ConfigureAwait(false);

            if (groupCleanupResult != null
                && !groupCleanupResult.Success)
            {
                warnings.Add(
                    groupCleanupResult.Message);
            }

            foreach (KeyValuePair<int, INvrProvider> item
                in _providers.ToList())
            {
                INvrProvider provider =
                    item.Value;

                if (provider == null)
                {
                    continue;
                }

                try
                {
                    NvrResult logoutResult =
                        await provider.LogoutAsync(
                            CancellationToken.None)
                            .ConfigureAwait(false);

                    if (logoutResult == null)
                    {
                        warnings.Add(
                            "NVR "
                            + item.Key
                            + " 로그아웃 결과가 없습니다.");
                    }
                    else if (!logoutResult.Success)
                    {
                        warnings.Add(
                            "NVR "
                            + item.Key
                            + " 로그아웃 실패: "
                            + logoutResult.Message);
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add(
                        "NVR "
                        + item.Key
                        + " 로그아웃 예외: "
                        + ex.Message);
                }

                try
                {
                    provider.Dispose();
                }
                catch (Exception ex)
                {
                    warnings.Add(
                        "NVR "
                        + item.Key
                        + " Provider 해제 예외: "
                        + ex.Message);
                }
            }

            _providers.Clear();

            ResetPlaybackState();

            if (warnings.Count > 0)
            {
                return PlayerPlaybackResult.Ok(
                    "재생은 중지되었지만 일부 리소스 정리 경고가 발생했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        warnings.Select(
                            warning =>
                                "- " + warning)));
            }

            return PlayerPlaybackResult.Ok(
                "재생을 중지했습니다.");
        }

        private async Task<PlayerPlaybackResult>
            RecoverFromGroupCommandFailureCoreAsync(
                PlayerPlaybackResult failure)
        {
            PlayerPlaybackResult cleanupResult =
                await StopPlaybackGroupsOnlyCoreAsync()
                    .ConfigureAwait(false);

            if (failure == null)
            {
                failure =
                    PlayerPlaybackResult.Fail(
                        "NVR 그룹 명령에 실패했습니다.",
                        "PLAYBACK_GROUP_COMMAND_FAILED",
                        PlaybackFailureCategory.System);
            }

            if (cleanupResult != null
                && !cleanupResult.Success)
            {
                failure.Message =
                    failure.Message
                    + Environment.NewLine
                    + "재생 그룹 정리 경고: "
                    + cleanupResult.Message;
            }

            return failure;
        }

        private static NvrPlaybackGroupRequest
            ToPlaybackGroupRequest(
                PlayerPlaybackRequest request,
                NvrConfig nvrConfig,
                IList<PlayerChannelTarget> channels,
                DateTime initialTime,
                NvrPlaybackDirection direction,
                NvrPlaybackSpeed speed)
        {
            var target =
                new NvrPlaybackGroupRequest
                {
                    CounterNo =
                        request.CounterNo,

                    NvrNo =
                        nvrConfig.NvrNo,

                    ProviderKey =
                        nvrConfig.ProviderKey,

                    SearchDateTime =
                        request.SearchDateTime,

                    StartTime =
                        request.PlayStartTime,

                    EndTime =
                        request.PlayEndTime,

                    InitialTime =
                        initialTime,

                    InitialDirection =
                        direction,

                    InitialSpeed =
                        speed
                };

            foreach (PlayerChannelTarget channel
                in channels)
            {
                target.Channels.Add(
                    new NvrPlaybackGroupChannelRequest
                    {
                        ChannelNo =
                            channel.ChannelNo,

                        ScreenPosition =
                            (int)channel.ScreenPosition,

                        RenderTargetHandle =
                            channel.OutputHandle,

                        TimeOffsetSeconds =
                            channel.TimeOffsetSeconds
                    });
            }

            return target;
        }

        private static NvrConnectionInfo ToConnectionInfo(
            NvrConfig source)
        {
            NvrConnectionType connectionType;

            if (!Enum.TryParse(
                    source.ConnectionType,
                    true,
                    out connectionType))
            {
                connectionType =
                    NvrConnectionType.Sdk;
            }

            var target =
                new NvrConnectionInfo
                {
                    NvrNo =
                        source.NvrNo,

                    ProviderKey =
                        source.ProviderKey,

                    Vendor =
                        source.Vendor,

                    ConnectionType =
                        connectionType,

                    Host =
                        source.Host,

                    Port =
                        source.Port,

                    UserId =
                        source.UserId,

                    Password =
                        source.Password,

                    ChannelCount =
                        source.ChannelCount
                };

            if (source.ProviderSettings != null)
            {
                foreach (KeyValuePair<string, string> item
                    in source.ProviderSettings)
                {
                    target.ProviderSettings[item.Key] =
                        item.Value;
                }
            }

            return target;
        }

        private static PlayerPlaybackResult ValidatePlaybackRequest(
            PlayerPlaybackRequest request)
        {
            if (request == null)
            {
                return PlayerPlaybackResult.Fail(
                    "재생 요청 정보가 없습니다.",
                    "PLAYBACK_REQUEST_REQUIRED",
                    PlaybackFailureCategory.Configuration);
            }

            if (request.Channels == null
                || request.Channels.Count == 0)
            {
                return PlayerPlaybackResult.Fail(
                    "재생할 채널 정보가 없습니다.",
                    "PLAYBACK_CHANNEL_REQUIRED",
                    PlaybackFailureCategory.Configuration);
            }

            if (request.PlayStartTime
                >= request.PlayEndTime)
            {
                return PlayerPlaybackResult.Fail(
                    "조회 시작시간은 조회 종료시간보다 이전이어야 합니다.",
                    "INVALID_PLAYBACK_RANGE",
                    PlaybackFailureCategory.Configuration);
            }

            var screenPositions =
                new HashSet<int>();

            foreach (PlayerChannelTarget channel
                in request.Channels)
            {
                if (channel == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "재생 채널 정보에 null 항목이 포함되어 있습니다.",
                        "PLAYBACK_CHANNEL_NULL",
                        PlaybackFailureCategory.Configuration);
                }

                if (channel.NvrConfig == null)
                {
                    return PlayerPlaybackResult.Fail(
                        "NVR 채널 설정이 없습니다. "
                        + "NvrNo="
                        + channel.NvrNo
                        + ", ChannelNo="
                        + channel.ChannelNo,
                        "NVR_CONFIG_REQUIRED",
                        PlaybackFailureCategory.Configuration);
                }

                if (channel.OutputHandle
                    == IntPtr.Zero)
                {
                    return PlayerPlaybackResult.Fail(
                        "영상 출력 대상 Handle이 없습니다. "
                        + "ScreenPosition="
                        + channel.ScreenPosition,
                        "PLAYBACK_RENDER_HANDLE_REQUIRED",
                        PlaybackFailureCategory.Configuration);
                }

                if (!screenPositions.Add(
                        (int)channel.ScreenPosition))
                {
                    return PlayerPlaybackResult.Fail(
                        "같은 화면 위치의 채널이 중복되었습니다. "
                        + "ScreenPosition="
                        + channel.ScreenPosition,
                        "PLAYBACK_SCREEN_POSITION_DUPLICATED",
                        PlaybackFailureCategory.Configuration);
                }

                if (channel.NvrConfig.NvrNo
                    != channel.NvrNo)
                {
                    return PlayerPlaybackResult.Fail(
                        "채널의 NVR번호와 NVR 설정 번호가 일치하지 않습니다. "
                        + "ChannelNvrNo="
                        + channel.NvrNo
                        + ", ConfigNvrNo="
                        + channel.NvrConfig.NvrNo,
                        "PLAYBACK_NVR_NUMBER_MISMATCH",
                        PlaybackFailureCategory.Configuration);
                }
            }

            return PlayerPlaybackResult.Ok(
                "재생 요청 검증에 성공했습니다.");
        }

        private DateTime NormalizeSeekTarget(
            DateTime targetTime)
        {
            if (_currentRequest == null)
            {
                return targetTime;
            }

            if (targetTime
                < _currentRequest.PlayStartTime)
            {
                return _currentRequest.PlayStartTime;
            }

            if (targetTime
                >= _currentRequest.PlayEndTime)
            {
                DateTime maximumTarget =
                    _currentRequest.PlayEndTime
                        .AddMilliseconds(
                            -1);

                return maximumTarget
                    < _currentRequest.PlayStartTime
                        ? _currentRequest.PlayStartTime
                        : maximumTarget;
            }

            return targetTime;
        }

        private DateTime ClampPlaybackTime(
            DateTime playbackTime)
        {
            if (_currentRequest == null)
            {
                return playbackTime;
            }

            if (playbackTime
                < _currentRequest.PlayStartTime)
            {
                return _currentRequest.PlayStartTime;
            }

            if (playbackTime
                > _currentRequest.PlayEndTime)
            {
                return _currentRequest.PlayEndTime;
            }

            return playbackTime;
        }

        private DateTime GetEstimatedPlaybackTime()
        {
            if (_currentRequest == null)
            {
                return DateTime.MinValue;
            }

            DateTime baseTime =
                _currentPlaybackTime.HasValue
                    ? _currentPlaybackTime.Value
                    : _currentRequest.PlayStartTime;

            if (!_playbackClockStartedAtUtc.HasValue)
            {
                return ClampPlaybackTime(
                    baseTime);
            }

            double elapsedSeconds =
                (
                    DateTime.UtcNow
                    - _playbackClockStartedAtUtc.Value
                ).TotalSeconds;

            double speedMultiplier =
                GetSpeedMultiplier(
                    _currentSpeed);

            DateTime estimatedTime =
                _currentDirection
                    == NvrPlaybackDirection.Reverse
                        ? baseTime.AddSeconds(
                            -elapsedSeconds
                            * speedMultiplier)
                        : baseTime.AddSeconds(
                            elapsedSeconds
                            * speedMultiplier);

            return ClampPlaybackTime(
                estimatedTime);
        }

        private static PlayerPlaybackResult GetFirstCommandFailure(
            IList<GroupCommandExecution> executions,
            string fallbackErrorCode)
        {
            if (executions == null)
            {
                return PlayerPlaybackResult.Fail(
                    "NVR 그룹 명령 결과가 없습니다.",
                    fallbackErrorCode,
                    PlaybackFailureCategory.System);
            }

            GroupCommandExecution failed =
                executions.FirstOrDefault(
                    execution =>
                        execution == null
                        || execution.Result == null
                        || !execution.Result.Success);

            if (failed == null)
            {
                return null;
            }

            PlayerPlaybackResult result =
                ToPlayerResult(
                    failed.Result,
                    fallbackErrorCode);

            result.Message =
                "NVR "
                + failed.NvrNo
                + ": "
                + result.Message;

            return result;
        }

        private static PlayerPlaybackResult ToPlayerResult(
            NvrResult result,
            string fallbackErrorCode)
        {
            if (result == null)
            {
                return PlayerPlaybackResult.Fail(
                    "NVR 처리 결과가 없습니다.",
                    fallbackErrorCode,
                    PlaybackFailureCategory.System);
            }

            if (result.Success)
            {
                return PlayerPlaybackResult.Ok(
                    string.IsNullOrWhiteSpace(
                        result.Message)
                        ? "NVR 처리가 완료되었습니다."
                        : result.Message);
            }

            string errorCode =
                result.Error == null
                || string.IsNullOrWhiteSpace(
                    result.Error.ErrorCode)
                    ? fallbackErrorCode
                    : result.Error.ErrorCode;

            string message =
                string.IsNullOrWhiteSpace(
                    result.Message)
                    ? result.Error == null
                        || string.IsNullOrWhiteSpace(
                            result.Error.ErrorMessage)
                            ? "NVR 처리에 실패했습니다."
                            : result.Error.ErrorMessage
                    : result.Message;

            return PlayerPlaybackResult.Fail(
                message,
                errorCode,
                ClassifyNvrFailure(
                    result.Status));
        }

        private static PlaybackFailureCategory ClassifyNvrFailure(
            NvrResultStatus status)
        {
            switch (status)
            {
                case NvrResultStatus.ConnectionFailed:
                case NvrResultStatus.ApiError:
                case NvrResultStatus.Failed:
                case NvrResultStatus.PartialSuccess:
                case NvrResultStatus.UnknownError:
                    return PlaybackFailureCategory.Retryable;

                case NvrResultStatus.LoginFailed:
                case NvrResultStatus.InvalidChannel:
                case NvrResultStatus.ProviderNotFound:
                    return PlaybackFailureCategory.Configuration;

                case NvrResultStatus.NoRecordFound:
                    return PlaybackFailureCategory.NoRecord;

                case NvrResultStatus.NotSupported:
                    return PlaybackFailureCategory.NotSupported;

                case NvrResultStatus.Cancelled:
                    return PlaybackFailureCategory.Cancelled;

                case NvrResultStatus.SdkError:
                    return PlaybackFailureCategory.System;

                default:
                    return PlaybackFailureCategory.System;
            }
        }

        private static PlayerPlaybackResult Cancelled(
            string message,
            string errorCode)
        {
            return PlayerPlaybackResult.Fail(
                message,
                errorCode,
                PlaybackFailureCategory.Cancelled);
        }

        private static NvrPlaybackSpeed ToNvrPlaybackSpeed(
            PlaybackSpeed speed)
        {
            switch (speed)
            {
                case PlaybackSpeed.Half:
                    return NvrPlaybackSpeed.Half;

                case PlaybackSpeed.Double:
                    return NvrPlaybackSpeed.Double;

                case PlaybackSpeed.Quad:
                    return NvrPlaybackSpeed.Quad;

                case PlaybackSpeed.Octuple:
                    return NvrPlaybackSpeed.Octuple;

                case PlaybackSpeed.Normal:
                default:
                    return NvrPlaybackSpeed.Normal;
            }
        }

        private static double GetSpeedMultiplier(
            PlaybackSpeed speed)
        {
            switch (speed)
            {
                case PlaybackSpeed.Half:
                    return 0.5;

                case PlaybackSpeed.Double:
                    return 2.0;

                case PlaybackSpeed.Quad:
                    return 4.0;

                case PlaybackSpeed.Octuple:
                    return 8.0;

                case PlaybackSpeed.Normal:
                default:
                    return 1.0;
            }
        }

        private static string GetPlaybackSpeedText(
            PlaybackSpeed speed)
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

                case PlaybackSpeed.Normal:
                default:
                    return "1배속";
            }
        }

        private static void DisposeProvider(
            INvrProvider provider)
        {
            if (provider == null)
            {
                return;
            }

            try
            {
                provider.Dispose();
            }
            catch
            {
            }
        }

        private static void DisposeEngine(
            INvrPlaybackEngine engine)
        {
            IDisposable disposable =
                engine
                    as IDisposable;

            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }

        private static void DisposeGroupSession(
            INvrPlaybackGroupSession session)
        {
            IDisposable disposable =
                session
                    as IDisposable;

            if (disposable == null)
            {
                return;
            }

            try
            {
                disposable.Dispose();
            }
            catch
            {
            }
        }

        private static NvrErrorInfo CreateError(
            string errorCode,
            string errorMessage,
            string operation)
        {
            return new NvrErrorInfo
            {
                ErrorCode =
                    errorCode,

                ErrorMessage =
                    errorMessage,

                Operation =
                    operation
            };
        }

        private void ResetPlaybackState()
        {
            _currentRequest =
                null;

            _currentPlaybackTime =
                null;

            _playbackClockStartedAtUtc =
                null;

            CurrentState =
                PlaybackState.Stopped;

            _currentDirection =
                NvrPlaybackDirection.Forward;

            _pausedFromState =
                PlaybackState.Playing;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    GetType().FullName);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                StopAsync(
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
            }

            _disposed =
                true;

            _commandGate.Dispose();
        }

        private sealed class PlaybackGroupContext
        {
            public int NvrNo { get; set; }

            public INvrPlaybackEngine Engine { get; set; }

            public INvrPlaybackGroupSession Session { get; set; }
        }

        private sealed class GroupCommandExecution
        {
            public int NvrNo { get; set; }

            public NvrResult Result { get; set; }
        }

        private sealed class GroupStatusSnapshot
        {
            public int NvrNo { get; set; }

            public NvrResult<NvrPlaybackGroupStatus> Result
            {
                get;
                set;
            }
        }
    }
}
