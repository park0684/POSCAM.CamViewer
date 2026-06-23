using System;
using System.IO;
using System.Text;

namespace CamViewer.Nvr.Dahua.Diagnostics
{
    /// <summary>
    /// Dahua Provider 내부 진단 로그를 기록한다.
    /// 계정, 비밀번호와 같은 인증 정보는 전달하지 않는다.
    /// </summary>
    internal static class DahuaLogWriter
    {
        private static readonly object SyncRoot =
            new object();

        internal static void Write(
            string level,
            string operation,
            string message)
        {
            try
            {
                string directory =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "logs");

                Directory.CreateDirectory(
                    directory);

                string path =
                    Path.Combine(
                        directory,
                        "dahua-provider.log");

                var builder =
                    new StringBuilder();

                builder.Append(
                    DateTime.Now.ToString(
                        "yyyy-MM-dd HH:mm:ss.fff"));

                builder.Append(" [");
                builder.Append(
                    string.IsNullOrWhiteSpace(level)
                        ? "INFO"
                        : level);
                builder.Append("] ");

                if (!string.IsNullOrWhiteSpace(
                        operation))
                {
                    builder.Append(operation);
                    builder.Append(" - ");
                }

                builder.AppendLine(
                    string.IsNullOrWhiteSpace(message)
                        ? "-"
                        : message);

                lock (SyncRoot)
                {
                    File.AppendAllText(
                        path,
                        builder.ToString(),
                        Encoding.UTF8);
                }
            }
            catch
            {
                /*
                 * 로그 기록 실패가 실제 SDK 처리를 중단하지 않게 한다.
                 */
            }
        }
    }
}
