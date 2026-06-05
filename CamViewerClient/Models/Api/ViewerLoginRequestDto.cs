namespace CamViewerClient.Models.Api
{
    /// <summary>
    /// 캠뷰어 최초 로그인 요청 DTO이다.
    ///
    /// 사용자는 화면에서 매장코드와 비밀번호만 입력한다.
    /// 화면의 매장코드는 서버 요청에서 StoreId로 전달한다.
    /// StoreCode는 로그인 성공 후 서버 응답으로 받는다.
    /// </summary>
    public sealed class ViewerLoginRequestDto
    {
        /// <summary>
        /// 화면에서 입력한 매장코드.
        /// 서버에는 StoreId로 전달한다.
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