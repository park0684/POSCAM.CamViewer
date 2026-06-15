using System;

namespace CamViewer.Models
{
    /// <summary>
    /// CamViewer 최초 실행 또는 기존 프로세스로 전달되는 실행 요청이다.
    /// </summary>
    public sealed class ApplicationLaunchRequest
    {
        /// <summary>
        /// 실행 요청 유형.
        /// </summary>
        public ApplicationLaunchRequestType RequestType { get; set; }

        /// <summary>
        /// 외부 프로그램이 전달한 영상 조회 기준 시각.
        /// 직접 실행 요청에서는 null이며,
        /// Player 준비 시점에 현재 시각을 사용한다.
        /// </summary>
        public DateTime? ReferenceTime { get; set; }

        /// <summary>
        /// 외부 프로그램이 지정한 계산대 번호.
        /// 값이 없으면 현재 선택값 또는 기본 계산대를 사용한다.
        /// </summary>
        public int? CounterNo { get; set; }

        /// <summary>
        /// 외부 영상 조회 요청인지 확인한다.
        /// </summary>
        public bool IsExternalPlaybackRequest
        {
            get
            {
                return RequestType ==
                    ApplicationLaunchRequestType.ExternalPlayback;
            }
        }

        /// <summary>
        /// 직접 실행 요청을 생성한다.
        /// 현재 시각은 이 시점에 고정하지 않고
        /// Player 화면이 준비된 뒤 결정한다.
        /// </summary>
        public static ApplicationLaunchRequest CreateDirectLaunch()
        {
            return new ApplicationLaunchRequest
            {
                RequestType =
                    ApplicationLaunchRequestType.DirectLaunch,
                ReferenceTime = null,
                CounterNo = null
            };
        }

        /// <summary>
        /// 외부 영상 조회 요청을 생성한다.
        /// </summary>
        /// <param name="referenceTime">영상 조회 기준 시각.</param>
        /// <param name="counterNo">조회할 계산대 번호.</param>
        public static ApplicationLaunchRequest CreateExternalPlayback(
            DateTime referenceTime,
            int? counterNo)
        {
            return new ApplicationLaunchRequest
            {
                RequestType =
                    ApplicationLaunchRequestType.ExternalPlayback,
                ReferenceTime = referenceTime,
                CounterNo = counterNo
            };
        }
    }
}