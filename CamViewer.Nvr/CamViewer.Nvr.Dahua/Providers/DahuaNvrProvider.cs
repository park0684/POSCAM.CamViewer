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
    public sealed class DahuaNvrProvider : INvrProvider
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
        ///
        /// 현재는 SDK가 연결되지 않은 임시 상태이므로 선택 기능을 모두 false로 반환한다.
        /// </summary>
        public ProviderCapabilities GetCapabilities()
        {
            EnsureNotDisposed();

            return new ProviderCapabilities
            {
                RenderMode = NvrRenderMode.DirectRender,
                CanPause = false,
                CanResume = false,
                CanSeek = false,
                CanPlayByRange = false,
                CanSnapshot = false,
                CanTestConnection = false,
                CanQueryRecordExists = false,
                CanGetPlaybackPosition = false
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
        /// 지정된 시각 기준으로 Dahua 녹화 영상을 재생한다.
        ///
        /// 현재 단계에서는 실제 SDK 재생을 수행하지 않는다.
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
                        "재생 요청이 취소되었습니다."));
            }

            return Task.FromResult(
                CreateNotSupportedResult<INvrPlaybackSession>(
                    "PlayByTime",
                    "Dahua SDK 재생 기능은 아직 구현되지 않았습니다."));
        }

        /// <summary>
        /// 지정된 재생 세션을 중지한다.
        /// </summary>
        public Task<NvrResult> StopAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                CreateNotSupportedResult(
                    "Stop",
                    "Dahua SDK 재생 중지 기능은 아직 구현되지 않았습니다."));
        }

        /// <summary>
        /// 지정된 재생 세션을 일시정지한다.
        /// </summary>
        public Task<NvrResult> PauseAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                CreateNotSupportedResult(
                    "Pause",
                    "Dahua SDK 일시정지 기능은 아직 구현되지 않았습니다."));
        }

        /// <summary>
        /// 일시정지된 재생 세션을 재개한다.
        /// </summary>
        public Task<NvrResult> ResumeAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                CreateNotSupportedResult(
                    "Resume",
                    "Dahua SDK 재개 기능은 아직 구현되지 않았습니다."));
        }

        /// <summary>
        /// 지정된 재생 세션을 특정 시각으로 이동한다.
        /// </summary>
        public Task<NvrResult> SeekAsync(
            INvrPlaybackSession session,
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                CreateNotSupportedResult(
                    "Seek",
                    "Dahua SDK 재생 위치 이동 기능은 아직 구현되지 않았습니다."));
        }

        /// <summary>
        /// NVR 접속 가능 여부를 확인한다.
        /// </summary>
        public Task<NvrResult> TestConnectionAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                CreateNotSupportedResult(
                    "TestConnection",
                    "Dahua SDK 연결 테스트 기능은 아직 구현되지 않았습니다."));
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
    }
}