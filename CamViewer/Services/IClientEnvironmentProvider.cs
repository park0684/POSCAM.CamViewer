namespace CamViewer.Services
{
    /// <summary>
    /// 캠뷰어 실행 PC의 식별정보와 프로그램 정보를 제공한다.
    /// </summary>
    public interface IClientEnvironmentProvider
    {
        /// <summary>
        /// 현재 PC의 HWID를 반환한다.
        /// </summary>
        string GetHwid();

        /// <summary>
        /// 현재 PC 또는 장비 표시명을 반환한다.
        /// </summary>
        string GetDeviceName();

        /// <summary>
        /// 현재 캠뷰어 프로그램 버전을 반환한다.
        /// </summary>
        string GetProgramVersion();
    }
}