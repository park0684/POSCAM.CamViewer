using CamViewer.Models;

namespace CamViewer.Services
{
    /// <summary>
    /// 프로그램 실행 인자를 해석하여 CamViewer 실행 요청으로 변환한다.
    /// </summary>
    public interface IApplicationLaunchArgumentParser
    {
        /// <summary>
        /// 명령행 인자를 분석하여 실행 요청을 생성한다.
        /// </summary>
        /// <param name="args">Main 메서드로 전달된 명령행 인자.</param>
        /// <param name="request">분석에 성공한 실행 요청.</param>
        /// <param name="errorMessage">분석 실패 시 사용자에게 표시할 메시지.</param>
        /// <returns>실행 인자 분석에 성공하면 true.</returns>
        bool TryParse(
            string[] args,
            out ApplicationLaunchRequest request,
            out string errorMessage);
    }
}
