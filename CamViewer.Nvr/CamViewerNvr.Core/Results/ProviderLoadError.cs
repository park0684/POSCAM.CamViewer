namespace CamViewer.Nvr.Core.Results
{
    /// <summary>
    /// Provider DLL 검색 또는 로드 중 발생한 오류 정보를 나타낸다.
    /// 특정 Provider 로드 실패가 CamViewer 전체 종료로 이어지지 않도록 기록에 사용한다.
    /// </summary>
    public sealed class ProviderLoadError
    {
        /// <summary>
        /// 오류가 발생한 DLL 파일 경로.
        /// </summary>
        public string AssemblyPath { get; set; }

        /// <summary>
        /// 오류가 발생한 클래스명.
        /// 클래스 확인 전 오류라면 비어 있을 수 있다.
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// 오류 설명.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 내부 예외 형식명.
        /// </summary>
        public string ExceptionType { get; set; }
    }
}