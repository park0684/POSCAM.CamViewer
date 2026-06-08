using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Attributes;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Sdk;
using System;
using System.Threading;
using System.Threading.Tasks;

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
    public sealed class DahuaNvrProvider : INvrProvider,INvrPlaybackPositionProvider
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

                CanChangeSpeed = true
            };
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
        /// 지정된 Dahua 재생 세션을 특정 시각으로 이동한다.
        ///
        /// 현재 단계에서는 Dahua SDK의 직접 Seek API를 사용하지 않고,
        /// 기존 재생을 중지한 뒤 targetTime 기준으로 다시 재생한다.
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
                    CreateCancelledResult("Seek"));
            }

            DahuaPlaybackSession dahuaSession =
                session as DahuaPlaybackSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    CreateInvalidSessionResult("Seek"));
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

            if (targetTime < dahuaSession.StartTime)
            {
                return Task.FromResult(
                    NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "이동할 재생 시각은 현재 재생 시작 시각보다 이전일 수 없습니다."));
            }

            if (targetTime >= dahuaSession.EndTime)
            {
                return Task.FromResult(
                    NvrResult.Fail(
                        NvrResultStatus.Failed,
                        "이동할 재생 시각은 종료 시각보다 이전이어야 합니다."));
            }

            var replayRequest = new NvrPlaybackRequest
            {
                CounterNo = dahuaSession.CounterNo,
                NvrNo = dahuaSession.NvrNo,
                ChannelNo = dahuaSession.ChannelNo,
                ScreenPosition = dahuaSession.ScreenPosition,
                SearchDateTime = dahuaSession.SearchDateTime,
                StartTime = targetTime,
                EndTime = dahuaSession.EndTime,
                RenderTargetHandle = dahuaSession.RenderTargetHandle,
                AutoPlay = dahuaSession.AutoPlay
            };

            NvrResult<DahuaPlaybackSession> replayResult =
                DahuaSdkClient.PlayByTime(
                    _loginSession,
                    replayRequest);

            if (!replayResult.Success || replayResult.Data == null)
            {
                _lastError = replayResult.Error;

                return Task.FromResult(
                    NvrResult.Fail(
                        replayResult.Status,
                        string.IsNullOrWhiteSpace(replayResult.Message)
                            ? "Dahua 재생 위치 이동에 실패했습니다."
                            : replayResult.Message,
                        replayResult.Error));
            }

            IntPtr newPlaybackHandle =
                replayResult.Data.DetachPlaybackHandle();

            dahuaSession.ReplacePlaybackHandle(
                newPlaybackHandle,
                targetTime);

            replayResult.Data.Dispose();

            _lastError = null;

            return Task.FromResult(
                NvrResult.Ok(
                    "Dahua 재생 위치를 이동했습니다. "
                    + targetTime.ToString("yyyy-MM-dd HH:mm:ss")));
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
    }
}