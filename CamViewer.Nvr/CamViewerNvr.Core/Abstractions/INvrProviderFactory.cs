using CamViewer.Nvr.Core.Results;

namespace CamViewer.Nvr.Core.Abstractions
{
    /// <summary>
    /// ProviderKey를 기준으로 새로운 NVR Provider 인스턴스를 생성하는 Factory 인터페이스.
    /// </summary>
    public interface INvrProviderFactory
    {
        /// <summary>
        /// ProviderKey에 해당하는 새로운 Provider 인스턴스를 생성한다.
        /// </summary>
        /// <param name="providerKey">생성할 Provider의 고유 키.</param>
        /// <returns>Provider 생성 결과.</returns>
        NvrResult<INvrProvider> Create(string providerKey);
    }
}