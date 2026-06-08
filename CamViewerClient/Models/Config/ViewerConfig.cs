using System;
using System.Collections.Generic;
using CamViewerClient.Enums;

namespace CamViewerClient.Models.Config
{
    /// <summary>
    /// 캠뷰어에서 사용하는 전체 설정 정보를 나타낸다.
    /// 서버 설정 다운로드, 업로드 및 로컬 설정 저장에 공통으로 사용한다.
    /// </summary>
    public sealed class ViewerConfig
    {
        /// <summary>
        /// 캠뷰어 설정을 초기화한다.
        /// </summary>
        public ViewerConfig()
        {
            NvrList = new List<NvrConfig>();
            CounterMapList = new List<CounterMap>();
            PlaybackOption = new PlaybackOption();
            SyncStatus = ViewerConfigSyncStatus.Synced;
            NextNvrNo = 1;
        }

        /// <summary>
        /// 서버 설정의 버전.
        /// 서버 설정이 변경될 때 증가하는 값을 사용한다.
        /// </summary>
        public long ConfigVersion { get; set; }

        /// <summary>
        /// 설정이 적용되는 매장 코드.
        /// </summary>
        public int StoreCode { get; set; }

        /// <summary>
        /// 서버와 로컬 설정의 동기화 상태.
        /// </summary>
        public ViewerConfigSyncStatus SyncStatus { get; set; }

        /// <summary>
        /// 서버 설정을 마지막으로 다운로드한 UTC 일시.
        /// 다운로드 이력이 없으면 null이다.
        /// </summary>
        public DateTime? LastDownloadedAtUtc { get; set; }

        /// <summary>
        /// 로컬 설정을 마지막으로 서버에 업로드한 UTC 일시.
        /// 업로드 이력이 없으면 null이다.
        /// </summary>
        public DateTime? LastUploadedAtUtc { get; set; }

        /// <summary>
        /// NVR 접속 설정 목록.
        /// </summary>
        public IList<NvrConfig> NvrList { get; private set; }

        /// <summary>
        /// 계산대별 NVR 채널 및 스크린위치 매핑 목록.
        /// </summary>
        public IList<CounterMap> CounterMapList { get; private set; }

        /// <summary>
        /// 영상 재생 시간 계산 옵션.
        /// </summary>
        public PlaybackOption PlaybackOption { get; set; }

        /// <summary>
        /// 신규 NVR 등록 시 사용할 다음 NVR번호.
        /// 삭제된 NVR번호를 다시 사용하지 않기 위해 설정에 함께 저장한다.
        /// </summary>
        public int NextNvrNo { get; set; }

        /// <summary>
        /// 영상 표시 방식.
        /// </summary>
        public VideoRenderMode VideoRenderMode { get; set; } =
            VideoRenderMode.KeepAspectRatio;


    }
}