using System;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 제조사 재생 엔진이 생성한 다중채널 재생 그룹.
    ///
    /// 실제 SDK 핸들, API 세션, 디코더 및 채널별 상태는
    /// 제조사 프로젝트 내부에 숨긴다.
    /// </summary>
    public interface INvrPlaybackGroupSession
    {
        /// <summary>
        /// 재생 그룹 고유 식별값.
        /// </summary>
        string SessionId { get; }

        /// <summary>
        /// 재생 그룹을 생성한 ProviderKey.
        /// </summary>
        string ProviderKey { get; }

        /// <summary>
        /// 재생 대상 NVR번호.
        /// </summary>
        int NvrNo { get; }

        /// <summary>
        /// 그룹에 포함된 채널 수.
        /// </summary>
        int ChannelCount { get; }

        /// <summary>
        /// 조회 가능한 공통 시작 시각.
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// 조회 가능한 공통 종료 시각.
        /// </summary>
        DateTime EndTime { get; }
    }
}