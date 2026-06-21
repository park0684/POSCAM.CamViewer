using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Attributes;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Sdk;
using NetSDKCS;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CamViewer.Nvr.Dahua.Playback;

namespace CamViewer.Nvr.Dahua.Providers
{
    /// <summary>
    /// Dahua NetSDK 기반 NVR Provider.
    ///
    /// 현재 단계에서는 Provider DLL 검색, 등록, 생성 테스트를 위한 임시 구현체이다.
    /// 실제 Dahua SDK 초기화, 로그인, 녹화 재생 기능은 다음 단계에서 구현한다.
    /// </summary>
    [NvrProviderExport(
        "DAHUA_SDK",
        "Dahua NetSDK",
        "Dahua",
        NvrConnectionType.Sdk)]
    public sealed class DahuaNvrProvider :
    INvrProvider,
    INvrPlaybackPositionProvider,
    INvrVideoSourceInfoProvider,
    INvrReversePlaybackProvider,
    INvrPlaybackAlignmentProvider,
    INvrPlaybackEngineProvider
    {
        private bool _disposed;
        private bool _runtimeAcquired;
        private DahuaLoginSession _loginSession;
        private NvrErrorInfo _lastError;
        
        /// <summary>
        /// Dahua Provider를 초기화한다.
        /// ProviderAssemblyLoader에서 생성할 수 있도록 public 기본 생성자를 유지한다.
        /// </summary>
        public DahuaNvrProvider()
        {
            Metadata = new ProviderMetadata
            {
                ProviderKey = "DAHUA_SDK",
                DisplayName = "Dahua NetSDK",
                Vendor = "Dahua",
                ConnectionType = NvrConnectionType.Sdk,
                Version = "1.0.0",
                RenderMode = NvrRenderMode.DirectRender,
                RequiredArchitecture = "x64"
            };
        }

        /// <summary>
        /// Provider 식별 정보.
        /// </summary>
        public ProviderMetadata Metadata { get; private set; }

        /// <summary>
        /// Provider 초기화 여부.
        /// </summary>
        public bool IsInitialized
        {
            get { return _runtimeAcquired && DahuaSdkRuntime.IsInitialized; }
        }

        /// <summary>
        /// NVR 로그인 여부.
        /// </summary>
        public bool IsLoggedIn
        {
            get { return _loginSession != null && _loginSession.IsValid; }
        }

        /// <summary>
        /// Dahua Provider가 지원하는 기능 목록을 반환한다.
        /// </summary>
        public ProviderCapabilities GetCapabilities()
        {
            EnsureNotDisposed();

            return new ProviderCapabilities
            {
                RenderMode = NvrRenderMode.DirectRender,

                CanPause = true,
                CanResume = true,
                CanSeek = true,

                CanPlayByRange = false,
                CanSnapshot = false,
                CanTestConnection = true,
                CanQueryRecordExists = false,
                CanGetPlaybackPosition = true,
                CanReversePlayback = true,
                CanChangeSpeed = true,
                CanGetVideoSourceInfo = true
            };
        }

        /// <summary>
        /// 현재 로그인된 Dahua Provider에 연결된
        /// 고수준 다중채널 재생 엔진을 생성한다.
        ///
        /// CamViewer 공통 서비스는 이 엔진에 명령만 전달하고,
        /// Dahua SDK 호출 순서와 동기화 방법에는 관여하지 않는다.
        /// </summary>
        public NvrResult<INvrPlaybackEngine>
            CreatePlaybackEngine()
        {
            EnsureNotDisposed();

            if (!IsInitialized)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.SdkError,
                    "Dahua Provider가 초기화되지 않았습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_ENGINE_PROVIDER_NOT_INITIALIZED",

                        ErrorMessage =
                            "Dahua Provider가 초기화되지 않았습니다.",

                        Operation =
                            "CreatePlaybackEngine"
                    });
            }

            if (!IsLoggedIn)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.LoginFailed,
                    "Dahua NVR에 로그인되어 있지 않습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_ENGINE_PROVIDER_NOT_LOGGED_IN",

                        ErrorMessage =
                            "Dahua NVR에 로그인되어 있지 않습니다.",

                        Operation =
                            "CreatePlaybackEngine"
                    });
            }

            try
            {
                INvrPlaybackEngine engine =
                    new DahuaPlaybackEngine(
                        this);

                return NvrResult<INvrPlaybackEngine>.Ok(
                    engine,
                    "Dahua 다중채널 재생 엔진을 생성했습니다.");
            }
            catch (Exception ex)
            {
                return NvrResult<INvrPlaybackEngine>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 다중채널 재생 엔진 생성 중 오류가 발생했습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_PLAYBACK_ENGINE_CREATE_FAILED",

                        ErrorMessage =
                            ex.Message,

                        Operation =
                            "CreatePlaybackEngine"
                    });
            }
        }

        /// <summary>
        /// Dahua SDK를 초기화한다.
        /// </summary>
        public NvrResult Initialize()
        {
            EnsureNotDisposed();

            if (_runtimeAcquired)
            {
                return NvrResult.Ok("Dahua Provider가 이미 초기화되어 있습니다.");
            }

            NvrResult result = DahuaSdkRuntime.Acquire();

            if (!result.Success)
            {
                _lastError = result.Error;
                return result;
            }

            _runtimeAcquired = true;
            _lastError = null;

            return result;
        }

        /// <summary>
        /// Dahua NVR에 로그인한다.
        /// </summary>
        public Task<NvrResult> LoginAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult.Fail(
                        NvrResultStatus.Cancelled,
                        "로그인 요청이 취소되었습니다."));
            }

            if (!IsInitialized)
            {
                return Task.FromResult(
                    NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua Provider가 초기화되지 않았습니다."));
            }

            if (_loginSession != null)
            {
                _loginSession.Dispose();
                _loginSession = null;
            }

            NvrResult<DahuaLoginSession> loginResult =
                DahuaSdkClient.Login(connectionInfo);

            if (!loginResult.Success)
            {
                _lastError = loginResult.Error;

                return Task.FromResult(
                    NvrResult.Fail(
                        loginResult.Status,
                        loginResult.Message,
                        loginResult.Error));
            }

            _loginSession = loginResult.Data;
            _lastError = null;

            return Task.FromResult(
                NvrResult.Ok(loginResult.Message));
        }

        /// <summary>
        /// 지정된 시간 구간의 Dahua 녹화 영상을 재생한다.
        /// </summary>
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
                        "재생 요청이 취소되었습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode = "CANCELLED",
                            ErrorMessage = "재생 요청이 취소되었습니다.",
                            Operation = "PlayByTime"
                        }));
            }

            if (!IsInitialized)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua Provider가 초기화되지 않았습니다."));
            }

            if (!IsLoggedIn)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.LoginFailed,
                        "Dahua NVR에 로그인되어 있지 않습니다."));
            }

            NvrResult<DahuaPlaybackSession> playResult =
                DahuaSdkClient.PlayByTime(
                    _loginSession,
                    request);

            if (!playResult.Success || playResult.Data == null)
            {
                _lastError = playResult.Error;

                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        playResult.Status,
                        playResult.Message,
                        playResult.Error));
            }

            _lastError = null;

            return Task.FromResult(
                NvrResult<INvrPlaybackSession>.Ok(
                    playResult.Data,
                    playResult.Message));
        }

        /// <summary>
        /// 지정된 Dahua 재생 세션을 중지한다.
        /// </summary>
        public Task<NvrResult> StopAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    CreateCancelledResult("Stop"));
            }

            DahuaPlaybackSession dahuaSession =
                session as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    CreateInvalidSessionResult("Stop"));
            }

            NvrResult stopResult =
                DahuaSdkClient.StopPlayback(dahuaSession);

            if (!stopResult.Success)
            {
                _lastError = stopResult.Error;
                return Task.FromResult(stopResult);
            }

            _lastError = null;

            return Task.FromResult(stopResult);
        }

        /// <summary>
        /// 지정된 Dahua 재생 세션을 일시정지한다.
        /// </summary>
        public Task<NvrResult> PauseAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    CreateCancelledResult("Pause"));
            }

            DahuaPlaybackSession dahuaSession =
                session as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    CreateInvalidSessionResult("Pause"));
            }

            NvrResult pauseResult =
                DahuaSdkClient.PausePlayback(dahuaSession);

            if (!pauseResult.Success)
            {
                _lastError = pauseResult.Error;
                return Task.FromResult(pauseResult);
            }

            _lastError = null;

            return Task.FromResult(pauseResult);
        }

        /// <summary>
        /// 일시정지된 Dahua 재생 세션을 재개한다.
        /// </summary>
        public Task<NvrResult> ResumeAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    CreateCancelledResult("Resume"));
            }

            DahuaPlaybackSession dahuaSession =
                session as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    CreateInvalidSessionResult("Resume"));
            }

            NvrResult resumeResult =
                DahuaSdkClient.ResumePlayback(dahuaSession);

            if (!resumeResult.Success)
            {
                _lastError = resumeResult.Error;
                return Task.FromResult(resumeResult);
            }

            _lastError = null;

            return Task.FromResult(resumeResult);
        }

        /// <summary>
        /// Dahua NVR 녹화 영상을 역방향으로 재생한다.
        /// </summary>
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
                        "역재생 요청이 취소되었습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode = "CANCELLED",
                            ErrorMessage = "역재생 요청이 취소되었습니다.",
                            Operation = "PlayReverseByTime"
                        }));
            }

            if (!IsInitialized)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua Provider가 초기화되지 않았습니다."));
            }

            if (!IsLoggedIn)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        NvrResultStatus.LoginFailed,
                        "Dahua NVR에 로그인되어 있지 않습니다."));
            }

            NvrResult<DahuaPlaybackSession> result =
                DahuaSdkClient.PlayReverseByTime(
                    _loginSession,
                    request,
                    reverseStartTime);

            if (!result.Success || result.Data == null)
            {
                _lastError = result.Error;

                return Task.FromResult(
                    NvrResult<INvrPlaybackSession>.Fail(
                        result.Status,
                        result.Message,
                        result.Error));
            }

            _lastError = null;

            return Task.FromResult(
                NvrResult<INvrPlaybackSession>.Ok(
                    result.Data,
                    result.Message));
        }


        /// <summary>
        /// 지정된 Dahua 재생 세션을 특정 시각으로 이동한다.
        ///
        /// 기존 재생 핸들을 중지하거나 새로 생성하지 않고
        /// CLIENT_SeekPlayBack을 사용하여 현재 핸들의 위치만 변경한다.
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
                    CreateCancelledResult(
                        "Seek"));
            }

            DahuaPlaybackSession dahuaSession =
                session as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    CreateInvalidSessionResult(
                        "Seek"));
            }

            if (!IsInitialized)
            {
                return Task.FromResult(
                    NvrResult.Fail(
                        NvrResultStatus.SdkError,
                        "Dahua Provider가 초기화되지 않았습니다."));
            }

            if (!IsLoggedIn)
            {
                return Task.FromResult(
                    NvrResult.Fail(
                        NvrResultStatus.LoginFailed,
                        "Dahua NVR에 로그인되어 있지 않습니다."));
            }

            NvrResult seekResult =
                DahuaSdkClient.SeekPlayback(
                    dahuaSession,
                    targetTime);

            if (seekResult == null
                || !seekResult.Success)
            {
                _lastError =
                    seekResult == null
                        ? new NvrErrorInfo
                        {
                            ErrorCode =
                                "DAHUA_SEEK_RESULT_EMPTY",

                            ErrorMessage =
                                "Dahua 재생 위치 이동 결과가 없습니다.",

                            Operation =
                                "Seek"
                        }
                        : seekResult.Error;

                return Task.FromResult(
                    seekResult
                    ?? NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 위치 이동 결과가 없습니다.",
                        _lastError));
            }

            _lastError =
                null;

            return Task.FromResult(
                seekResult);
        }

        /// <summary>
        /// Dahua 재생 세션을 지정된 영상 시각과 상태로 정렬한다.
        ///
        /// 현재 구현 방식:
        /// 1. 기존 SeekAsync를 사용하여 목표 시각으로 재생 핸들을 교체
        /// 2. 요청된 재생속도 적용
        /// 3. RemainPaused가 true이면 정렬된 세션을 일시정지
        ///
        /// 추후 Dahua 직접 Seek API가 확인되면
        /// 이 메서드 내부 구현만 교체한다.
        /// </summary>
        public async Task<NvrResult<INvrPlaybackSession>> AlignPlaybackAsync(
            INvrPlaybackSession session,
            NvrPlaybackAlignmentRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return NvrResult<INvrPlaybackSession>.Fail(
                    NvrResultStatus.Cancelled,
                    "재생 세션 정렬 요청이 취소되었습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "PLAYBACK_ALIGNMENT_CANCELLED",
                        ErrorMessage = "재생 세션 정렬 요청이 취소되었습니다.",
                        Operation = "AlignPlayback"
                    });
            }

            if (request == null)
            {
                return NvrResult<INvrPlaybackSession>.Fail(
                    NvrResultStatus.Failed,
                    "재생 세션 정렬 요청 정보가 없습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "PLAYBACK_ALIGNMENT_REQUEST_REQUIRED",
                        ErrorMessage = "재생 세션 정렬 요청 정보가 없습니다.",
                        Operation = "AlignPlayback"
                    });
            }

            DahuaPlaybackSession dahuaSession =
                session as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return NvrResult<INvrPlaybackSession>.Fail(
                    NvrResultStatus.Failed,
                    "Dahua 재생 세션이 아닙니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "INVALID_DAHUA_PLAYBACK_SESSION",
                        ErrorMessage = "Dahua 재생 세션이 아닙니다.",
                        Operation = "AlignPlayback"
                    });
            }

            /*
             * 현재 Dahua SeekAsync는 정방향 PlayByTime을 이용한다.
             * 역방향 정렬은 아직 동일한 방식으로 보장할 수 없으므로
             * 성공으로 처리하지 않는다.
             */
            if (request.Direction
                != NvrPlaybackDirection.Forward)
            {
                return NvrResult<INvrPlaybackSession>.Fail(
                    NvrResultStatus.NotSupported,
                    "현재 Dahua Provider는 역방향 재생 세션 정렬을 지원하지 않습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "DAHUA_REVERSE_ALIGNMENT_NOT_SUPPORTED",
                        ErrorMessage =
                            "현재 Dahua Provider는 역방향 재생 세션 정렬을 지원하지 않습니다.",
                        Operation = "AlignPlayback"
                    });
            }

            /*
 * 1. 기존 재생 핸들에 직접 Seek 명령을 전달한다.
 */
            NvrResult seekResult =
                await SeekAsync(
                    dahuaSession,
                    request.TargetTime,
                    cancellationToken);

            if (seekResult == null
                || !seekResult.Success)
            {
                return NvrResult<INvrPlaybackSession>.Fail(
                    seekResult == null
                        ? NvrResultStatus.Failed
                        : seekResult.Status,

                    seekResult == null
                        ? "Dahua 재생 위치 이동 결과가 없습니다."
                        : seekResult.Message,

                    seekResult == null
                        ? new NvrErrorInfo
                        {
                            ErrorCode =
                                "DAHUA_ALIGNMENT_SEEK_RESULT_EMPTY",

                            ErrorMessage =
                                "Dahua 재생 위치 이동 결과가 없습니다.",

                            Operation =
                                "AlignPlayback"
                        }
                        : seekResult.Error);
            }

            /*
             * 현재 공통 동기화는 1배속 정방향에서만 실행한다.
             * 속도 변경 명령을 반복 호출하면 Pause 상태가 바뀔 수 있으므로
             * 여기서는 별도 속도 명령을 실행하지 않는다.
             */
            if (request.Speed != NvrPlaybackSpeed.Normal)
            {
                return NvrResult<INvrPlaybackSession>.Fail(
                    NvrResultStatus.NotSupported,
                    "Dahua 재생 정렬은 현재 1배속만 지원합니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode =
                            "DAHUA_ALIGNMENT_SPEED_NOT_SUPPORTED",

                        ErrorMessage =
                            "Dahua 재생 정렬은 현재 1배속만 지원합니다.",

                        Operation =
                            "AlignPlayback"
                    });
            }

            /*
             * 2. Seek 직후에는 목표 이전의 키프레임에 위치할 수 있다.
             * 디코더가 목표 시각까지 진행하도록 명시적으로 재개한다.
             */
            NvrResult resumeResult =
                await ResumeAsync(
                    dahuaSession,
                    cancellationToken);

            if (resumeResult == null
                || !resumeResult.Success)
            {
                return NvrResult<INvrPlaybackSession>.Fail(
                    resumeResult == null
                        ? NvrResultStatus.Failed
                        : resumeResult.Status,

                    resumeResult == null
                        ? "Dahua 정렬 세션 재개 결과가 없습니다."
                        : resumeResult.Message,

                    resumeResult == null
                        ? new NvrErrorInfo
                        {
                            ErrorCode =
                                "DAHUA_ALIGNMENT_RESUME_RESULT_EMPTY",

                            ErrorMessage =
                                "Dahua 정렬 세션 재개 결과가 없습니다.",

                            Operation =
                                "AlignPlayback"
                        }
                        : resumeResult.Error);
            }

            /*
             * 3. 첫 유효 OSD가 아니라
             * 목표 시각 부근에 도착한 실제 OSD를 기다린다.
             */
            NvrResult<DateTime> readyResult =
                await WaitForAlignmentPlaybackTimeAsync(
                    dahuaSession,
                    request.TargetTime,
                    cancellationToken);

            if (readyResult == null
                || !readyResult.Success)
            {
                /*
                 * 준비 실패 시 한 채널만 계속 재생되지 않도록
                 * 가능한 범위에서 다시 Pause한다.
                 */
                try
                {
                    await PauseAsync(
                        dahuaSession,
                        CancellationToken.None);
                }
                catch
                {
                }

                return NvrResult<INvrPlaybackSession>.Fail(
                    readyResult == null
                        ? NvrResultStatus.Failed
                        : readyResult.Status,

                    readyResult == null
                        ? "Dahua 재생 정렬 준비 확인 결과가 없습니다."
                        : readyResult.Message,

                    readyResult == null
                        ? new NvrErrorInfo
                        {
                            ErrorCode =
                                "DAHUA_ALIGNMENT_READY_RESULT_EMPTY",

                            ErrorMessage =
                                "Dahua 재생 정렬 준비 확인 결과가 없습니다.",

                            Operation =
                                "AlignPlayback"
                        }
                        : readyResult.Error);
            }

            /*
             * 4. 목표 시각에 도착한 실제 시간을 세션에 기록한다.
             */
            dahuaSession.SetCurrentPlaybackTime(
                readyResult.Data);

            /*
             * 5. 공통 서비스가 모든 채널을 함께 Resume할 수 있도록
             * RemainPaused=true이면 여기서 정지 상태로 반환한다.
             */
            if (request.RemainPaused)
            {
                NvrResult pauseResult =
                    await PauseAsync(
                        dahuaSession,
                        cancellationToken);

                if (pauseResult == null
                    || !pauseResult.Success)
                {
                    return NvrResult<INvrPlaybackSession>.Fail(
                        pauseResult == null
                            ? NvrResultStatus.Failed
                            : pauseResult.Status,

                        pauseResult == null
                            ? "Dahua 정렬 세션의 일시정지 결과가 없습니다."
                            : pauseResult.Message,

                        pauseResult == null
                            ? new NvrErrorInfo
                            {
                                ErrorCode =
                                    "DAHUA_ALIGNMENT_PAUSE_RESULT_EMPTY",

                                ErrorMessage =
                                    "Dahua 정렬 세션의 일시정지 결과가 없습니다.",

                                Operation =
                                    "AlignPlayback"
                            }
                            : pauseResult.Error);
                }
            }

            _lastError =
                null;

            return NvrResult<INvrPlaybackSession>.Ok(
                dahuaSession,
                "Dahua 재생 세션을 정렬했습니다. "
                + readyResult.Data.ToString(
                    "yyyy-MM-dd HH:mm:ss"));
        }

        /// <summary>
        /// NVR 접속 가능 여부를 확인한다.
        /// </summary>
        public Task<NvrResult> TestConnectionAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    CreateCancelledResult("TestConnection"));
            }

            if (!IsInitialized)
            {
                NvrResult initializeResult = Initialize();

                if (!initializeResult.Success)
                {
                    _lastError = initializeResult.Error;
                    return Task.FromResult(initializeResult);
                }
            }

            NvrResult<DahuaLoginSession> loginResult =
                DahuaSdkClient.Login(connectionInfo);

            if (!loginResult.Success || loginResult.Data == null)
            {
                _lastError = loginResult.Error;

                return Task.FromResult(
                    NvrResult.Fail(
                        loginResult.Status,
                        string.IsNullOrWhiteSpace(loginResult.Message)
                            ? "Dahua NVR 연결 테스트에 실패했습니다."
                            : loginResult.Message,
                        loginResult.Error));
            }

            using (loginResult.Data)
            {
                // 연결 테스트용 로그인 세션은 즉시 로그아웃된다.
            }

            _lastError = null;

            return Task.FromResult(
                NvrResult.Ok("Dahua NVR 연결 테스트에 성공했습니다."));
        }

        /// <summary>
        /// 지정된 시간 구간에 녹화 영상이 존재하는지 확인한다.
        /// </summary>
        public Task<NvrResult<bool>> QueryRecordExistsAsync(
            NvrRecordQueryRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                CreateNotSupportedResult<bool>(
                    "QueryRecordExists",
                    "Dahua SDK 녹화 조회 기능은 아직 구현되지 않았습니다."));
        }

        /// <summary>
        /// Dahua NVR에서 로그아웃한다.
        /// </summary>
        public Task<NvrResult> LogoutAsync(CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (_loginSession != null)
            {
                _loginSession.Dispose();
                _loginSession = null;
            }

            _lastError = null;

            return Task.FromResult(
                NvrResult.Ok("Dahua NVR 로그아웃이 완료되었습니다."));
        }


        /**Helper Methods**/

        /// <summary>
        /// Provider에서 마지막으로 발생한 오류 정보를 반환한다.
        /// </summary>
        public NvrErrorInfo GetLastError()
        {
            EnsureNotDisposed();

            return _lastError;
        }

        /// <summary>
        /// Provider에서 사용 중인 로그인 세션과 SDK 참조를 정리한다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_loginSession != null)
            {
                _loginSession.Dispose();
                _loginSession = null;
            }

            if (_runtimeAcquired)
            {
                DahuaSdkRuntime.Release();
                _runtimeAcquired = false;
            }

            _lastError = null;
            _disposed = true;
        }

        /// <summary>
        /// Provider가 이미 해제되었는지 확인한다.
        /// </summary>
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(typeof(DahuaNvrProvider).FullName);
            }
        }

        /// <summary>
        /// 지원하지 않는 기능 결과를 생성한다.
        /// </summary>
        private NvrResult CreateNotSupportedResult(
            string operation,
            string message)
        {
            _lastError = new NvrErrorInfo
            {
                ErrorCode = "NOT_IMPLEMENTED",
                ErrorMessage = message,
                Operation = operation
            };

            return NvrResult.Fail(
                NvrResultStatus.NotSupported,
                message,
                _lastError);
        }

        /// <summary>
        /// 반환 데이터를 포함하는 지원하지 않는 기능 결과를 생성한다.
        /// </summary>
        private NvrResult<T> CreateNotSupportedResult<T>(
            string operation,
            string message)
        {
            _lastError = new NvrErrorInfo
            {
                ErrorCode = "NOT_IMPLEMENTED",
                ErrorMessage = message,
                Operation = operation
            };

            return NvrResult<T>.Fail(
                NvrResultStatus.NotSupported,
                message,
                _lastError);
        }

        /// <summary>
        /// 일반 실패 결과를 생성한다.
        /// </summary>
        private NvrResult CreateFailedResult(
            NvrResultStatus status,
            string message,
            string errorCode,
            string operation)
        {
            _lastError = new NvrErrorInfo
            {
                ErrorCode = errorCode,
                ErrorMessage = message,
                Operation = operation
            };

            return NvrResult.Fail(status, message, _lastError);
        }

        /// <summary>
        /// 취소 결과를 생성한다.
        /// </summary>
        private static NvrResult CreateCancelledResult(string operation)
        {
            return NvrResult.Fail(
                NvrResultStatus.Cancelled,
                "요청이 취소되었습니다.",
                new NvrErrorInfo
                {
                    ErrorCode = "CANCELLED",
                    ErrorMessage = "요청이 취소되었습니다.",
                    Operation = operation
                });
        }

        /// <summary>
        /// Dahua 재생 세션 타입이 아닌 경우 실패 결과를 생성한다.
        /// </summary>
        private NvrResult CreateInvalidSessionResult(string operation)
        {
            _lastError = new NvrErrorInfo
            {
                ErrorCode = "INVALID_DAHUA_SESSION",
                ErrorMessage = "Dahua 재생 세션이 아닙니다.",
                Operation = operation
            };

            return NvrResult.Fail(
                NvrResultStatus.Failed,
                "Dahua 재생 세션이 아닙니다.",
                _lastError);
        }

        /// <summary>
        /// Dahua 재생속도를 변경한다.
        /// 
        /// Dahua SDK의 속도 제어 함수 호출 후 일시정지 상태가 풀릴 수 있으므로,
        /// 기존 상태가 Paused였으면 속도 변경 후 다시 Pause를 적용한다.
        /// </summary>
        public Task<NvrResult> SetPlaybackSpeedAsync(
            INvrPlaybackSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    CreateCancelledResult("SetPlaybackSpeed"));
            }

            DahuaPlaybackSession dahuaSession =
                session as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    CreateInvalidSessionResult("SetPlaybackSpeed"));
            }

            // 여기서 선언한다.
            // 속도 변경 전 상태가 Paused였는지 확인하기 위한 값이다.
            NvrPlaybackState stateBeforeChange =
                dahuaSession.State;

            NvrResult speedResult =
                DahuaSdkClient.SetPlaybackSpeed(
                    dahuaSession,
                    speed);

            if (!speedResult.Success)
            {
                _lastError = speedResult.Error;
                return Task.FromResult(speedResult);
            }

            // Dahua SDK의 속도 변경 호출 과정에서 일시정지가 풀릴 수 있으므로
            // 변경 전 상태가 Paused였다면 다시 Pause를 걸어준다.
            if (stateBeforeChange == NvrPlaybackState.Paused)
            {
                NvrResult pauseResult =
                    DahuaSdkClient.PausePlayback(dahuaSession);

                if (!pauseResult.Success)
                {
                    _lastError = pauseResult.Error;
                    return Task.FromResult(pauseResult);
                }

                dahuaSession.SetState(NvrPlaybackState.Paused);
            }

            _lastError = null;

            return Task.FromResult(speedResult);
        }

        /// <summary>
        /// Dahua 재생 세션의 실제 영상재생시간을 조회한다.
        /// </summary>
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
                        "재생시간 조회 요청이 취소되었습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode = "CANCELLED",
                            ErrorMessage = "재생시간 조회 요청이 취소되었습니다.",
                            Operation = "GetPlaybackTime"
                        }));
            }

            DahuaPlaybackSession dahuaSession =
                session as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    NvrResult<DateTime>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 세션이 아닙니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode = "INVALID_DAHUA_SESSION",
                            ErrorMessage = "Dahua 재생 세션이 아닙니다.",
                            Operation = "GetPlaybackTime"
                        }));
            }

            NvrResult<DateTime> result =
                DahuaSdkClient.GetPlaybackOsdTime(dahuaSession);

            if (!result.Success)
            {
                _lastError = result.Error;
                return Task.FromResult(result);
            }

            _lastError = null;
            return Task.FromResult(result);
        }
        /// <summary>
        /// Dahua NVR의 지정 채널 영상 원본 정보를 조회한다.
        /// 
        /// PlayerView의 "원본 비율" 표시 모드에서 사용할
        /// 영상 원본 Width / Height를 반환한다.
        /// 
        /// 주의:
        /// Dahua Native SDK 호출 과정에서 예외가 발생할 수 있으므로,
        /// Provider 레벨에서도 예외를 반드시 NvrResult 실패 결과로 변환한다.
        /// </summary>
        public Task<NvrResult<NvrVideoSourceInfo>> GetVideoSourceInfoAsync(
            int channelNo,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<NvrVideoSourceInfo>.Fail(
                        NvrResultStatus.Cancelled,
                        "영상 원본 정보 조회 요청이 취소되었습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode = "CANCELLED",
                            ErrorMessage = "영상 원본 정보 조회 요청이 취소되었습니다.",
                            Operation = "GetVideoSourceInfo"
                        }));
            }

            if (!IsLoggedIn || _loginSession == null)
            {
                return Task.FromResult(
                    NvrResult<NvrVideoSourceInfo>.Fail(
                        NvrResultStatus.LoginFailed,
                        "Dahua NVR에 로그인되어 있지 않습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode = "DAHUA_NOT_LOGGED_IN",
                            ErrorMessage = "Dahua NVR에 로그인되어 있지 않습니다.",
                            Operation = "GetVideoSourceInfo"
                        }));
            }

            if (channelNo < 0)
            {
                return Task.FromResult(
                    NvrResult<NvrVideoSourceInfo>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 채널 번호가 올바르지 않습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode = "DAHUA_INVALID_CHANNEL_NO",
                            ErrorMessage = "Dahua 채널 번호가 올바르지 않습니다.",
                            Operation = "GetVideoSourceInfo"
                        }));
            }

            try
            {
                NvrResult<NvrVideoSourceInfo> result =
                    DahuaSdkClient.GetVideoSourceInfo(
                        _loginSession,
                        channelNo);

                if (!result.Success)
                {
                    _lastError = result.Error;
                    return Task.FromResult(result);
                }

                _lastError = null;

                return Task.FromResult(result);
            }
            catch (EntryPointNotFoundException ex)
            {
                NvrErrorInfo error =
                    CreateVideoSourceInfoExceptionError(
                        "DAHUA_CONFIG_ENTRYPOINT_NOT_FOUND",
                        "현재 Dahua SDK DLL에서 설정 조회 함수를 찾을 수 없습니다.",
                        ex);

                _lastError = error;

                return Task.FromResult(
                    NvrResult<NvrVideoSourceInfo>.Fail(
                        NvrResultStatus.Failed,
                        error.ErrorMessage,
                        error));
            }
            catch (DllNotFoundException ex)
            {
                NvrErrorInfo error =
                    CreateVideoSourceInfoExceptionError(
                        "DAHUA_SDK_DLL_NOT_FOUND",
                        "Dahua SDK DLL을 찾을 수 없습니다.",
                        ex);

                _lastError = error;

                return Task.FromResult(
                    NvrResult<NvrVideoSourceInfo>.Fail(
                        NvrResultStatus.Failed,
                        error.ErrorMessage,
                        error));
            }
            catch (BadImageFormatException ex)
            {
                NvrErrorInfo error =
                    CreateVideoSourceInfoExceptionError(
                        "DAHUA_SDK_BITNESS_MISMATCH",
                        "Dahua SDK DLL의 32/64bit 구성이 현재 CamViewer와 맞지 않습니다.",
                        ex);

                _lastError = error;

                return Task.FromResult(
                    NvrResult<NvrVideoSourceInfo>.Fail(
                        NvrResultStatus.Failed,
                        error.ErrorMessage,
                        error));
            }
            catch (AccessViolationException ex)
            {
                NvrErrorInfo error =
                    CreateVideoSourceInfoExceptionError(
                        "DAHUA_CONFIG_ACCESS_VIOLATION",
                        "Dahua 해상도 조회 중 메모리 접근 오류가 발생했습니다. Native 함수 선언 또는 구조체 정의가 SDK와 맞지 않을 가능성이 높습니다.",
                        ex);

                _lastError = error;

                return Task.FromResult(
                    NvrResult<NvrVideoSourceInfo>.Fail(
                        NvrResultStatus.Failed,
                        error.ErrorMessage,
                        error));
            }
            catch (Exception ex)
            {
                NvrErrorInfo error =
                    CreateVideoSourceInfoExceptionError(
                        "DAHUA_VIDEO_SOURCE_INFO_EXCEPTION",
                        "Dahua 영상 원본 정보 조회 중 예외가 발생했습니다.",
                        ex);

                _lastError = error;

                return Task.FromResult(
                    NvrResult<NvrVideoSourceInfo>.Fail(
                        NvrResultStatus.Failed,
                        error.ErrorMessage,
                        error));
            }
        }

        /// <summary>
        /// Dahua 영상 원본 정보 조회 중 발생한 예외를 NvrErrorInfo로 변환한다.
        /// </summary>
        private static NvrErrorInfo CreateVideoSourceInfoExceptionError(
            string errorCode,
            string message,
            Exception exception)
        {
            return new NvrErrorInfo
            {
                ErrorCode = errorCode,
                ErrorMessage =
                    message
                    + " "
                    + exception.GetType().Name
                    + ": "
                    + exception.Message,
                Operation = "GetVideoSourceInfo"
            };
        }

        /// <summary>
        /// Dahua 직접 Seek 후 실제 OSD 시간이 목표 시각 부근에
        /// 도착할 때까지 기다린다.
        ///
        /// Dahua OSD와 CLIENT_SeekPlayBack은 초 단위로 동작하므로
        /// 목표와 실제값을 모두 초 단위로 정규화하여 비교한다.
        /// </summary>
        private async Task<NvrResult<DateTime>>
            WaitForAlignmentPlaybackTimeAsync(
                DahuaPlaybackSession session,
                DateTime targetTime,
                CancellationToken cancellationToken)
        {
            const int maximumWaitMilliseconds =
                5000;

            const int pollingIntervalMilliseconds =
                100;

            /*
             * SDK가 목표 이전 키프레임부터 디코딩할 수 있으므로
             * 목표보다 1초 이전까지 허용한다.
             *
             * 폴링 간격과 OSD 초 단위 갱신을 고려하여
             * 목표보다 3초 이후까지 허용한다.
             */
            const int allowedBeforeSeconds =
                1;

            const int allowedAfterSeconds =
                3;

            /*
             * 서비스의 추정 시간에는 밀리초가 포함될 수 있지만
             * Dahua OSD에는 밀리초가 존재하지 않는다.
             *
             * 비교 전에 반드시 초 단위로 잘라낸다.
             */
            DateTime normalizedTargetTime =
                TruncateToSecond(
                    targetTime);

            DateTime minimumAcceptedTime =
                normalizedTargetTime.AddSeconds(
                    -allowedBeforeSeconds);

            DateTime maximumAcceptedTime =
                normalizedTargetTime.AddSeconds(
                    allowedAfterSeconds);

            DateTime? lastObservedTime =
                null;

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            while (stopwatch.ElapsedMilliseconds
                < maximumWaitMilliseconds)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return NvrResult<DateTime>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 정렬 준비 확인이 취소되었습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "DAHUA_ALIGNMENT_READY_CANCELLED",

                            ErrorMessage =
                                "Dahua 재생 정렬 준비 확인이 취소되었습니다.",

                            Operation =
                                "WaitForAlignmentPlaybackTime"
                        });
                }

                NvrResult<DateTime> timeResult =
                    await GetPlaybackTimeAsync(
                        session,
                        cancellationToken);

                if (timeResult != null
                    && timeResult.Success)
                {
                    DateTime actualTime =
                        timeResult.Data;

                    DateTime normalizedActualTime =
                        TruncateToSecond(
                            actualTime);

                    lastObservedTime =
                        normalizedActualTime;

                    /*
                     * 여기에서는 세션 StartTime/EndTime을 다시 검사하지 않는다.
                     *
                     * 목표 시각의 조회 범위 검증은 SeekPlayback에서 이미 완료했다.
                     * 이 메서드의 책임은 실제 OSD가 목표 부근에 도착했는지만
                     * 확인하는 것이다.
                     */
                    bool reachedTargetRange =
                        normalizedActualTime
                            >= minimumAcceptedTime
                        && normalizedActualTime
                            <= maximumAcceptedTime;

                    if (reachedTargetRange)
                    {
                        return NvrResult<DateTime>.Ok(
                            normalizedActualTime,
                            "Dahua 재생 시간이 목표 시각 부근에 도착했습니다.");
                    }
                }

                int remainingMilliseconds =
                    maximumWaitMilliseconds
                    - Convert.ToInt32(
                        stopwatch.ElapsedMilliseconds);

                if (remainingMilliseconds <= 0)
                {
                    break;
                }

                int delayMilliseconds =
                    Math.Min(
                        pollingIntervalMilliseconds,
                        remainingMilliseconds);

                try
                {
                    await Task.Delay(
                        delayMilliseconds,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return NvrResult<DateTime>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 정렬 준비 확인이 취소되었습니다.",
                        new NvrErrorInfo
                        {
                            ErrorCode =
                                "DAHUA_ALIGNMENT_READY_CANCELLED",

                            ErrorMessage =
                                "Dahua 재생 정렬 준비 확인이 취소되었습니다.",

                            Operation =
                                "WaitForAlignmentPlaybackTime"
                        });
                }
            }

            string lastTimeText =
                lastObservedTime.HasValue
                    ? lastObservedTime.Value.ToString(
                        "yyyy-MM-dd HH:mm:ss.fff")
                    : "확인 불가";

            return NvrResult<DateTime>.Fail(
                NvrResultStatus.Failed,
                "제한시간 안에 Dahua 재생 시간이 목표 시각에 도착하지 못했습니다. "
                + "목표="
                + normalizedTargetTime.ToString(
                    "yyyy-MM-dd HH:mm:ss.fff")
                + ", 허용범위="
                + minimumAcceptedTime.ToString(
                    "HH:mm:ss.fff")
                + " ~ "
                + maximumAcceptedTime.ToString(
                    "HH:mm:ss.fff")
                + ", 마지막 확인="
                + lastTimeText,
                new NvrErrorInfo
                {
                    ErrorCode =
                        "DAHUA_ALIGNMENT_TARGET_TIMEOUT",

                    ErrorMessage =
                        "제한시간 안에 Dahua 재생 시간이 목표 시각에 도착하지 못했습니다.",

                    Operation =
                        "WaitForAlignmentPlaybackTime"
                });
        }

        /// <summary>
        /// DateTime의 밀리초 이하 값을 제거하여
        /// Dahua OSD의 초 단위 시간과 동일한 정밀도로 변환한다.
        /// </summary>
        private static DateTime TruncateToSecond(
            DateTime value)
        {
            return new DateTime(
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                value.Second,
                value.Kind);
        }

    }
}