using System.Collections.Generic;
using CamViewer.Nvr.Core.Models;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// 검색된 NVR Provider 등록 정보를 관리하는 Catalog 인터페이스.
    /// </summary>
    public interface INvrProviderCatalog
    {
        /// <summary>
        /// Provider 등록 정보를 추가한다.
        /// ProviderKey가 중복되면 등록하지 않는다.
        /// </summary>
        /// <param name="registration">추가할 Provider 등록 정보.</param>
        /// <returns>등록 성공 여부.</returns>
        bool Register(ProviderRegistration registration);

        /// <summary>
        /// ProviderKey에 해당하는 등록 정보를 조회한다.
        /// </summary>
        /// <param name="providerKey">조회할 ProviderKey.</param>
        /// <param name="registration">조회된 Provider 등록 정보.</param>
        /// <returns>등록 정보 존재 여부.</returns>
        bool TryGet(string providerKey, out ProviderRegistration registration);

        /// <summary>
        /// 등록된 모든 Provider 정보를 반환한다.
        /// </summary>
        IReadOnlyCollection<ProviderRegistration> GetAll();

        /// <summary>
        /// ProviderKey가 등록되어 있는지 확인한다.
        /// </summary>
        bool Contains(string providerKey);
    }
}