using System;
using System.Collections.Generic;

namespace CamViewer.Models
{
    /// <summary>
    /// PlayerView에서 생성하는 영상 재생 요청 정보이다.
    ///
    /// 이 모델은 특정 NVR 제조사 SDK에 종속되지 않는다.
    /// 이후 POSCAM.Nvr.Core 또는 Provider 공통 재생 요청 모델로 변환해서 사용한다.
    /// </summary>
    public sealed class PlayerPlaybackRequest
    {
        /// <summary>
        /// 선택된 계산대번호.
        /// </summary>
        public int CounterNo { get; set; }

        /// <summary>
        /// 사용자가 선택한 영상검색일시.
        /// 현재 정책상 거래완료 시각에 해당한다.
        /// </summary>
        public DateTime SearchDateTime { get; set; }

        /// <summary>
        /// 실제 재생 시작 시각.
        /// 영상검색일시 - 검색시간조정초.
        /// </summary>
        public DateTime PlayStartTime { get; set; }

        /// <summary>
        /// 실제 재생 종료 시각.
        /// 영상검색일시 + 거래완료 후 보정초.
        /// </summary>
        public DateTime PlayEndTime { get; set; }

        /// <summary>
        /// 좌측/우측 재생 대상 채널 목록.
        /// </summary>
        public List<PlayerChannelTarget> Channels { get; private set; }

        /// <summary>
        /// 재생 요청 모델을 초기화한다.
        /// </summary>
        public PlayerPlaybackRequest()
        {
            Channels = new List<PlayerChannelTarget>();
        }
    }
}