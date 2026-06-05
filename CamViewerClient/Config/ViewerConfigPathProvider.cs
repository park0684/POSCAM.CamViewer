using System;
using System.IO;

namespace CamViewerClient.Config
{
    /// <summary>
    /// 캠뷰어 로컬 설정 파일의 저장 경로를 제공한다.
    ///
    /// 현재 정책:
    /// - config 하위 폴더를 만들지 않는다.
    /// - 실행 파일과 동일한 위치에 viewer_config.dat 파일을 저장한다.
    /// </summary>
    public sealed class ViewerConfigPathProvider
    {
        private const string ConfigFileName = "viewer_config.dat";

        /// <summary>
        /// 로컬 설정 파일 전체 경로를 반환한다.
        /// </summary>
        public string GetConfigFilePath()
        {
            string baseDirectory =
                AppDomain.CurrentDomain.BaseDirectory;

            return Path.Combine(
                baseDirectory,
                ConfigFileName);
        }

        /// <summary>
        /// 설정 파일이 저장될 기준 폴더를 반환한다.
        /// 현재는 실행 파일이 있는 폴더이다.
        /// </summary>
        public string GetBaseDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}