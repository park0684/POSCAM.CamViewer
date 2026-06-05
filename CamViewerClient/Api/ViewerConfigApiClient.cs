using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CamViewerClient.Models.Api;
using Newtonsoft.Json;
using CamViewerClient.Results;

namespace CamViewerClient.Api
{
    /// <summary>
    /// AuthServer의 ConfigController와 통신하는 API Client이다.
    ///
    /// 현재 AuthServer 기준 endpoint:
    /// - POST api/config/version
    /// - POST api/config/latest
    /// - POST api/config/sync
    ///
    /// viewer_config.dat 파일 자체를 서버로 전송하지 않고,
    /// ViewerConfigServerDto를 요청 DTO에 담아 전송한다.
    /// </summary>
    public sealed class ViewerConfigApiClient
    {
        private readonly ApiClientOptions _options;
        private readonly JsonSerializerSettings _jsonSettings;

        /// <summary>
        /// ViewerConfigApiClient를 기본 옵션으로 초기화한다.
        /// </summary>
        public ViewerConfigApiClient()
            : this(ApiClientOptions.CreateDefault())
        {
        }

        /// <summary>
        /// ViewerConfigApiClient를 지정 옵션으로 초기화한다.
        /// </summary>
        public ViewerConfigApiClient(ApiClientOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _options = options;

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Include,
                DateTimeZoneHandling = DateTimeZoneHandling.Utc
            };
        }

        /// <summary>
        /// 서버 설정 버전을 조회한다.
        /// </summary>
        public Task<ClientResult<ConfigVersionResponseDto>> GetVersionAsync(
            ConfigVersionRequestDto request,
            CancellationToken cancellationToken)
        {
            return PostAsync<ConfigVersionRequestDto, ConfigVersionResponseDto>(
                _options.ConfigVersionEndpoint,
                request,
                cancellationToken);
        }

        /// <summary>
        /// 서버 최신 설정을 다운로드한다.
        /// </summary>
        public Task<ClientResult<ViewerConfigServerDto>> GetLatestConfigAsync(
            ConfigLatestRequestDto request,
            CancellationToken cancellationToken)
        {
            return PostAsync<ConfigLatestRequestDto, ViewerConfigServerDto>(
                _options.ConfigLatestEndpoint,
                request,
                cancellationToken);
        }

        /// <summary>
        /// 로컬 설정을 서버에 동기화한다.
        /// </summary>
        public Task<ClientResult<ConfigSyncResponseDto>> SyncConfigAsync(
            ConfigSyncRequestDto request,
            CancellationToken cancellationToken)
        {
            return PostAsync<ConfigSyncRequestDto, ConfigSyncResponseDto>(
                _options.ConfigSyncEndpoint,
                request,
                cancellationToken);
        }

        /// <summary>
        /// POST 요청을 전송하고 AuthServer ApiResponse&lt;T&gt;를 ClientResult로 변환한다.
        /// </summary>
        private async Task<ClientResult<TResponse>> PostAsync<TRequest, TResponse>(
            string relativeUrl,
            TRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseAddress))
            {
                return ClientResult<TResponse>.Fail(
                    "인증서버 기본 주소가 설정되지 않았습니다.",
                    "API_BASE_ADDRESS_REQUIRED");
            }

            if (string.IsNullOrWhiteSpace(relativeUrl))
            {
                return ClientResult<TResponse>.Fail(
                    "API 경로가 설정되지 않았습니다.",
                    "API_ENDPOINT_REQUIRED");
            }

            try
            {
                using (var httpClient = CreateHttpClient())
                using (var requestMessage = new HttpRequestMessage(
                    HttpMethod.Post,
                    relativeUrl))
                {
                    string json = JsonConvert.SerializeObject(
                        request,
                        _jsonSettings);

                    requestMessage.Content = new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json");

                    using (HttpResponseMessage response =
                        await httpClient.SendAsync(
                            requestMessage,
                            cancellationToken))
                    {
                        string responseText =
                            await response.Content.ReadAsStringAsync();

                        if (!response.IsSuccessStatusCode)
                        {
                            return ClientResult<TResponse>.Fail(
                                "인증서버 요청에 실패했습니다. HTTP "
                                + (int)response.StatusCode
                                + " "
                                + response.ReasonPhrase,
                                "HTTP_REQUEST_FAILED");
                        }

                        ApiResponseDto<TResponse> apiResponse =
                            JsonConvert.DeserializeObject<ApiResponseDto<TResponse>>(
                                responseText,
                                _jsonSettings);

                        if (apiResponse == null)
                        {
                            return ClientResult<TResponse>.Fail(
                                "인증서버 응답을 해석할 수 없습니다.",
                                "API_RESPONSE_EMPTY");
                        }

                        if (!apiResponse.Success)
                        {
                            return ClientResult<TResponse>.Fail(
                                apiResponse.Message,
                                apiResponse.ErrorCode);
                        }

                        return ClientResult<TResponse>.Ok(
                            apiResponse.Data,
                            apiResponse.Message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return ClientResult<TResponse>.Fail(
                    "인증서버 요청이 취소되었거나 시간이 초과되었습니다.",
                    "API_REQUEST_CANCELLED");
            }
            catch (Exception ex)
            {
                return ClientResult<TResponse>.Fail(
                    "인증서버 요청 중 오류가 발생했습니다. " + ex.Message,
                    "API_REQUEST_FAILED");
            }
        }

        /// <summary>
        /// HTTP Client를 생성한다.
        /// </summary>
        private HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(_options.BaseAddress),
                Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
            };

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            return httpClient;
        }
    }
}
