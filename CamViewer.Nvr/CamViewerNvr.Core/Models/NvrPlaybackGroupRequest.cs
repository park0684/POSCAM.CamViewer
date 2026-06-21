using CamViewer.Nvr.Core.Enums;
using System;
using System.Collections.Generic;

namespace CamViewer.Nvr.Core.Models
{
    /// <summary>
    /// 하나의 제조사 재생 엔진에 전달할
    /// 다중채널 재생 그룹 요청 정보.
    /// </summary>
    public sealed class NvrPlaybackGroupRequest
    {
        /// <summary>
        /// 요청 객체를 초기화한다.
        /// </summary>
        public NvrPlaybackGroupRequest()
        {
            Channels =
                new List<NvrPlaybackGroupChannelRequest>();

            InitialDirection =
                NvrPlaybackDirection.Forward;

            InitialSpeed =
                NvrPlaybackSpeed.Normal;
        }

        /// <summary>
        /// 조회 대상 계산대번호.
        /// </summary>
        public int CounterNo { get; set; }

        /// <summary>
        /// 조회 대상 NVR번호.
        ///
        /// 하나의 그룹에는 동일한 Provider 인스턴스에 연결된
        /// 동일 NVR의 채널만 포함하는 것을 기본으로 한다.
        /// </summary>
        public int NvrNo { get; set; }

        /// <summary>
        /// ProviderKey.
        /// </summary>
        public string ProviderKey { get; set; }

        /// <summary>
        /// 사용자 또는 외부 POS에서 전달된 기준 시각.
        /// </summary>
        public DateTime SearchDateTime { get; set; }

        /// <summary>
        /// 조회 가능한 공통 시작 시각.
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 조회 가능한 공통 종료 시각.
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 그룹을 처음 준비할 영상 시각.
        /// </summary>
        public DateTime InitialTime { get; set; }

        /// <summary>
        /// 최초 재생 방향.
        /// </summary>
        public NvrPlaybackDirection InitialDirection { get; set; }

        /// <summary>
        /// 최초 재생속도.
        /// </summary>
        public NvrPlaybackSpeed InitialSpeed { get; set; }

        /// <summary>
        /// 그룹에 포함할 채널 목록.
        /// </summary>
        public IList<NvrPlaybackGroupChannelRequest> Channels { get; private set; }
    }
}