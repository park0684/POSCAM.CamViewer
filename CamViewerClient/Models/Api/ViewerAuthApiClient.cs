using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CamViewerClient.Api;
using Newtonsoft.Json;
using CamViewerClient.Models.Api;
using CamViewerClient.Results;

namespace CamViewerClient.Api
{
    /// <summary>
    /// AuthServer의 ViewerAuthController와 통신하는 API Client이다.
    ///
    /// endpoint:
    /// - POST api/viewer/login
    /// - POST api/viewer/verify-token
    /// - POST api/viewer/devices
    /// - DELETE api/viewer/devices/release
    /// </summary>
    public sealed class ViewerAuthApiClient
    {
        private readonly ApiClientOptions _options;
        private readonly JsonSerializerSettings _jsonSettings;

        /// <summary>
        /// ViewerAuthApiClient를 기본 옵션으로 초기화한다.
        /// </summary>
        public ViewerAuthApiClient()
            : this(ApiClientOptions.CreateDefault())
        {
        }

        /// <summary>
        /// ViewerAuthApiClient를 지정 옵션으로 초기화한다.
        /// </summary>
        public ViewerAuthApiClient(ApiClientOptions options)
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
        /// 캠뷰어 최초 로그인 API를 호출한다.
        /// </summary>
        public Task<ClientResult<ViewerLoginResponseDto>> LoginAsync(
            ViewerLoginRequestDto request,
            CancellationToken cancellationToken)
        {
            return SendAsync<ViewerLoginRequestDto, ViewerLoginResponseDto>(
                HttpMethod.Post,
                "api/viewer/login",
                request,
                cancellationToken);
        }

        /// <summary>
        /// 캠뷰어 토큰 실행 인증 API를 호출한다.
        /// </summary>
        public Task<ClientResult<ViewerTokenVerifyResponseDto>> VerifyTokenAsync(
            ViewerTokenVerifyRequestDto request,
            CancellationToken cancellationToken)
        {
            return SendAsync<ViewerTokenVerifyRequestDto, ViewerTokenVerifyResponseDto>(
                HttpMethod.Post,
                "api/viewer/verify-token",
                request,
                cancellationToken);
        }

        /// <summary>
        /// 캠뷰어 등록 장비 목록 조회 API를 호출한다.
        /// </summary>
        public Task<ClientResult<IList<ViewerDeviceSummaryDto>>> GetDevicesAsync(
            ViewerDeviceListRequestDto request,
            CancellationToken cancellationToken)
        {
            return SendAsync<ViewerDeviceListRequestDto, IList<ViewerDeviceSummaryDto>>(
                HttpMethod.Post,
                "api/viewer/devices",
                request,
                cancellationToken);
        }

        /// <summary>
        /// 캠뷰어 장비 등록해제 API를 호출한다.
        /// </summary>
        public Task<ClientResult<ViewerDeviceReleaseResponseDto>> ReleaseDeviceAsync(
            ViewerDeviceReleaseRequestDto request,
            CancellationToken cancellationToken)
        {
            return SendAsync<ViewerDeviceReleaseRequestDto, ViewerDeviceReleaseResponseDto>(
                HttpMethod.Delete,
                "api/viewer/devices/release",
                request,
                cancellationToken);
        }

        /// <summary>
        /// HTTP 요청을 전송하고 AuthServer ApiResponse&lt;T&gt;를 ClientResult로 변환한다.
        /// </summary>
        private async Task<ClientResult<TResponse>> SendAsync<TRequest, TResponse>(
            HttpMethod method,
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

            try
            {
                using (var httpClient = CreateHttpClient())
                using (var requestMessage = new HttpRequestMessage(
                    method,
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