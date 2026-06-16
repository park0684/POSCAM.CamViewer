using CamViewer.Models;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;

namespace CamViewer.Services
{
    /// <summary>
    /// 이미 실행 중인 CamViewer 프로세스로
    /// 실행 요청을 전송하는 Named Pipe 클라이언트이다.
    ///
    /// 주 사용 대상:
    /// - 두 번째로 실행된 CamViewer 프로세스
    /// - 외부 프로그램에서 전달된 영상 조회 요청
    ///
    /// 처리 순서:
    /// 1. 기존 CamViewer의 Named Pipe 서버에 연결
    /// 2. ApplicationLaunchRequest를 문자열로 직렬화
    /// 3. 한 줄 메시지로 요청 전송
    /// 4. 서버 처리 결과 응답 수신
    /// </summary>
    public sealed class ApplicationLaunchPipeClient
    {
        /// <summary>
        /// Pipe 서버와 클라이언트가 공통으로 사용하는 이름.
        ///
        /// 서버 이름에는 경로나 역슬래시를 포함하지 않는다.
        /// 동일한 PC 내부 통신에만 사용한다.
        /// </summary>
        public const string PipeName =
            "POSCAM.CamViewer.LaunchRequest";

        /// <summary>
        /// 기존 CamViewer 프로세스로 연결을 시도할 최대 시간.
        /// </summary>
        private const int ConnectionTimeoutMilliseconds =
            2000;

        /// <summary>
        /// 서버가 요청을 정상적으로 수신했음을 나타내는 응답값.
        /// </summary>
        private const string SuccessResponse =
            "OK";

        /// <summary>
        /// 실행 요청을 기존 CamViewer 프로세스로 전송한다.
        /// </summary>
        /// <param name="request">
        /// 기존 프로세스로 전달할 실행 요청.
        /// </param>
        /// <param name="errorMessage">
        /// 연결 또는 전송 실패 원인.
        /// </param>
        /// <returns>
        /// 서버가 요청을 정상적으로 수신했으면 true.
        /// </returns>
        public bool TrySend(
            ApplicationLaunchRequest request,
            out string errorMessage)
        {
            errorMessage = string.Empty;

            if (request == null)
            {
                errorMessage =
                    "전송할 실행 요청이 없습니다.";

                return false;
            }

            string requestMessage;

            try
            {
                /*
                 * 검증된 실행 요청 모델을
                 * Named Pipe 전송 문자열로 변환한다.
                 */
                requestMessage =
                    ApplicationLaunchRequestPipeProtocol
                        .Serialize(
                            request);
            }
            catch (Exception ex)
            {
                errorMessage =
                    "실행 요청을 전송 형식으로 변환하지 못했습니다."
                    + Environment.NewLine
                    + ex.Message;

                return false;
            }

            try
            {
                /*
                 * "."은 현재 PC의 Named Pipe 서버를 의미한다.
                 *
                 * PipeDirection.InOut을 사용하는 이유:
                 * - 클라이언트가 요청 전송
                 * - 서버가 처리 결과 응답
                 */
                using (var pipeClient =
                    new NamedPipeClientStream(
                        ".",
                        PipeName,
                        PipeDirection.InOut,
                        PipeOptions.None))
                {
                    /*
                     * 기존 프로세스가 Pipe 서버를 시작하지 않았거나
                     * 응답할 수 없는 경우 무한 대기하지 않도록
                     * 최대 2초까지만 연결을 시도한다.
                     */
                    pipeClient.Connect(
                        ConnectionTimeoutMilliseconds);

                    using (var writer =
                        new StreamWriter(
                            pipeClient,
                            new UTF8Encoding(false),
                            1024,
                            true))
                    using (var reader =
                        new StreamReader(
                            pipeClient,
                            Encoding.UTF8,
                            false,
                            1024,
                            true))
                    {
                        /*
                         * 서버는 ReadLine()으로 요청을 수신하므로
                         * 반드시 WriteLine()으로 개행까지 전송한다.
                         */
                        writer.WriteLine(
                            requestMessage);

                        writer.Flush();

                        /*
                         * 서버가 요청을 역직렬화하고 저장한 뒤
                         * OK 또는 ERROR 응답을 반환한다.
                         */
                        string response =
                            reader.ReadLine();

                        if (string.Equals(
                                response,
                                SuccessResponse,
                                StringComparison.Ordinal))
                        {
                            return true;
                        }

                        if (string.IsNullOrWhiteSpace(
                            response))
                        {
                            errorMessage =
                                "기존 CamViewer에서 응답을 받지 못했습니다.";

                            return false;
                        }

                        errorMessage =
                            "기존 CamViewer가 실행 요청을 처리하지 못했습니다."
                            + Environment.NewLine
                            + response;

                        return false;
                    }
                }
            }
            catch (TimeoutException)
            {
                errorMessage =
                    "실행 중인 CamViewer에 연결할 수 없습니다."
                    + Environment.NewLine
                    + "연결 대기 시간이 초과되었습니다.";

                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                errorMessage =
                    "CamViewer 프로세스 간 통신 권한이 없습니다."
                    + Environment.NewLine
                    + ex.Message;

                return false;
            }
            catch (IOException ex)
            {
                errorMessage =
                    "실행 중인 CamViewer로 요청을 전송하지 못했습니다."
                    + Environment.NewLine
                    + ex.Message;

                return false;
            }
            catch (Exception ex)
            {
                errorMessage =
                    "CamViewer 실행 요청 전송 중 오류가 발생했습니다."
                    + Environment.NewLine
                    + ex.Message;

                return false;
            }
        }
    }
}