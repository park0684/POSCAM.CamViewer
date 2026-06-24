using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Attributes;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Diagnostics;
using CamViewer.Nvr.Dahua.Models;
using CamViewer.Nvr.Dahua.Playback;
using CamViewer.Nvr.Dahua.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Dahua.Providers
{
    /// <summary>
    /// Dahua NetSDK 기반 NVR Provider다.
    ///
    /// 개별 채널 호환 기능은 INvrProvider로 제공하고,
    /// CamViewer의 실제 다중채널 재생은
    /// INvrPlaybackEngineProvider를 통해 DahuaPlaybackEngine이 담당한다.
    /// </summary>
    [NvrProviderExport(
        "DAHUA_SDK",
        "Dahua NetSDK",
        "Dahua",
        NvrConnectionType.Sdk)]
    public sealed class DahuaNvrProvider :
        INvrProvider,
        INvrPlaybackPositionProvider,
        INvrPlaybackAlignmentProvider,
        INvrReversePlaybackProvider,
        INvrVideoSourceInfoProvider,
        INvrPlaybackEngineProvider
    {
        private readonly object _syncRoot =
            new object();

        private readonly HashSet<DahuaPlaybackSession>
            _legacySessions =
                new HashSet<DahuaPlaybackSession>();

        private readonly HashSet<DahuaPlaybackEngine>
            _engines =
                new HashSet<DahuaPlaybackEngine>();

        private bool _disposed;
        private bool _runtimeAcquired;
        private DahuaLoginSession _loginSession;
        private NvrErrorInfo _lastError;

        public DahuaNvrProvider()
        {
            Metadata =
                new ProviderMetadata
                {
                    ProviderKey =
                        "DAHUA_SDK",

                    DisplayName =
                        "Dahua NetSDK",

                    Vendor =
                        "Dahua",

                    ConnectionType =
                        NvrConnectionType.Sdk,

                    Version =
                        "2.0.0",

                    RenderMode =
                        NvrRenderMode.DirectRender,

                    RequiredArchitecture =
                        "x64"
                };
        }

        public ProviderMetadata Metadata
        {
            get;
            private set;
        }

        public bool IsInitialized
        {
            get
            {
                return !_disposed
                    && _runtimeAcquired
                    && DahuaSdkRuntime.IsInitialized;
            }
        }

        public bool IsLoggedIn
        {
            get
            {
                return !_disposed
                    && _loginSession != null
                    && _loginSession.IsValid;
            }
        }

        internal DahuaLoginSession LoginSession
        {
            get
            {
                return _loginSession;
            }
        }

        public ProviderCapabilities GetCapabilities()
        {
            EnsureNotDisposed();

            return new ProviderCapabilities
            {
                RenderMode =
                    NvrRenderMode.DirectRender,

                CanPause =
                    true,

                CanResume =
                    true,

                CanSeek =
                    true,

                CanPlayByRange =
                    true,

                CanSnapshot =
                    false,

                CanTestConnection =
                    true,

                CanQueryRecordExists =
                    true,

                CanGetPlaybackPosition =
                    true,

                CanChangeSpeed =
                    true,

                /*
                 * 새 구현에서는 영상 재생 안정화를 우선한다.
                 * 인코딩 설정 조회는 별도 단계에서 추가한다.
                 */
                CanGetVideoSourceInfo =
                    false,

                CanReversePlayback =
                    true
            };
        }

        public NvrResult Initialize()
        {
            EnsureNotDisposed();

            if (_runtimeAcquired)
            {
                return NvrResult.Ok(
                    "Dahua Provider가 이미 초기화되어 있습니다.");
            }

            NvrResult result =
                DahuaSdkRuntime.Acquire();

            if (!result.Success)
            {
                SetLastError(
                    result.Error);

                return result;
            }

            _runtimeAcquired =
                true;

            SetLastError(
                null);

            return result;
        }

        public Task<NvrResult> LoginAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    Cancelled(
                        "Login"));
            }

            if (!IsInitialized)
            {
                return Task.FromResult(
                    Fail(
                        NvrResultStatus.SdkError,
                        "Dahua Provider가 초기화되지 않았습니다.",
                        "DAHUA_PROVIDER_NOT_INITIALIZED",
                        "Login"));
            }

            /*
             * 기존 로그인에 연결된 재생 리소스를 먼저 정리한다.
             */
            NvrResult releaseResult =
                ReleasePlaybackResources();

            if (!releaseResult.Success)
            {
                return Task.FromResult(
                    releaseResult);
            }

            if (_loginSession != null)
            {
                NvrResult oldLogoutResult =
                    DahuaDeviceClient.Logout(
                        _loginSession);

                _loginSession =
                    null;

                if (!oldLogoutResult.Success)
                {
                    SetLastError(
                        oldLogoutResult.Error);

                    return Task.FromResult(
                        oldLogoutResult);
                }
            }

            NvrResult<DahuaLoginSession> loginResult =
                DahuaDeviceClient.Login(
                    connectionInfo);

            if (!loginResult.Success
                || loginResult.Data == null)
            {
                SetLastError(
                    loginResult.Error);

                return Task.FromResult(
                    NvrResult.Fail(
                        loginResult.Status,
                        loginResult.Message,
                        loginResult.Error));
            }

            _loginSession =
                loginResult.Data;

            SetLastError(
                null);

            DahuaLogWriter.Write(
                "INFO",
                "Provider.Login",
                "NvrNo="
                + connectionInfo.NvrNo
                + ", Host="
                + connectionInfo.Host
                + ", Port="
                + connectionInfo.Port);

            return Task.FromResult(
                NvrResult.Ok(
                    loginResult.Message));
        }

        public Task<NvrResult<INvrPlaybackSession>> PlayByTimeAsync(
            NvrPlaybackRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 요청이 취소되었습니다.",
                        Error(
                            "DAHUA_PLAYBACK_CANCELLED",
                            "Dahua 재생 요청이 취소되었습니다.",
                            "PlayByTime")));
            }

            NvrResult<DahuaPlaybackSession> openResult =
                OpenPlaybackSession(
                    request,
                    NvrPlaybackDirection.Forward);

            if (!openResult.Success
                || openResult.Data == null)
            {
                SetLastError(
                    openResult.Error);

                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        openResult.Status,
                        openResult.Message,
                        openResult.Error));
            }

            DahuaPlaybackSession session =
                openResult.Data;

            if (!request.AutoPlay)
            {
                NvrResult pauseResult =
                    DahuaPlaybackClient.Pause(
                        session);

                if (!pauseResult.Success)
                {
                    DahuaPlaybackClient.Stop(
                        session);

                    SetLastError(
                        pauseResult.Error);

                    return Task.FromResult(
                        NvrResult<INvrPlaybackSession>.Fail(
                            pauseResult.Status,
                            pauseResult.Message,
                            pauseResult.Error));
                }
            }

            lock (_syncRoot)
            {
                _legacySessions.Add(
                    session);
            }

            SetLastError(
                null);

            return Task.FromResult(
                NvrResult<INvrPlaybackSession>.Ok(
                    session,
                    "Dahua 녹화영상 재생 세션을 생성했습니다."));
        }

        public Task<NvrResult<INvrPlaybackSession>> PlayReverseByTimeAsync(
            NvrPlaybackRequest request,
            DateTime reverseStartTime,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 역재생 요청이 취소되었습니다.",
                        Error(
                            "DAHUA_REVERSE_CANCELLED",
                            "Dahua 역재생 요청이 취소되었습니다.",
                            "PlayReverseByTime")));
            }

            if (request == null)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 역재생 요청 정보가 없습니다.",
                        Error(
                            "DAHUA_REVERSE_REQUEST_REQUIRED",
                            "Dahua 역재생 요청 정보가 없습니다.",
                            "PlayReverseByTime")));
            }

            if (reverseStartTime <= request.StartTime)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.Failed,
                        "역재생 시작시간은 조회 시작시간보다 이후여야 합니다.",
                        Error(
                            "DAHUA_REVERSE_TIME_INVALID",
                            "역재생 시작시간은 조회 시작시간보다 이후여야 합니다.",
                            "PlayReverseByTime")));
            }

            DateTime reverseEnd =
                reverseStartTime > request.EndTime
                    ? request.EndTime
                    : reverseStartTime;

            var reverseRequest =
                new NvrPlaybackRequest
                {
                    CounterNo =
                        request.CounterNo,

                    NvrNo =
                        request.NvrNo,

                    ChannelNo =
                        request.ChannelNo,

                    ScreenPosition =
                        request.ScreenPosition,

                    SearchDateTime =
                        request.SearchDateTime,

                    StartTime =
                        request.StartTime,

                    EndTime =
                        reverseEnd,

                    RenderTargetHandle =
                        request.RenderTargetHandle,

                    AutoPlay =
                        request.AutoPlay
                };

            NvrResult<DahuaPlaybackSession> openResult =
                OpenPlaybackSession(
                    reverseRequest,
                    NvrPlaybackDirection.Reverse);

            if (!openResult.Success
                || openResult.Data == null)
            {
                SetLastError(
                    openResult.Error);

                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        openResult.Status,
                        openResult.Message,
                        openResult.Error));
            }

            DahuaPlaybackSession session =
                openResult.Data;

            session.SetCurrentPlaybackTime(
                reverseEnd);

            if (!request.AutoPlay)
            {
                NvrResult pauseResult =
                    DahuaPlaybackClient.Pause(
                        session);

                if (!pauseResult.Success)
                {
                    DahuaPlaybackClient.Stop(
                        session);

                    return Task.FromResult(
                        NvrResult<INvrPlaybackSession>.Fail(
                            pauseResult.Status,
                            pauseResult.Message,
                            pauseResult.Error));
                }
            }

            lock (_syncRoot)
            {
                _legacySessions.Add(
                    session);
            }

            return Task.FromResult(
                NvrResult<INvrPlaybackSession>.Ok(
                    session,
                    "Dahua 역재생 세션을 생성했습니다."));
        }

        public Task<NvrResult> StopAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            DahuaPlaybackSession dahuaSession =
                session
                    as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    InvalidSession(
                        "Stop"));
            }

            NvrResult result =
                DahuaPlaybackClient.Stop(
                    dahuaSession);

            lock (_syncRoot)
            {
                _legacySessions.Remove(
                    dahuaSession);
            }

            SetLastError(
                result.Success
                    ? null
                    : result.Error);

            return Task.FromResult(
                result);
        }

        public Task<NvrResult> PauseAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    Cancelled(
                        "Pause"));
            }

            DahuaPlaybackSession dahuaSession =
                session
                    as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    InvalidSession(
                        "Pause"));
            }

            NvrResult result =
                DahuaPlaybackClient.Pause(
                    dahuaSession);

            SetLastError(
                result.Success
                    ? null
                    : result.Error);

            return Task.FromResult(
                result);
        }

        public Task<NvrResult> ResumeAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    Cancelled(
                        "Resume"));
            }

            DahuaPlaybackSession dahuaSession =
                session
                    as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    InvalidSession(
                        "Resume"));
            }

            NvrResult result =
                DahuaPlaybackClient.Resume(
                    dahuaSession);

            SetLastError(
                result.Success
                    ? null
                    : result.Error);

            return Task.FromResult(
                result);
        }

        /// <summary>
        /// 개별 호환 세션의 Seek도 기존 핸들 직접 이동이 아니라
        /// 목표 시각에서 새 핸들을 생성하여 교체한다.
        /// </summary>
        public Task<NvrResult> SeekAsync(
            INvrPlaybackSession session,
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    Cancelled(
                        "Seek"));
            }

            DahuaPlaybackSession dahuaSession =
                session
                    as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    InvalidSession(
                        "Seek"));
            }

            NvrResult result =
                RebuildLegacySession(
                    dahuaSession,
                    targetTime,
                    dahuaSession.Direction,
                    dahuaSession.Speed,
                    dahuaSession.State
                        == NvrPlaybackState.Paused);

            SetLastError(
                result.Success
                    ? null
                    : result.Error);

            return Task.FromResult(
                result);
        }

        public Task<NvrResult<INvrPlaybackSession>> AlignPlaybackAsync(
            INvrPlaybackSession session,
            NvrPlaybackAlignmentRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 정렬 요청이 취소되었습니다.",
                        Error(
                            "DAHUA_ALIGNMENT_CANCELLED",
                            "Dahua 재생 정렬 요청이 취소되었습니다.",
                            "AlignPlayback")));
            }

            DahuaPlaybackSession dahuaSession =
                session
                    as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                NvrResult invalid =
                    InvalidSession(
                        "AlignPlayback");

                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        invalid.Status,
                        invalid.Message,
                        invalid.Error));
            }

            if (request == null)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 정렬 요청 정보가 없습니다.",
                        Error(
                            "DAHUA_ALIGNMENT_REQUEST_REQUIRED",
                            "Dahua 재생 정렬 요청 정보가 없습니다.",
                            "AlignPlayback")));
            }

            NvrResult result =
                RebuildLegacySession(
                    dahuaSession,
                    request.TargetTime,
                    request.Direction,
                    request.Speed,
                    request.RemainPaused);

            if (!result.Success)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        result.Status,
                        result.Message,
                        result.Error));
            }

            return Task.FromResult(
                NvrResult<INvrPlaybackSession>.Ok(
                    dahuaSession,
                    "Dahua 재생 세션을 요청한 위치와 상태로 정렬했습니다."));
        }

        public Task<NvrResult<DateTime>> GetPlaybackTimeAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<DateTime>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생시간 조회가 취소되었습니다.",
                        Error(
                            "DAHUA_PLAYBACK_TIME_CANCELLED",
                            "Dahua 재생시간 조회가 취소되었습니다.",
                            "GetPlaybackTime")));
            }

            DahuaPlaybackSession dahuaSession =
                session
                    as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                NvrResult invalid =
                    InvalidSession(
                        "GetPlaybackTime");

                return Task.FromResult(
                    NvrResult<DateTime>.Fail(
                        invalid.Status,
                        invalid.Message,
                        invalid.Error));
            }

            NvrResult<DateTime> result =
                DahuaPlaybackClient.QueryTime(
                    dahuaSession);

            SetLastError(
                result.Success
                    ? null
                    : result.Error);

            return Task.FromResult(
                result);
        }

        public Task<NvrResult> SetPlaybackSpeedAsync(
            INvrPlaybackSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    Cancelled(
                        "SetPlaybackSpeed"));
            }

            DahuaPlaybackSession dahuaSession =
                session
                    as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    InvalidSession(
                        "SetPlaybackSpeed"));
            }

            NvrResult result =
                DahuaPlaybackClient.SetSpeed(
                    dahuaSession,
                    speed);

            SetLastError(
                result.Success
                    ? null
                    : result.Error);

            return Task.FromResult(
                result);
        }

        public Task<NvrResult> TestConnectionAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    Cancelled(
                        "TestConnection"));
            }

            if (!IsInitialized)
            {
                return Task.FromResult(
                    Fail(
                        NvrResultStatus.SdkError,
                        "Dahua Provider가 초기화되지 않았습니다.",
                        "DAHUA_PROVIDER_NOT_INITIALIZED",
                        "TestConnection"));
            }

            NvrResult result =
                DahuaDeviceClient.TestConnection(
                    connectionInfo);

            SetLastError(
                result.Success
                    ? null
                    : result.Error);

            return Task.FromResult(
                result);
        }

        public Task<NvrResult<bool>> QueryRecordExistsAsync(
            NvrRecordQueryRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<bool>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 녹화 조회가 취소되었습니다.",
                        Error(
                            "DAHUA_RECORD_QUERY_CANCELLED",
                            "Dahua 녹화 조회가 취소되었습니다.",
                            "QueryRecordExists")));
            }

            if (!IsLoggedIn)
            {
                return Task.FromResult(
                    NvrResult<bool>.Fail(
                        NvrResultStatus.LoginFailed,
                        "Dahua NVR에 로그인되어 있지 않습니다.",
                        Error(
                            "DAHUA_NOT_LOGGED_IN",
                            "Dahua NVR에 로그인되어 있지 않습니다.",
                            "QueryRecordExists")));
            }

            NvrResult<bool> result =
                DahuaDeviceClient.QueryRecordExists(
                    _loginSession,
                    request);

            SetLastError(
                result.Success
                    ? null
                    : result.Error);

            return Task.FromResult(
                result);
        }

        public Task<NvrResult<NvrVideoSourceInfo>> GetVideoSourceInfoAsync(
            int channelNo,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                NvrResult<NvrVideoSourceInfo>.Fail(
                    NvrResultStatus.NotSupported,
                    "새 Dahua Provider의 1차 구현에서는 "
                    + "영상 원본 해상도 조회를 지원하지 않습니다.",
                    Error(
                        "DAHUA_VIDEO_SOURCE_INFO_NOT_SUPPORTED",
                        "영상 원본 해상도 조회는 재생 안정화 후 추가합니다.",
                        "GetVideoSourceInfo")));
        }

        public NvrResult<INvrPlaybackEngine> CreatePlaybackEngine()
        {
            EnsureNotDisposed();

            if (!IsInitialized)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.SdkError,
                    "Dahua Provider가 초기화되지 않았습니다.",
                    Error(
                        "DAHUA_PROVIDER_NOT_INITIALIZED",
                        "Dahua Provider가 초기화되지 않았습니다.",
                        "CreatePlaybackEngine"));
            }

            if (!IsLoggedIn)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR에 로그인되어 있지 않습니다.",
                    Error(
                        "DAHUA_NOT_LOGGED_IN",
                        "Dahua NVR에 로그인되어 있지 않습니다.",
                        "CreatePlaybackEngine"));
            }

            var engine =
                new DahuaPlaybackEngine(
                    this);

            lock (_syncRoot)
            {
                _engines.Add(
                    engine);
            }

            return NvrResult<INvrPlaybackEngine>.Ok(
                engine,
                "Dahua PlayGroup 재생 엔진을 생성했습니다.");
        }

        public Task<NvrResult> LogoutAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            NvrResult resourceResult =
                ReleasePlaybackResources();

            NvrResult logoutResult =
                DahuaDeviceClient.Logout(
                    _loginSession);

            _loginSession =
                null;

            if (!resourceResult.Success)
            {
                SetLastError(
                    resourceResult.Error);

                return Task.FromResult(
                    resourceResult);
            }

            SetLastError(
                logoutResult.Success
                    ? null
                    : logoutResult.Error);

            return Task.FromResult(
                logoutResult);
        }

        public NvrErrorInfo GetLastError()
        {
            return _lastError;
        }

        internal NvrResult<DahuaPlaybackSession> OpenPlaybackSession(
            NvrPlaybackRequest request,
            NvrPlaybackDirection direction)
        {
            if (!IsInitialized)
            {
                return NvrResult<DahuaPlaybackSession>.Fail(
                    NvrResultStatus.SdkError,
                    "Dahua Provider가 초기화되지 않았습니다.",
                    Error(
                        "DAHUA_PROVIDER_NOT_INITIALIZED",
                        "Dahua Provider가 초기화되지 않았습니다.",
                        "OpenPlaybackSession"));
            }

            if (!IsLoggedIn)
            {
                return NvrResult<DahuaPlaybackSession>.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR에 로그인되어 있지 않습니다.",
                    Error(
                        "DAHUA_NOT_LOGGED_IN",
                        "Dahua NVR에 로그인되어 있지 않습니다.",
                        "OpenPlaybackSession"));
            }

            return DahuaPlaybackClient.Open(
                _loginSession,
                request,
                direction);
        }

        internal void NotifyEngineDisposed(
            DahuaPlaybackEngine engine)
        {
            if (engine == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                _engines.Remove(
                    engine);
            }
        }

        private NvrResult RebuildLegacySession(
            DahuaPlaybackSession session,
            DateTime targetTime,
            NvrPlaybackDirection direction,
            NvrPlaybackSpeed speed,
            bool remainPaused)
        {
            if (targetTime < session.StartTime
                || targetTime >= session.EndTime)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "이동할 영상시간이 재생 범위를 벗어났습니다.",
                    "DAHUA_SEEK_OUT_OF_RANGE",
                    "RebuildLegacySession");
            }

            DateTime nativeStart =
                direction == NvrPlaybackDirection.Reverse
                    ? session.StartTime
                    : targetTime;

            DateTime nativeEnd =
                direction == NvrPlaybackDirection.Reverse
                    ? targetTime
                    : session.EndTime;

            if (nativeStart >= nativeEnd)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "재구성할 Dahua 재생 구간이 올바르지 않습니다.",
                    "DAHUA_REBUILD_RANGE_INVALID",
                    "RebuildLegacySession");
            }

            var request =
                new NvrPlaybackRequest
                {
                    CounterNo =
                        session.CounterNo,

                    NvrNo =
                        session.NvrNo,

                    ChannelNo =
                        session.ChannelNo,

                    ScreenPosition =
                        session.ScreenPosition,

                    SearchDateTime =
                        session.SearchDateTime,

                    StartTime =
                        nativeStart,

                    EndTime =
                        nativeEnd,

                    RenderTargetHandle =
                        session.RenderTargetHandle,

                    AutoPlay =
                        true
                };

            NvrResult<DahuaPlaybackSession> openResult =
                OpenPlaybackSession(
                    request,
                    direction);

            if (!openResult.Success
                || openResult.Data == null)
            {
                return NvrResult.Fail(
                    openResult.Status,
                    openResult.Message,
                    openResult.Error);
            }

            DahuaPlaybackSession replacement =
                openResult.Data;

            NvrResult speedResult =
                DahuaPlaybackClient.SetSpeed(
                    replacement,
                    speed);

            if (!speedResult.Success)
            {
                DahuaPlaybackClient.Stop(
                    replacement);

                return speedResult;
            }

            if (remainPaused)
            {
                NvrResult pauseResult =
                    DahuaPlaybackClient.Pause(
                        replacement);

                if (!pauseResult.Success)
                {
                    DahuaPlaybackClient.Stop(
                        replacement);

                    return pauseResult;
                }
            }

            IntPtr replacementHandle =
                replacement.TakePlaybackHandle();

            DahuaPlaybackClient.Stop(
                session);

            session.AdoptPlaybackHandle(
                replacementHandle,
                nativeStart,
                nativeEnd);

            session.SetDirection(
                direction);

            session.SetSpeed(
                speed);

            session.SetCurrentPlaybackTime(
                targetTime);

            session.SetState(
                remainPaused
                    ? NvrPlaybackState.Paused
                    : direction == NvrPlaybackDirection.Reverse
                        ? NvrPlaybackState.Rewinding
                        : NvrPlaybackState.Playing);

            replacement.Dispose();

            return NvrResult.Ok(
                "Dahua 재생 세션을 목표 시각에서 재구성했습니다.");
        }

        private NvrResult ReleasePlaybackResources()
        {
            var failures =
                new List<string>();

            List<DahuaPlaybackEngine> engines;

            lock (_syncRoot)
            {
                engines =
                    _engines.ToList();
            }

            foreach (DahuaPlaybackEngine engine
                in engines)
            {
                try
                {
                    engine.Dispose();
                }
                catch (Exception ex)
                {
                    failures.Add(
                        "재생 엔진 정리 실패: "
                        + ex.Message);
                }
            }

            List<DahuaPlaybackSession> sessions;

            lock (_syncRoot)
            {
                sessions =
                    _legacySessions.ToList();

                _legacySessions.Clear();
                _engines.Clear();
            }

            foreach (DahuaPlaybackSession session
                in sessions)
            {
                NvrResult stopResult =
                    DahuaPlaybackClient.Stop(
                        session);

                if (!stopResult.Success)
                {
                    failures.Add(
                        stopResult.Message);
                }

                session.Dispose();
            }

            if (failures.Count > 0)
            {
                return Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 리소스를 완전히 정리하지 못했습니다."
                    + Environment.NewLine
                    + string.Join(
                        Environment.NewLine,
                        failures),
                    "DAHUA_PLAYBACK_RESOURCE_CLEANUP_FAILED",
                    "ReleasePlaybackResources");
            }

            return NvrResult.Ok(
                "Dahua 재생 리소스를 정리했습니다.");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            /*
             * EnsureNotDisposed를 사용하는 공개 LogoutAsync 대신
             * 내부 정리 메서드를 직접 호출한다.
             */
            try
            {
                ReleasePlaybackResources();
            }
            catch
            {
            }

            try
            {
                DahuaDeviceClient.Logout(
                    _loginSession);
            }
            catch
            {
            }

            _loginSession =
                null;

            if (_runtimeAcquired)
            {
                try
                {
                    DahuaSdkRuntime.Release();
                }
                catch
                {
                }

                _runtimeAcquired =
                    false;
            }

            _disposed =
                true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    GetType().FullName);
            }
        }

        private void SetLastError(
            NvrErrorInfo error)
        {
            _lastError =
                error;

            if (error != null)
            {
                DahuaLogWriter.Write(
                    "ERROR",
                    error.Operation,
                    error.ErrorCode
                    + ": "
                    + error.ErrorMessage
                    + ", Native="
                    + error.NativeErrorCode);
            }
        }

        private static NvrResult Cancelled(
            string operation)
        {
            return NvrResult.Fail(
                NvrResultStatus.Cancelled,
                "Dahua 요청이 취소되었습니다.",
                Error(
                    "DAHUA_OPERATION_CANCELLED",
                    "Dahua 요청이 취소되었습니다.",
                    operation));
        }

        private static NvrResult InvalidSession(
            string operation)
        {
            return Fail(
                NvrResultStatus.Failed,
                "Dahua 재생 세션 형식이 올바르지 않습니다.",
                "INVALID_DAHUA_PLAYBACK_SESSION",
                operation);
        }

        private static NvrResult Fail(
            NvrResultStatus status,
            string message,
            string errorCode,
            string operation)
        {
            return NvrResult.Fail(
                status,
                message,
                Error(
                    errorCode,
                    message,
                    operation));
        }

        private static NvrErrorInfo Error(
            string errorCode,
            string message,
            string operation)
        {
            return new NvrErrorInfo
            {
                ErrorCode =
                    errorCode,

                ErrorMessage =
                    message,

                NativeErrorCode =
                    DahuaSdkRuntime.GetLastErrorSafe(),

                Operation =
                    operation
            };
        }
    }
}
