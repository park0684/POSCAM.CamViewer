using System;
using System.IO;
using System.Text;


namespace CamViewer.Infrastructure
{
    /// <summary>
    /// NVR Provider DLL 검색, 등록, Factory 생성이 정상 동작하는지 확인하기 위한 임시 테스트 클래스.
    ///
    /// Provider 구조 검증이 완료되면 제거하거나 별도 테스트 프로젝트로 이동한다.
    /// </summary>
    public static class ProviderLoadSmokeTest
    {
        /// <summary>
        /// Provider 폴더를 검색하고 DAHUA_SDK Provider 생성 결과를 문자열로 반환한다.
        /// </summary>
        //public static string Run()
        //{
        //    var providerRootPath = Path.Combine(
        //        AppDomain.CurrentDomain.BaseDirectory,
        //        "providers");

        //    var catalog = new NvrProviderCatalog();
        //    var loader = new ProviderAssemblyLoader();
        //    var bootstrapper = new NvrProviderBootstrapper(loader, catalog);
        //    var factory = new NvrProviderFactory(catalog);

        //    var report = bootstrapper.LoadAndRegister(providerRootPath);

        //    var builder = new StringBuilder();

        //    builder.AppendLine("Provider 검색 경로");
        //    builder.AppendLine(providerRootPath);
        //    builder.AppendLine();

        //    builder.AppendLine("검색된 Provider 수: " + report.LoadedCount);
        //    builder.AppendLine("로드 오류 수: " + report.ErrorCount);
        //    builder.AppendLine();

        //    foreach (var registration in catalog.GetAll())
        //    {
        //        builder.AppendLine(
        //            string.Format(
        //                "등록 Provider: {0} / {1} / {2}",
        //                registration.ProviderKey,
        //                registration.DisplayName,
        //                registration.ProviderType.FullName));
        //    }

        //    foreach (var error in report.Errors)
        //    {
        //        builder.AppendLine();
        //        builder.AppendLine("로드 오류");
        //        builder.AppendLine("파일: " + error.AssemblyPath);
        //        builder.AppendLine("형식: " + error.TypeName);
        //        builder.AppendLine("메시지: " + error.Message);
        //    }

        //    builder.AppendLine();

        //    var createResult = factory.Create("DAHUA_SDK");

        //    builder.AppendLine("DAHUA_SDK 생성 성공: " + createResult.Success);
        //    builder.AppendLine("생성 결과 상태: " + createResult.Status);
        //    builder.AppendLine("생성 결과 메시지: " + createResult.Message);

        //    if (createResult.Success && createResult.Data != null)
        //    {
        //        INvrProvider provider = createResult.Data;

        //        builder.AppendLine("ProviderKey: " + provider.Metadata.ProviderKey);
        //        builder.AppendLine("DisplayName: " + provider.Metadata.DisplayName);
        //        builder.AppendLine("Vendor: " + provider.Metadata.Vendor);
        //        builder.AppendLine("RenderMode: " + provider.Metadata.RenderMode);
        //        builder.AppendLine("Architecture: " + provider.Metadata.RequiredArchitecture);

        //        var initializeResult = provider.Initialize();

        //        builder.AppendLine("Initialize 성공: " + initializeResult.Success);
        //        builder.AppendLine("Initialize 메시지: " + initializeResult.Message);

        //        provider.Dispose();
        //    }

        //    return builder.ToString();
        //}
    }
}