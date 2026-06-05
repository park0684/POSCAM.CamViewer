using System;
using System.Collections.Generic;
using System.Linq;
using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Models;

namespace CamViewer.Nvr.Core.Providers
{
    /// <summary>
    /// ProviderKey 기준으로 NVR Provider 등록 정보를 관리한다.
    /// </summary>
    public sealed class NvrProviderCatalog : INvrProviderCatalog
    {
        private readonly Dictionary<string, ProviderRegistration> _registrations;

        /// <summary>
        /// Provider Catalog를 초기화한다.
        /// ProviderKey는 대소문자를 구분하지 않는다.
        /// </summary>
        public NvrProviderCatalog()
        {
            _registrations = new Dictionary<string, ProviderRegistration>(
                StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Provider 등록 정보를 추가한다.
        /// </summary>
        public bool Register(ProviderRegistration registration)
        {
            if (registration == null)
            {
                throw new ArgumentNullException("registration");
            }

            if (string.IsNullOrWhiteSpace(registration.ProviderKey))
            {
                throw new ArgumentException(
                    "ProviderKey가 비어 있습니다.",
                    "registration");
            }

            if (registration.ProviderType == null)
            {
                throw new ArgumentException(
                    "ProviderType이 비어 있습니다.",
                    "registration");
            }

            if (_registrations.ContainsKey(registration.ProviderKey))
            {
                return false;
            }

            _registrations.Add(registration.ProviderKey, registration);
            return true;
        }

        /// <summary>
        /// ProviderKey에 해당하는 등록 정보를 조회한다.
        /// </summary>
        public bool TryGet(string providerKey, out ProviderRegistration registration)
        {
            registration = null;

            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return false;
            }

            return _registrations.TryGetValue(providerKey, out registration);
        }

        /// <summary>
        /// 등록된 모든 Provider 정보를 반환한다.
        /// </summary>
        public IReadOnlyCollection<ProviderRegistration> GetAll()
        {
            return _registrations.Values
                .OrderBy(x => x.ProviderKey)
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// ProviderKey가 등록되어 있는지 확인한다.
        /// </summary>
        public bool Contains(string providerKey)
        {
            if (string.IsNullOrWhiteSpace(providerKey))
            {
                return false;
            }

            return _registrations.ContainsKey(providerKey);
        }
    }
}