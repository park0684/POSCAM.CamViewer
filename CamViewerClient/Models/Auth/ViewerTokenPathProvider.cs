using System;
using System.IO;

namespace CamViewerClient.Models.Auth
{
    /// <summary>
    /// 캠뷰어 로컬 인증 토큰 파일 경로를 제공한다.
    ///
    /// 현재 정책:
    /// - 별도 하위 폴더를 만들지 않는다.
    /// - 실행 파일과 동일한 위치에 viewer_token.dat 파일을 저장한다.
    /// </summary>
    public sealed class ViewerTokenPathProvider
    {
        private const string TokenFileName = "viewer_token.dat";

        /// <summary>
        /// 로컬 인증 토큰 파일 전체 경로를 반환한다.
        /// </summary>
        public string GetTokenFilePath()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                TokenFileName);
        }
    }
}