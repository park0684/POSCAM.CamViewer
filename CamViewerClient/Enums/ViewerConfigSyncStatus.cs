namespace CamViewerClient.Enums
{
    /// <summary>
    /// 로컬 캠뷰어 설정과 서버 설정의 동기화 상태를 정의한다.
    /// </summary>
    public enum ViewerConfigSyncStatus
    {
        /// <summary>
        /// 서버와 로컬 설정이 동기화된 상태.
        /// </summary>
        Synced = 0,

        /// <summary>
        /// 로컬 설정이 변경되었지만 서버에 업로드되지 않은 상태.
        /// </summary>
        LocalModified = 1
    }
}