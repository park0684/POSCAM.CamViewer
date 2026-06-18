using CamViewer.Models;
using System;
using System.IO;
using System.Text;

namespace CamViewer.Services
{
    /// <summary>
    /// NVR 재생 명령과 오류 정보를 일자별 파일로 기록한다.
    ///
    /// 저장 위치:
    /// 실행파일 경로\logs\playback-yyyyMMdd.log
    ///
    /// 로그 기록 실패가 실제 재생 흐름을 방해하지 않도록
    /// 내부 예외는 외부로 전달하지 않는다.
    /// </summary>
    internal static class PlaybackLogWriter
    {
        private static readonly object SyncRoot =
            new object();

        /// <summary>
        /// 재생 처리 결과를 기록한다.
        /// </summary>
        /// <param name="operationName">
        /// 재생 시작, 역재생, 위치 이동, 동기화 등의 작업명.
        /// </param>
        /// <param name="result">
        /// 재생 서비스 처리 결과.
        /// </param>
        /// <param name="details">
        /// NVR번호, 채널번호, ProviderKey 등의 추가 진단 정보.
        /// 비밀번호와 토큰은 포함하지 않는다.
        /// </param>
        public static void WriteResult(
            string operationName,
            PlayerPlaybackResult result,
            string details = null)
        {
            if (result == null)
            {
                Write(
                    "ERROR",
                    operationName,
                    "NVR_RESULT_EMPTY",
                    PlaybackFailureCategory.System.ToString(),
                    "재생 처리 결과가 없습니다.",
                    details);

                return;
            }

            Write(
                result.Success
                    ? "INFO"
                    : "ERROR",
                operationName,
                result.ErrorCode,
                result.FailureCategory.ToString(),
                result.Message,
                details);
        }

        /// <summary>
        /// 재생 처리 중 발생한 예외를 기록한다.
        /// </summary>
        public static void WriteException(
            string operationName,
            Exception exception,
            string details = null)
        {
            string message =
                exception == null
                    ? "예외 정보가 없습니다."
                    : exception.ToString();

            Write(
                "ERROR",
                operationName,
                "UNHANDLED_EXCEPTION",
                PlaybackFailureCategory.System.ToString(),
                message,
                details);
        }

        /// <summary>
        /// 로그 한 줄을 일자별 파일에 기록한다.
        /// </summary>
        private static void Write(
            string level,
            string operationName,
            string errorCode,
            string category,
            string message,
            string details)
        {
            try
            {
                string baseDirectory =
                    AppDomain.CurrentDomain.BaseDirectory;

                string logDirectory =
                    Path.Combine(
                        baseDirectory,
                        "logs");

                Directory.CreateDirectory(
                    logDirectory);

                string logFilePath =
                    Path.Combine(
                        logDirectory,
                        "playback-"
                        + DateTime.Now.ToString("yyyyMMdd")
                        + ".log");

                string logLine =
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss.fff")
                    + " | "
                    + Normalize(level)
                    + " | Operation="
                    + Normalize(operationName)
                    + " | Category="
                    + Normalize(category)
                    + " | ErrorCode="
                    + Normalize(errorCode)
                    + " | Message="
                    + Normalize(message)
                    + (
                        string.IsNullOrWhiteSpace(details)
                            ? string.Empty
                            : " | Details="
                              + Normalize(details)
                    )
                    + Environment.NewLine;

                /*
                 * 재생 타이머와 UI 명령에서 동시에 로그를 작성할 수 있으므로
                 * 파일 쓰기는 하나씩 실행한다.
                 */
                lock (SyncRoot)
                {
                    File.AppendAllText(
                        logFilePath,
                        logLine,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                /*
                 * 로그 폴더 생성 또는 파일 쓰기 실패가
                 * NVR 재생 기능까지 중단시키면 안 된다.
                 */
            }
        }

        /// <summary>
        /// 여러 줄 문자열을 한 줄 로그 형식으로 정리한다.
        /// </summary>
        private static string Normalize(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            return value
                .Replace(
                    "\r\n",
                    " / ")
                .Replace(
                    "\r",
                    " / ")
                .Replace(
                    "\n",
                    " / ")
                .Trim();
        }
    }
}