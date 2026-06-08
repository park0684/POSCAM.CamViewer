using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Attributes;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.ApiProviders.Providers
{
    /// <summary>
    /// TP-Link API 기반 NVR Provider의 기본 골격이다.
    ///
    /// 현재 단계에서는 제조사 선택 목록과 Provider 로딩 구조를 확인하기 위해 사용한다.
    /// 실제 API 연결 테스트와 영상 조회 기능은 추후 구현한다.
    /// </summary>
    [NvrProviderExport(
        "TPLINK_API",
        "TP-Link API",
        "TP-Link",
        NvrConnectionType.Api)]
    public sealed class TpLinkApiNvrProvider : INvrProvider
    {
        private bool _disposed;
        private NvrErrorInfo _lastError;

        /// <summary>
        /// TP-Link API Provider를 초기화한다.
        /// </summary>
        public TpLinkApiNvrProvider()
        {
            Metadata = new ProviderMetadata
            {
                ProviderKey = "TPLINK_API",
                DisplayName = "TP-Link API",
                Vendor = "TP-Link",
                ConnectionType = NvrConnectionType.Api,
                Version = "1.0.0",
                RenderMode = NvrRenderMode.RtspUrl,
                RequiredArchitecture = "x64"
            };
        }

        public ProviderMetadata Metadata { get; private set; }

        public bool IsInitialized { get; private set; }

        public bool IsLoggedIn { get; private set; }


        public ProviderCapabilities GetCapabilities()
        {
            EnsureNotDisposed();

            return new ProviderCapabilities
            {
                RenderMode = NvrRenderMode.RtspUrl,
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

        public NvrResult Initialize()
        {
            EnsureNotDisposed();

            IsInitialized = true;
            _lastError = null;

            return NvrResult.Ok("TP-Link API Provider가 초기화되었습니다.");
        }

        public Task<NvrResult> LoginAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                NotSupported(
                    "Login",
                    "TP-Link API 로그인 기능은 아직 구현되지 않았습니다."));
        }

        public Task<NvrResult<INvrPlaybackSession>> PlayByTimeAsync(
            NvrPlaybackRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                NotSupported<INvrPlaybackSession>(
                    "PlayByTime",
                    "TP-Link API 재생 기능은 아직 구현되지 않았습니다."));
        }

        public Task<NvrResult> StopAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                NotSupported(
                    "Stop",
                    "TP-Link API 재생 중지 기능은 아직 구현되지 않았습니다."));
        }

        public Task<NvrResult> PauseAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                NotSupported(
                    "Pause",
                    "TP-Link API 일시정지 기능은 아직 구현되지 않았습니다."));
        }

        public Task<NvrResult> ResumeAsync(
            INvrPlaybackSession session,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                NotSupported(
                    "Resume",
                    "TP-Link API 재개 기능은 아직 구현되지 않았습니다."));
        }

        public Task<NvrResult> SeekAsync(
            INvrPlaybackSession session,
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                NotSupported(
                    "Seek",
                    "TP-Link API 재생 위치 이동 기능은 아직 구현되지 않았습니다."));
        }

        public Task<NvrResult> TestConnectionAsync(
            NvrConnectionInfo connectionInfo,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                NotSupported(
                    "TestConnection",
                    "TP-Link API 연결 테스트 기능은 아직 구현되지 않았습니다."));
        }

        public Task<NvrResult<bool>> QueryRecordExistsAsync(
            NvrRecordQueryRequest request,
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            return Task.FromResult(
                NotSupported<bool>(
                    "QueryRecordExists",
                    "TP-Link API 녹화 조회 기능은 아직 구현되지 않았습니다."));
        }

        public Task<NvrResult> LogoutAsync(
            CancellationToken cancellationToken)
        {
            EnsureNotDisposed();

            IsLoggedIn = false;
            _lastError = null;

            return Task.FromResult(
                NvrResult.Ok("TP-Link API Provider 로그아웃이 완료되었습니다."));
        }

        public NvrErrorInfo GetLastError()
        {
            EnsureNotDisposed();

            return _lastError;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            IsLoggedIn = false;
            IsInitialized = false;
            _lastError = null;
            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    typeof(TpLinkApiNvrProvider).FullName);
            }
        }

        private NvrResult NotSupported(
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

        private NvrResult<T> NotSupported<T>(
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
        /// TP-Link Provider의 재생속도를 변경한다.
        /// 현재 단계에서는 미지원으로 처리한다.
        /// </summary>
        public Task<NvrResult> SetPlaybackSpeedAsync(
            INvrPlaybackSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                NvrResult.Fail(
                    NvrResultStatus.NotSupported,
                    "TP-Link 재생속도 변경 기능은 아직 지원되지 않습니다.",
                    new NvrErrorInfo
                    {
                        ErrorCode = "PLAYBACK_SPEED_NOT_SUPPORTED",
                        ErrorMessage = "TP-Link Provider는 재생속도 변경을 지원하지 않습니다.",
                        Operation = "SetPlaybackSpeed"
                    }));
        }
    }
}
