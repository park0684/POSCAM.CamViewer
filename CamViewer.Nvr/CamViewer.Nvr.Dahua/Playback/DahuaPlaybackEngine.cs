using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Providers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// Dahua NVR의 다중채널 녹화영상 재생을 담당하는
    /// 제조사 전용 고수준 재생 엔진.
    ///
    /// 최종적으로 다음 기능은 모두 이 클래스와
    /// DahuaPlaybackSynchronizer 내부에서 처리한다.
    ///
    /// - 여러 채널의 재생 세션 생성
    /// - 채널별 시간 보정값 적용
    /// - Seek 및 키프레임 처리
    /// - 정방향 및 역방향 전환
    /// - 재생속도 변경
    /// - 채널 간 동기화
    /// - Dahua SDK 오류 복구
    ///
    /// 현재 단계에서는 공통 인터페이스 연결 여부만 검증하기 위해
    /// 실제 기능은 아직 구현하지 않는다.
    /// </summary>
    internal sealed class DahuaPlaybackEngine :
        INvrPlaybackEngine
    {
        private readonly DahuaNvrProvider _provider;

        /// <summary>
        /// Dahua 재생 엔진을 초기화한다.
        ///
        /// Provider 초기화와 로그인은
        /// 엔진 생성 전에 완료되어 있어야 한다.
        /// </summary>
        public DahuaPlaybackEngine(
            DahuaNvrProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(
                    "provider");
            }

            _provider =
                provider;
        }

        /// <summary>
        /// Dahua 다중채널 재생 그룹을 준비한다.
        ///
        /// 다음 단계에서 각 채널의 PlayByTimeAsync를 호출하고
        /// 반환된 DahuaPlaybackSession을 그룹에 등록한다.
        /// </summary>
        public Task<NvrResult<INvrPlaybackGroupSession>>
            OpenAsync(
                NvrPlaybackGroupRequest request,
                CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackGroupSession>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 그룹 준비 요청이 취소되었습니다.",
                        CreateError(
                            "DAHUA_GROUP_OPEN_CANCELLED",
                            "Dahua 재생 그룹 준비 요청이 취소되었습니다.",
                            "Open")));
            }

            if (request == null)
            {
                return Task.FromResult(
                    NvrResult<INvrPlaybackGroupSession>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹 요청 정보가 없습니다.",
                        CreateError(
                            "DAHUA_GROUP_REQUEST_REQUIRED",
                            "Dahua 재생 그룹 요청 정보가 없습니다.",
                            "Open")));
            }

            return Task.FromResult(
                CreateNotImplementedGroupResult(
                    "Open"));
        }

        /// <summary>
        /// 준비된 Dahua 재생 그룹의 모든 채널을 재생한다.
        /// </summary>
        public Task<NvrResult> StartAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Start"));
        }

        /// <summary>
        /// Dahua 재생 그룹의 모든 채널을 일시정지한다.
        /// </summary>
        public Task<NvrResult> PauseAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Pause"));
        }

        /// <summary>
        /// 일시정지된 Dahua 재생 그룹을 재개한다.
        /// </summary>
        public Task<NvrResult> ResumeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Resume"));
        }

        /// <summary>
        /// Dahua 재생 그룹을 지정 시각으로 이동한다.
        /// </summary>
        public Task<NvrResult> SeekAsync(
            INvrPlaybackGroupSession session,
            DateTime targetTime,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Seek"));
        }

        /// <summary>
        /// Dahua 재생 그룹의 방향을 변경한다.
        /// </summary>
        public Task<NvrResult> SetDirectionAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackDirection direction,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "SetDirection"));
        }

        /// <summary>
        /// Dahua 재생 그룹의 재생속도를 변경한다.
        /// </summary>
        public Task<NvrResult> SetSpeedAsync(
            INvrPlaybackGroupSession session,
            NvrPlaybackSpeed speed,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "SetSpeed"));
        }

        /// <summary>
        /// Dahua 제조사 방식으로 채널 간 동기화를 수행한다.
        /// </summary>
        public Task<NvrResult> SynchronizeAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Synchronize"));
        }

        /// <summary>
        /// Dahua 재생 그룹의 현재 상태를 반환한다.
        ///
        /// 그룹 상태는 제조사 프로젝트가 직접 관리한다.
        /// </summary>
        public Task<NvrResult<NvrPlaybackGroupStatus>>
            GetStatusAsync(
                INvrPlaybackGroupSession session,
                CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(
                    NvrResult<NvrPlaybackGroupStatus>.Fail(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 그룹 상태 조회가 취소되었습니다.",
                        CreateError(
                            "DAHUA_GROUP_STATUS_CANCELLED",
                            "Dahua 재생 그룹 상태 조회가 취소되었습니다.",
                            "GetStatus")));
            }

            DahuaPlaybackGroupSession dahuaSession =
                session as DahuaPlaybackGroupSession;

            if (dahuaSession == null)
            {
                return Task.FromResult(
                    NvrResult<NvrPlaybackGroupStatus>.Fail(
                        NvrResultStatus.Failed,
                        "Dahua 재생 그룹 세션이 아닙니다.",
                        CreateError(
                            "INVALID_DAHUA_GROUP_SESSION",
                            "Dahua 재생 그룹 세션이 아닙니다.",
                            "GetStatus")));
            }

            var status =
                new NvrPlaybackGroupStatus
                {
                    CurrentPlaybackTime =
                        dahuaSession.CurrentPlaybackTime,

                    State =
                        dahuaSession.State,

                    Direction =
                        dahuaSession.Direction,

                    Speed =
                        dahuaSession.Speed,

                    IsReady =
                        dahuaSession.IsReady,

                    SynchronizationAvailable =
                        dahuaSession.ChannelCount > 1,

                    IsSynchronized =
                        dahuaSession.IsSynchronized,

                    MaximumDriftSeconds =
                        dahuaSession.MaximumDriftSeconds,

                    Message =
                        dahuaSession.StatusMessage
                };

            return Task.FromResult(
                NvrResult<NvrPlaybackGroupStatus>.Ok(
                    status,
                    "Dahua 재생 그룹 상태를 조회했습니다."));
        }

        /// <summary>
        /// Dahua 재생 그룹의 모든 채널을 중지한다.
        /// </summary>
        public Task<NvrResult> StopAsync(
            INvrPlaybackGroupSession session,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                CreateNotImplementedResult(
                    "Stop"));
        }

        /// <summary>
        /// 아직 구현되지 않은 그룹 반환 명령의 결과를 생성한다.
        /// </summary>
        private static NvrResult<INvrPlaybackGroupSession>
            CreateNotImplementedGroupResult(
                string operation)
        {
            return NvrResult<INvrPlaybackGroupSession>.Fail(
                NvrResultStatus.NotSupported,
                "Dahua 다중채널 재생 엔진의 "
                + operation
                + " 기능은 아직 연결되지 않았습니다.",
                CreateError(
                    "DAHUA_GROUP_ENGINE_NOT_IMPLEMENTED",
                    "Dahua 다중채널 재생 엔진의 "
                    + operation
                    + " 기능은 아직 연결되지 않았습니다.",
                    operation));
        }

        /// <summary>
        /// 아직 구현되지 않은 명령의 결과를 생성한다.
        /// </summary>
        private static NvrResult
            CreateNotImplementedResult(
                string operation)
        {
            return NvrResult.Fail(
                NvrResultStatus.NotSupported,
                "Dahua 다중채널 재생 엔진의 "
                + operation
                + " 기능은 아직 연결되지 않았습니다.",
                CreateError(
                    "DAHUA_GROUP_ENGINE_NOT_IMPLEMENTED",
                    "Dahua 다중채널 재생 엔진의 "
                    + operation
                    + " 기능은 아직 연결되지 않았습니다.",
                    operation));
        }

        /// <summary>
        /// Dahua 그룹 재생 오류 정보를 생성한다.
        /// </summary>
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
    }
}