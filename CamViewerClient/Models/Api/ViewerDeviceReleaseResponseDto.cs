namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 장비 등록해제 응답 DTO이다.
    /// </summary>
    public sealed class ViewerDeviceReleaseResponseDto
    {
        /// <summary>
        /// 해제된 장비 코드.
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 해제 성공 여부.
        /// </summary>
        public bool Released { get; set; }
    }
}