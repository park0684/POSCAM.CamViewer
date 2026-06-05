using System;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Providers
{
    /// <summary>
    /// ProviderKey를 기준으로 새로운 NVR Provider 인스턴스를 생성한다.
    /// </summary>
    public sealed class NvrProviderFactory : INvrProviderFactory
    {
        private readonly INvrProviderCatalog _catalog;

        /// <summary>
        /// Provider Factory를 초기화한다.
        /// </summary>
        public NvrProviderFactory(INvrProviderCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            _catalog = catalog;
        }

        /// <summary>
        /// ProviderKey에 해당하는 새로운 Provider 인스턴스를 생성한다.
        /// </summary>
        public NvrResult<INvrProvider> Create(string providerKey)
        {
            ProviderRegistration registration;

            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.ProviderNotFound,
                    "ProviderKey가 비어 있습니다.");
            }

            if (!_catalog.TryGet(providerKey, out registration))
            {
                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.ProviderNotFound,
                    "등록된 NVR Provider를 찾을 수 없습니다. ProviderKey: " + providerKey);
            }

            try
            {
                var instance = Activator.CreateInstance(registration.ProviderType) as INvrProvider;

                if (instance == null)
                {
                    return NvrResult<INvrProvider>.Fail(
                        NvrResultStatus.ProviderNotFound,
                        "Provider 인스턴스를 생성할 수 없습니다. ProviderKey: " + providerKey);
                }

                return NvrResult<INvrProvider>.Ok(instance);
            }
            catch (Exception ex)
            {
                return NvrResult<INvrProvider>.Fail(
                    NvrResultStatus.ProviderNotFound,
                    "Provider 인스턴스 생성 중 오류가 발생했습니다. ProviderKey: " + providerKey,
                    new NvrErrorInfo
                    {
                        ErrorCode = "PROVIDER_CREATE_FAILED",
                        ErrorMessage = ex.Message,
                        Operation = "CreateProvider"
                    });
            }
        }
    }
}