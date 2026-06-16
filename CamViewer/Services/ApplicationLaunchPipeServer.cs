using CamViewer.Models;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Services
{
    /// <summary>
    /// 실행 중인 CamViewer 프로세스에서 외부 실행 요청을 기다리는
    /// Named Pipe 서버이다.
    ///
    /// 처리 순서:
    /// 1. 백그라운드 작업에서 Named Pipe 연결 대기
    /// 2. 두 번째 CamViewer 프로세스가 보낸 메시지 수신
    /// 3. 메시지를 ApplicationLaunchRequest로 변환
    /// 4. RequestReceived 이벤트 발생
    /// 5. 클라이언트에 OK 또는 ERROR 응답 반환
    ///
    /// 주의:
    /// Request CamViewer 프로세스가 보낸 메시지 수신
    /// 3. 메시지를 ApplicationLaunchRequest로 변환
    /// 4. RequestReceived 이벤트 발생
    /// 5.Received 이벤트는 UI 스레드가 아닌
    /// Pipe 수신 백그라운드 스레드에서 발생한다.
    /// 화면 접근은 이후 연결 단계에서 UI 스레드로 전환해야 한다.
    /// </summary>
    public sealed class ApplicationLaunchPipeServer
        : IDisposable
    {
        /// <summary>
        /// 요청을 정상적으로 수신했음을 나타내는 응답값.
        /// </summary>
        private const string SuccessResponse =
            "OK";

        /// <summary>
        /// 요청 처리 실패 응답의 접두사.
        /// </summary>
        private const string ErrorResponsePrefix =
            "ERROR|";

        /// <summary>
        /// 서버 시작·종료 및 활성 Pipe 접근을 보호한다.
        /// </summary>
        private readonly object _syncRoot =
            new object();

        private CancellationTokenSource
            _cancellationTokenSource;

        private Task _listenTask;

        private NamedPipeServerStream
            _activeServer;

        private bool _disposed;

        /// <summary>
        /// 새로운 CamViewer 실행 요청을 정상적으로 수신하면 발생한다.
        ///
        /// 이 이벤트는 Pipe 수신 백그라운드 스레드에서 발생한다.
        /// 이벤트 처리기에서 WinForms 컨트롤에 직접 접근하면 안 된다.
        /// </summary>
        public event Action<ApplicationLaunchRequest>
            RequestReceived;

        /// <summary>
        /// Pipe 서버가 현재 실행 중인지 확인한다.
        /// </summary>
        public bool IsRunning
        {
            get
            {
                lock (_syncRoot)
                {
                    return _listenTask != null
                        && !_listenTask.IsCompleted;
                }
            }
        }

        /// <summary>
        /// Named Pipe 요청 수신을 시작한다.
        ///
        /// 이미 실행 중이면 서버를 중복으로 시작하지 않는다.
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();

            lock (_syncRoot)
            {
                if (_listenTask != null
                    && !_listenTask.IsCompleted)
                {
                    return;
                }

                _cancellationTokenSource =
                    new CancellationTokenSource();

                CancellationToken cancellationToken =
                    _cancellationTokenSource.Token;

                /*
                 * WaitForConnection()은 클라이언트가 연결할 때까지 대기한다.
                 *
                 * UI 스레드에서 실행하면 화면이 멈추므로
                 * 별도의 백그라운드 작업에서 수신 반복문을 실행한다.
                 */
                _listenTask =
                    Task.Run(
                        () => ListenLoop(
                            cancellationToken));
            }
        }

        /// <summary>
        /// Named Pipe 요청 수신을 중지한다.
        ///
        /// 현재 연결 대기 중인 Pipe도 함께 종료한다.
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource
                cancellationTokenSource;

            Task listenTask;

            NamedPipeServerStream activeServer;

            lock (_syncRoot)
            {
                cancellationTokenSource =
                    _cancellationTokenSource;

                listenTask =
                    _listenTask;

                activeServer =
                    _activeServer;

                _cancellationTokenSource = null;
                _listenTask = null;
                _activeServer = null;
            }

            if (cancellationTokenSource == null)
            {
                return;
            }

            /*
             * 수신 반복문에 종료 요청을 전달한다.
             */
            cancellationTokenSource.Cancel();

            /*
             * WaitForConnection()은 CancellationToken만으로
             * 즉시 종료되지 않을 수 있다.
             *
             * 현재 활성 Pipe를 Dispose하여 연결 대기를 해제한다.
             */
            if (activeServer != null)
            {
                try
                {
                    activeServer.Dispose();
                }
                catch
                {
                    /*
                     * 프로그램 종료 중 Pipe 정리 실패는
                     * 애플리케이션 종료를 막지 않는다.
                     */
                }
            }

            if (listenTask != null)
            {
                try
                {
                    /*
                     * 백그라운드 수신 작업이 종료될 시간을 기다린다.
                     *
                     * 종료가 지연되어도 프로그램이 무한 대기하지 않도록
                     * 최대 2초까지만 기다린다.
                     */
                    listenTask.Wait(
                        2000);
                }
                catch (AggregateException)
                {
                    /*
                     * Pipe Dispose로 인해 WaitForConnection()이 종료될 때
                     * 백그라운드 작업에 예외가 기록될 수 있다.
                     *
                     * 서버 종료 과정이므로 해당 예외는 무시한다.
                     */
                }
            }

            cancellationTokenSource.Dispose();
        }

        /// <summary>
        /// 클라이언트 연결을 반복해서 기다린다.
        ///
        /// 한 요청 처리가 끝날 때마다 새 Pipe 인스턴스를 만들어
        /// 다음 요청을 수신한다.
        /// </summary>
        private void ListenLoop(
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                NamedPipeServerStream pipeServer =
                    null;

                try
                {
                    pipeServer =
                        new NamedPipeServerStream(
                            ApplicationLaunchPipeClient.PipeName,
                            PipeDirection.InOut,
                            1,
                            PipeTransmissionMode.Byte,
                            PipeOptions.Asynchronous);

                    lock (_syncRoot)
                    {
                        if (cancellationToken
                            .IsCancellationRequested)
                        {
                            pipeServer.Dispose();
                            return;
                        }

                        _activeServer =
                            pipeServer;
                    }

                    /*
                     * 두 번째 CamViewer 프로세스가 연결할 때까지
                     * 백그라운드 스레드에서 기다린다.
                     */
                    pipeServer.WaitForConnection();

                    if (cancellationToken
                        .IsCancellationRequested)
                    {
                        return;
                    }

                    ProcessConnectedClient(
                        pipeServer);
                }
                catch (ObjectDisposedException)
                {
                    /*
                     * Stop()에서 Pipe를 Dispose한 경우
                     * 정상적으로 발생할 수 있다.
                     */
                    if (cancellationToken
                        .IsCancellationRequested)
                    {
                        return;
                    }
                }
                catch (IOException)
                {
                    /*
                     * 클라이언트가 연결 과정에서 종료되었거나
                     * Pipe 연결이 끊어진 경우 다음 요청을 기다린다.
                     */
                    if (cancellationToken
                        .IsCancellationRequested)
                    {
                        return;
                    }
                }
                catch
                {
                    /*
                     * 한 번의 요청 처리 실패로 Pipe 서버 전체가
                     * 종료되지 않도록 다음 연결 대기를 계속한다.
                     *
                     * 상세 오류 로그는 이후 안정성 개선 작업에서 추가한다.
                     */
                    if (cancellationToken
                        .IsCancellationRequested)
                    {
                        return;
                    }
                }
                finally
                {
                    lock (_syncRoot)
                    {
                        if (ReferenceEquals(
                            _activeServer,
                            pipeServer))
                        {
                            _activeServer = null;
                        }
                    }

                    if (pipeServer != null)
                    {
                        try
                        {
                            pipeServer.Dispose();
                        }
                        catch
                        {
                            /*
                             * 현재 Pipe 정리 실패가 다음 요청 수신을
                             * 막지 않도록 예외를 무시한다.
                             */
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 연결된 클라이언트로부터 실행 요청 한 건을 읽고 처리한다.
        /// </summary>
        /// <param name="pipeServer">
        /// 클라이언트와 연결된 Named Pipe.
        /// </param>
        private void ProcessConnectedClient(
            NamedPipeServerStream pipeServer)
        {
            using (var reader =
                new StreamReader(
                    pipeServer,
                    Encoding.UTF8,
                    false,
                    1024,
                    true))
            using (var writer =
                new StreamWriter(
                    pipeServer,
                    new UTF8Encoding(false),
                    1024,
                    true))
            {
                writer.AutoFlush = true;

                /*
                 * 클라이언트가 WriteLine()으로 보낸
                 * 한 줄의 실행 요청 메시지를 읽는다.
                 */
                string requestMessage =
                    reader.ReadLine();

                ApplicationLaunchRequest request;
                string parseErrorMessage;

                bool parseSuccess =
                    ApplicationLaunchRequestPipeProtocol
                        .TryDeserialize(
                            requestMessage,
                            out request,
                            out parseErrorMessage);

                if (!parseSuccess)
                {
                    WriteErrorResponse(
                        writer,
                        parseErrorMessage);

                    return;
                }

                Action<ApplicationLaunchRequest>
                    requestReceivedHandler =
                        RequestReceived;

                /*
                 * 요청을 받을 이벤트 처리기가 준비되지 않았다면
                 * 클라이언트에 성공 응답을 보내면 안 된다.
                 */
                if (requestReceivedHandler == null)
                {
                    WriteErrorResponse(
                        writer,
                        "실행 요청을 처리할 수신기가 준비되지 않았습니다.");

                    return;
                }

                try
                {
                    /*
                     * 주의:
                     * 이 이벤트는 Pipe 수신 백그라운드 스레드에서 발생한다.
                     *
                     * 이벤트 처리기는 여기서 WinForms 컨트롤에 직접 접근하지 않고
                     * 요청 저장소에 보관하거나 UI 스레드로 전환해야 한다.
                     */
                    requestReceivedHandler(
                        request);

                    writer.WriteLine(
                        SuccessResponse);
                }
                catch (Exception ex)
                {
                    WriteErrorResponse(
                        writer,
                        "실행 요청 처리 중 오류가 발생했습니다. "
                        + ex.Message);
                }
            }
        }

        /// <summary>
        /// 클라이언트가 ReadLine()으로 읽을 수 있도록
        /// 한 줄의 오류 응답을 전송한다.
        /// </summary>
        private static void WriteErrorResponse(
            StreamWriter writer,
            string errorMessage)
        {
            string safeMessage;

            if (string.IsNullOrWhiteSpace(
                errorMessage))
            {
                safeMessage =
                    "알 수 없는 오류";
            }
            else
            {
                /*
                 * Pipe 응답은 한 줄 형식이므로
                 * 오류 메시지의 줄바꿈을 공백으로 변환한다.
                 */
                safeMessage =
                    errorMessage
                        .Replace(
                            "\r",
                            " ")
                        .Replace(
                            "\n",
                            " ");
            }

            writer.WriteLine(
                ErrorResponsePrefix
                + safeMessage);
        }

        /// <summary>
        /// 객체가 이미 해제되었는지 확인한다.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    "ApplicationLaunchPipeServer");
            }
        }

        /// <summary>
        /// Pipe 서버와 백그라운드 수신 작업을 종료한다.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();

            _disposed = true;
        }
    }
}