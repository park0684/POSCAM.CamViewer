namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 장비 등록해제 요청 DTO이다.
    ///
    /// AuthServer endpoint:
    /// DELETE api/viewer/devices/release
    /// </summary>
    public sealed class ViewerDeviceReleaseRequestDto
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

        /// <summary>
        /// 해제할 캠뷰어 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 해제 사유.
        /// </summary>
        public string Reason { get; set; }
    }
}