using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Dahua.Sdk;
using System;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// Dahua 다중채널 재생 그룹에 포함된
    /// 하나의 채널 정보를 나타낸다.
    ///
    /// 공통 프로젝트에는 DahuaPlaybackSession이나
    /// 네이티브 재생 핸들을 노출하지 않는다.
    /// </summary>
    internal sealed class DahuaPlaybackGroupChannel
    {
        /// <summary>
        /// 그룹 채널을 초기화한다.
        /// </summary>
        public DahuaPlaybackGroupChannel(
            NvrPlaybackGroupChannelRequest request,
            DahuaPlaybackSession session)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "request");
            }

            if (session == null)
            {
                throw new ArgumentNullException(
                    "session");
            }

            ChannelNo =
                request.ChannelNo;

            ScreenPosition =
                request.ScreenPosition;

            RenderTargetHandle =
                request.RenderTargetHandle;

            TimeOffsetSeconds =
                request.TimeOffsetSeconds;

            Session =
                session;
        }

        /// <summary>
        /// NVR 채널번호.
        /// </summary>
        public int ChannelNo { get; private set; }

        /// <summary>
        /// 화면 위치.
        /// 0 = 좌측, 1 = 우측.
        /// </summary>
        public int ScreenPosition { get; private set; }

        /// <summary>
        /// Dahua SDK가 영상을 출력할 Windows Handle.
        /// </summary>
        public IntPtr RenderTargetHandle { get; private set; }

        /// <summary>
        /// 해당 채널의 영상시간 보정값.
        ///
        /// 공통 CamViewer 서비스는 이 값을 적용하지 않고,
        /// Dahua 재생 엔진이 직접 처리한다.
        /// </summary>
        public int TimeOffsetSeconds { get; private set; }

        /// <summary>
        /// 현재 채널의 실제 Dahua 재생 세션.
        /// </summary>
        public DahuaPlaybackSession Session { get; private set; }

        /// <summary>
        /// CamViewer 공통 영상 시각을
        /// 해당 Dahua 채널에 전달할 원본 시각으로 변환한다.
        /// </summary>
        public DateTime ToProviderTime(
            DateTime commonPlaybackTime)
        {
            return commonPlaybackTime.AddSeconds(
                TimeOffsetSeconds);
        }

        /// <summary>
        /// Dahua SDK에서 읽은 채널 원본 시각을
        /// CamViewer 공통 영상 시각으로 변환한다.
        /// </summary>
        public DateTime ToCommonTime(
            DateTime providerPlaybackTime)
        {
            return providerPlaybackTime.AddSeconds(
                -TimeOffsetSeconds);
        }

        /// <summary>
        /// 방향 전환이나 세션 재생성으로
        /// 현재 채널의 Dahua 세션을 교체한다.
        ///
        /// 기존 세션의 중지와 Dispose는 이 메서드를 호출하는
        /// DahuaPlaybackEngine이 먼저 처리해야 한다.
        /// </summary>
        public void ReplaceSession(
            DahuaPlaybackSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(
                    "session");
            }

            Session =
                session;
        }
    }
}