using System;

namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 등록 장비 목록 표시용 DTO이다.
    ///
    /// AuthServer의 DeviceSummary 속성과 다르면
    /// 서버 응답 DTO에 맞춰 속성명을 조정한다.
    /// </summary>
    public sealed class ViewerDeviceSummaryDto
    {
        /// <summary>
        /// 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 장비명.
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 장비 HWID.
        /// </summary>
        public string Hwid { get; set; }

        /// <summary>
        /// 장비 등록일시.
        /// </summary>
        public DateTime? CreatedAt { get; set; }

        /// <summary>
        /// 장비 수정일시.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }
}