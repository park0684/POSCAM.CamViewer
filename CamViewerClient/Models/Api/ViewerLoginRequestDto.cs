namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 최초 로그인 요청 DTO이다.
    ///
    /// AuthServer endpoint:
    /// POST api/viewer/login
    /// </summary>
    public sealed class ViewerLoginRequestDto
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
        /// 현재 PC의 HWID.
        /// </summary>
        public string Hwid { get; set; }

        /// <summary>
        /// 캠뷰어 장비 이름 또는 PC 이름.
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// 캠뷰어 프로그램 버전.
        /// </summary>
        public string ProgramVersion { get; set; }
    }
}