namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 등록 장비 목록 조회 요청 DTO이다.
    ///
    /// AuthServer endpoint:
    /// POST api/viewer/devices
    /// </summary>
    public sealed class ViewerDeviceListRequestDto
    {
        /// <summary>
        /// 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 매장 로그인 ID.
        /// </summary>
        public string StoreId { get; set; }

        /// <summary>
        /// 매장 로그인 비밀번호.
        /// </summary>
        public string StorePassword { get; set; }
    }
}