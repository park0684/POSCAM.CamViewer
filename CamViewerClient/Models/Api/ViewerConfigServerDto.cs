using System;
using System.Collections.Generic;

namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 인증서버와 주고받는 캠뷰어 설정 전체 DTO이다.
    ///
    /// 이 DTO는 서버 업로드/다운로드용이며,
    /// 로컬 viewer_config.dat 암호화 파일 자체를 서버로 전송하지 않는다.
    /// </summary>
    public sealed class ViewerConfigServerDto
    {
        /// <summary>
        /// 설정이 적용되는 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 서버 설정 버전.
        /// 서버에서 설정이 변경될 때 증가하는 값이다.
        /// </summary>
        public long ConfigVersion { get; set; }

        /// <summary>
        /// 서버 기준 설정 수정 UTC 일시.
        /// </summary>
        public DateTime? ServerUpdatedAtUtc { get; set; }

        /// <summary>
        /// NVR 설정 목록.
        /// </summary>
        public IList<NvrConfigServerDto> NvrList { get; set; }

        /// <summary>
        /// 계산대 채널 매핑 목록.
        /// </summary>
        public IList<CounterMapServerDto> CounterMapList { get; set; }

        /// <summary>
        /// 재생 시간 계산 옵션.
        /// </summary>
        public PlaybackOptionServerDto PlaybackOption { get; set; }

        /// <summary>
        /// 서버 설정 DTO를 초기화한다.
        /// </summary>
        public ViewerConfigServerDto()
        {
            NvrList = new List<NvrConfigServerDto>();
            CounterMapList = new List<CounterMapServerDto>();
            PlaybackOption = new PlaybackOptionServerDto();
        }
    }
}
