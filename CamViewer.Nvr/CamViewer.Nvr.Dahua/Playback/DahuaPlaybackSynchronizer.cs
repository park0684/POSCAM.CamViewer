using CamViewer.Nvr.Core.Abstractions;
using CamViewer.Nvr.Core.Enums;
using CamViewer.Nvr.Core.Models;
using CamViewer.Nvr.Core.Results;
using CamViewer.Nvr.Dahua.Providers;
using CamViewer.Nvr.Dahua.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// Dahua 다중채널 재생 그룹의 실제 영상재생시간을 조회하고
    /// 채널 간 시간차를 Dahua 제조사 방식으로 보정한다.
    ///
    /// CamViewer 공통 프로젝트는 동기화 방식에 관여하지 않고
    /// INvrPlaybackEngine.SynchronizeAsync 명령만 호출한다.
    /// </summary>
    internal sealed class DahuaPlaybackSynchronizer
    {
        /// <summary>
        /// 채널 간 동기화 성공으로 인정할 최대 시간차.
        ///
        /// Dahua OSD는 초 단위로 갱신되므로
        /// 1초 이하는 정상적인 표시 차이로 허용한다.
        /// </summary>
        private const double AllowedDriftSeconds =
            1.0d;

        /// <summary>
        /// 동기화 보정 최대 시도 횟수.
        /// </summary>
        private const int MaximumSyncAttempts =
            3;

        private readonly DahuaNvrProvider _provider;

        /// <summary>
        /// Dahua 재생 동기화기를 초기화한다.
        /// </summary>
        public DahuaPlaybackSynchronizer(
            DahuaNvrProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(
                    "provider");
            }

            _provider =
                provider;
        }

        /// <summary>
        /// Dahua 재생 그룹의 채널 간 영상재생시간을 동기화한다.
        ///
        /// 처리 순서:
        /// 1. 그룹 상태 검증
        /// 2. 재생 중인 경우 전체 채널 Pause
        /// 3. 채널별 실제 OSD 영상재생시간 조회
        /// 4. 채널 간 최대 시간차 계산
        /// 5. 허용 범위를 초과한 채널만 기준 시각으로 정렬
        /// 6. 최대 3회까지 실제 OSD 시간을 다시 확인
        /// 7. 동기화 전 재생 중이었다면 전체 채널 Resume
        ///
        /// 동기화 실패 시에는 일부 채널만 재생되는 상태를 막기 위해
        /// 전체 그룹을 Paused 상태로 유지한다.
        /// </summary>
        public async Task<NvrResult> SynchronizeAsync(
            DahuaPlaybackGroupSession groupSession,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return CreateFailure(
                    NvrResultStatus.Cancelled,
                    "Dahua 재생 그룹 동기화 요청이 취소되었습니다.",
                    "DAHUA_GROUP_SYNC_CANCELLED",
                    "Synchronize");
            }

            if (groupSession == null)
            {
                return CreateFailure(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹 세션이 없습니다.",
                    "DAHUA_GROUP_SESSION_REQUIRED",
                    "Synchronize");
            }

            if (!groupSession.IsReady)
            {
                return CreateFailure(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹이 준비되지 않았습니다.",
                    "DAHUA_GROUP_NOT_READY",
                    "Synchronize");
            }

            if (groupSession.ChannelCount <= 0)
            {
                return CreateFailure(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹에 등록된 채널이 없습니다.",
                    "DAHUA_GROUP_CHANNEL_EMPTY",
                    "Synchronize");
            }

            /*
             * 현재 Dahua AlignPlaybackAsync는
             * 정방향 재생 세션 정렬만 지원한다.
             */
            if (groupSession.Direction
                != NvrPlaybackDirection.Forward)
            {
                return CreateFailure(
                    NvrResultStatus.NotSupported,
                    "현재 Dahua 그룹 동기화는 정방향 재생만 지원합니다.",
                    "DAHUA_GROUP_REVERSE_SYNC_NOT_SUPPORTED",
                    "Synchronize");
            }

            /*
             * 현재 Dahua 세션 정렬은 1배속에서만 보장된다.
             *
             * 배속 상태 동기화는 이후 SetSpeedAsync 구현과 함께
             * 임시 1배속 전환 및 기존 배속 복원 방식으로 확장한다.
             */
            if (groupSession.Speed
                != NvrPlaybackSpeed.Normal)
            {
                return CreateFailure(
                    NvrResultStatus.NotSupported,
                    "현재 Dahua 그룹 동기화는 1배속에서만 지원합니다.",
                    "DAHUA_GROUP_SYNC_SPEED_NOT_SUPPORTED",
                    "Synchronize");
            }

            if (groupSession.State != NvrPlaybackState.Playing
                && groupSession.State != NvrPlaybackState.Paused)
            {
                return CreateFailure(
                    NvrResultStatus.Failed,
                    "Dahua 재생 그룹을 동기화할 수 없는 상태입니다. "
                    + "State="
                    + groupSession.State,
                    "DAHUA_GROUP_INVALID_SYNC_STATE",
                    "Synchronize");
            }

            bool resumeAfterSynchronization =
                groupSession.State
                    == NvrPlaybackState.Playing;

            /*
             * 재생 중인 채널을 순서대로 측정하면
             * 측정 시점 차이 자체가 드리프트로 보일 수 있다.
             *
             * 따라서 실제 시간 측정과 정렬 전에
             * 전체 채널을 일시정지한다.
             */
            if (resumeAfterSynchronization)
            {
                NvrResult pauseResult =
                    await PauseAllChannelsAsync(
                        groupSession,
                        cancellationToken);

                if (pauseResult == null
                    || !pauseResult.Success)
                {
                    return pauseResult
                        ?? CreateFailure(
                            NvrResultStatus.Failed,
                            "Dahua 재생 그룹 일시정지 결과가 없습니다.",
                            "DAHUA_GROUP_SYNC_PAUSE_RESULT_EMPTY",
                            "Synchronize");
                }
            }

            double lastMeasuredDriftSeconds =
                double.MaxValue;

            for (int attempt = 1;
                attempt <= MaximumSyncAttempts;
                attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetSynchronizationStatus(
                        false,
                        null,
                        "Dahua 동기화 요청이 취소되어 "
                        + "일시정지 상태로 유지합니다.");

                    return CreateFailure(
                        NvrResultStatus.Cancelled,
                        "Dahua 재생 그룹 동기화 요청이 취소되었습니다.",
                        "DAHUA_GROUP_SYNC_CANCELLED",
                        "Synchronize");
                }

                NvrResult<IList<DahuaPlaybackTimeSnapshot>>
                    snapshotResult =
                        await ReadPlaybackSnapshotsAsync(
                            groupSession,
                            cancellationToken);

                if (snapshotResult == null
                    || !snapshotResult.Success
                    || snapshotResult.Data == null)
                {
                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    groupSession.SetSynchronizationStatus(
                        false,
                        null,
                        "Dahua 채널의 실제 영상재생시간을 "
                        + "확인하지 못했습니다.");

                    if (snapshotResult == null)
                    {
                        return CreateFailure(
                            NvrResultStatus.Failed,
                            "Dahua 채널 시간 조회 결과가 없습니다.",
                            "DAHUA_GROUP_SYNC_TIME_RESULT_EMPTY",
                            "Synchronize");
                    }

                    return NvrResult.Fail(
                        snapshotResult.Status,
                        snapshotResult.Message,
                        snapshotResult.Error);
                }

                IList<DahuaPlaybackTimeSnapshot> snapshots =
                    snapshotResult.Data;

                DateTime minimumCommonTime =
                    snapshots.Min(
                        item => item.CommonPlaybackTime);

                DateTime maximumCommonTime =
                    snapshots.Max(
                        item => item.CommonPlaybackTime);

                lastMeasuredDriftSeconds =
                    Math.Abs(
                        (
                            maximumCommonTime
                            - minimumCommonTime
                        ).TotalSeconds);

                /*
                 * 가장 빠른 채널과 가장 느린 채널의 차이가
                 * 허용 범위 이내라면 동기화 성공으로 판단한다.
                 *
                 * 정확히 같은 초가 표시되는지는 요구하지 않는다.
                 */
                if (lastMeasuredDriftSeconds
                    <= AllowedDriftSeconds)
                {
                    DahuaPlaybackTimeSnapshot referenceSnapshot =
                        SelectReferenceSnapshot(
                            snapshots);

                    groupSession.SetCurrentPlaybackTime(
                        referenceSnapshot.CommonPlaybackTime);

                    groupSession.SetSynchronizationStatus(
                        true,
                        lastMeasuredDriftSeconds,
                        "Dahua 채널 동기화가 완료되었습니다. "
                        + "최대 시간차="
                        + lastMeasuredDriftSeconds.ToString("0.0")
                        + "초");

                    if (resumeAfterSynchronization)
                    {
                        NvrResult resumeResult =
                            await ResumeAllChannelsAsync(
                                groupSession,
                                cancellationToken);

                        if (resumeResult == null
                            || !resumeResult.Success)
                        {
                            groupSession.SetState(
                                NvrPlaybackState.Paused);

                            return resumeResult
                                ?? CreateFailure(
                                    NvrResultStatus.Failed,
                                    "Dahua 재생 그룹 재개 결과가 없습니다.",
                                    "DAHUA_GROUP_SYNC_RESUME_RESULT_EMPTY",
                                    "Synchronize");
                        }
                    }
                    else
                    {
                        groupSession.SetState(
                            NvrPlaybackState.Paused);
                    }

                    return NvrResult.Ok(
                        "Dahua 재생 그룹 동기화가 완료되었습니다.");
                }

                /*
                 * 마지막 측정에서도 허용 범위를 초과했다면
                 * 더 이상 세션을 움직이지 않고 실패 처리한다.
                 */
                if (attempt >= MaximumSyncAttempts)
                {
                    break;
                }

                /*
                 * 정방향 재생에서는 가장 앞서 있는 채널의 시각을
                 * 다음 공통 정렬 목표로 사용한다.
                 *
                 * 이미 앞서 있는 채널을 과거로 되돌리지 않고
                 * 뒤처진 채널만 따라오게 하기 위한 정책이다.
                 */
                DateTime commonTargetTime =
                    ClampCommonPlaybackTime(
                        groupSession,
                        maximumCommonTime);

                foreach (DahuaPlaybackTimeSnapshot snapshot
                    in snapshots)
                {
                    double differenceFromTargetSeconds =
                        Math.Abs(
                            (
                                commonTargetTime
                                - snapshot.CommonPlaybackTime
                            ).TotalSeconds);

                    /*
                     * 이미 목표와 허용 범위 이내인 채널은
                     * 불필요하게 다시 Seek하지 않는다.
                     */
                    if (differenceFromTargetSeconds
                        <= AllowedDriftSeconds)
                    {
                        continue;
                    }

                    DateTime providerTargetTime =
                        snapshot.Channel.ToProviderTime(
                            commonTargetTime);

                    var alignmentRequest =
                        new NvrPlaybackAlignmentRequest
                        {
                            TargetTime =
                                providerTargetTime,

                            Direction =
                                NvrPlaybackDirection.Forward,

                            Speed =
                                NvrPlaybackSpeed.Normal,

                            /*
                             * 모든 채널 보정이 끝난 뒤
                             * 함께 Resume할 수 있도록 Paused 상태를 유지한다.
                             */
                            RemainPaused =
                                true
                        };

                    NvrResult<INvrPlaybackSession>
                        alignmentResult =
                            await _provider.AlignPlaybackAsync(
                                snapshot.Channel.Session,
                                alignmentRequest,
                                cancellationToken);

                    if (alignmentResult == null
                        || !alignmentResult.Success
                        || alignmentResult.Data == null)
                    {
                        groupSession.SetState(
                            NvrPlaybackState.Paused);

                        groupSession.SetSynchronizationStatus(
                            false,
                            lastMeasuredDriftSeconds,
                            "Dahua 채널 정렬에 실패하여 "
                            + "일시정지 상태로 유지합니다.");

                        if (alignmentResult == null)
                        {
                            return CreateFailure(
                                NvrResultStatus.Failed,
                                "Dahua 채널 정렬 결과가 없습니다.",
                                "DAHUA_GROUP_SYNC_ALIGNMENT_RESULT_EMPTY",
                                "Synchronize");
                        }

                        return NvrResult.Fail(
                            alignmentResult.Status,
                            alignmentResult.Message,
                            alignmentResult.Error);
                    }

                    DahuaPlaybackSession alignedSession =
                        alignmentResult.Data
                            as DahuaPlaybackSession;

                    if (alignedSession == null)
                    {
                        groupSession.SetState(
                            NvrPlaybackState.Paused);

                        groupSession.SetSynchronizationStatus(
                            false,
                            lastMeasuredDriftSeconds,
                            "Dahua 정렬 세션 형식이 올바르지 않습니다.");

                        return CreateFailure(
                            NvrResultStatus.Failed,
                            "Dahua Provider가 다른 형식의 "
                            + "재생 세션을 반환했습니다.",
                            "INVALID_DAHUA_ALIGNMENT_SESSION",
                            "Synchronize");
                    }

                    /*
                     * 현재 구현은 기존 DahuaPlaybackSession을
                     * 그대로 반환한다.
                     *
                     * 향후 Provider가 새 세션을 반환할 수 있으므로
                     * 동일 인스턴스가 아니면 그룹 참조를 교체한다.
                     *
                     * INvrPlaybackAlignmentProvider 계약상 새 세션을
                     * 반환한 경우 기존 네이티브 세션 정리는
                     * Provider 내부 책임이다.
                     */
                    if (!object.ReferenceEquals(
                            snapshot.Channel.Session,
                            alignedSession))
                    {
                        snapshot.Channel.ReplaceSession(
                            alignedSession);
                    }
                }
            }

            /*
             * 최대 보정 횟수를 모두 사용했지만
             * 채널 차이가 허용 범위 안으로 들어오지 않았다.
             */
            groupSession.SetState(
                NvrPlaybackState.Paused);

            groupSession.SetSynchronizationStatus(
                false,
                lastMeasuredDriftSeconds,
                "Dahua 채널 시간차가 허용 범위를 초과했습니다. "
                + "최대 시간차="
                + lastMeasuredDriftSeconds.ToString("0.0")
                + "초");

            return CreateFailure(
                NvrResultStatus.Failed,
                "Dahua 재생 그룹 동기화에 실패했습니다. "
                + "최대 시간차="
                + lastMeasuredDriftSeconds.ToString("0.0")
                + "초",
                "DAHUA_GROUP_SYNC_TOLERANCE_EXCEEDED",
                "Synchronize");
        }

        /// <summary>
        /// 현재 Dahua 채널들의 실제 OSD 영상재생시간을 읽고
        /// 채널별 공통 영상 시각으로 변환한다.
        /// </summary>
        private async Task<
            NvrResult<IList<DahuaPlaybackTimeSnapshot>>>
            ReadPlaybackSnapshotsAsync(
                DahuaPlaybackGroupSession groupSession,
                CancellationToken cancellationToken)
        {
            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            var snapshots =
                new List<DahuaPlaybackTimeSnapshot>();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return NvrResult<
                        IList<DahuaPlaybackTimeSnapshot>>.Fail(
                            NvrResultStatus.Cancelled,
                            "Dahua 채널 시간 조회가 취소되었습니다.",
                            CreateError(
                                "DAHUA_GROUP_SYNC_TIME_CANCELLED",
                                "Dahua 채널 시간 조회가 취소되었습니다.",
                                "ReadPlaybackSnapshots"));
                }

                if (channel == null
                    || channel.Session == null)
                {
                    return NvrResult<
                        IList<DahuaPlaybackTimeSnapshot>>.Fail(
                            NvrResultStatus.Failed,
                            "Dahua 채널 세션 정보가 없습니다.",
                            CreateError(
                                "DAHUA_GROUP_CHANNEL_SESSION_INVALID",
                                "Dahua 채널 세션 정보가 없습니다.",
                                "ReadPlaybackSnapshots"));
                }

                NvrResult<DateTime> timeResult =
                    await _provider.GetPlaybackTimeAsync(
                        channel.Session,
                        cancellationToken);

                if (timeResult == null
                    || !timeResult.Success)
                {
                    if (timeResult == null)
                    {
                        return NvrResult<
                            IList<DahuaPlaybackTimeSnapshot>>.Fail(
                                NvrResultStatus.Failed,
                                "Dahua 채널 시간 조회 결과가 없습니다.",
                                CreateError(
                                    "DAHUA_GROUP_SYNC_TIME_RESULT_EMPTY",
                                    "Dahua 채널 시간 조회 결과가 없습니다.",
                                    "ReadPlaybackSnapshots"));
                    }

                    return NvrResult<
                        IList<DahuaPlaybackTimeSnapshot>>.Fail(
                            timeResult.Status,
                            timeResult.Message,
                            timeResult.Error);
                }

                DateTime providerPlaybackTime =
                    TruncateToSecond(
                        timeResult.Data);

                DateTime commonPlaybackTime =
                    TruncateToSecond(
                        channel.ToCommonTime(
                            providerPlaybackTime));

                /*
                 * 비정상 OSD 값이 동기화 기준으로 사용되지 않도록
                 * 공통 조회 범위에서 크게 벗어난 값은 실패 처리한다.
                 *
                 * SDK 폴링과 OSD 갱신 오차를 고려해
                 * 조회 범위 전후 5초까지는 허용한다.
                 */
                DateTime minimumValidTime =
                    groupSession.StartTime.AddSeconds(-5);

                DateTime maximumValidTime =
                    groupSession.EndTime.AddSeconds(5);

                if (commonPlaybackTime < minimumValidTime
                    || commonPlaybackTime > maximumValidTime)
                {
                    return NvrResult<
                        IList<DahuaPlaybackTimeSnapshot>>.Fail(
                            NvrResultStatus.Failed,
                            "Dahua 채널에서 비정상적인 "
                            + "영상재생시간이 반환되었습니다. "
                            + "ChannelNo="
                            + channel.ChannelNo
                            + ", PlaybackTime="
                            + commonPlaybackTime.ToString(
                                "yyyy-MM-dd HH:mm:ss"),
                            CreateError(
                                "DAHUA_GROUP_SYNC_TIME_OUT_OF_RANGE",
                                "Dahua 채널 영상재생시간이 "
                                + "조회 범위를 벗어났습니다.",
                                "ReadPlaybackSnapshots"));
                }

                /*
                 * 채널 세션에는 Provider 원본 시각을 보관한다.
                 */
                channel.Session.SetCurrentPlaybackTime(
                    providerPlaybackTime);

                snapshots.Add(
                    new DahuaPlaybackTimeSnapshot
                    {
                        Channel =
                            channel,

                        ProviderPlaybackTime =
                            providerPlaybackTime,

                        CommonPlaybackTime =
                            commonPlaybackTime
                    });
            }

            if (snapshots.Count
                != channels.Count)
            {
                return NvrResult<
                    IList<DahuaPlaybackTimeSnapshot>>.Fail(
                        NvrResultStatus.Failed,
                        "일부 Dahua 채널의 영상재생시간을 "
                        + "확인하지 못했습니다.",
                        CreateError(
                            "DAHUA_GROUP_SYNC_TIME_INCOMPLETE",
                            "일부 Dahua 채널의 영상재생시간을 "
                            + "확인하지 못했습니다.",
                            "ReadPlaybackSnapshots"));
            }

            return NvrResult<
                IList<DahuaPlaybackTimeSnapshot>>.Ok(
                    snapshots,
                    "Dahua 채널 영상재생시간을 조회했습니다.");
        }

        /// <summary>
        /// 재생 중인 그룹의 모든 채널을 일시정지한다.
        ///
        /// 일부 채널 Pause 실패 시 이미 일시정지된 채널을
        /// 다시 Resume하여 원래 재생 상태로 복원한다.
        /// </summary>
        private async Task<NvrResult> PauseAllChannelsAsync(
            DahuaPlaybackGroupSession groupSession,
            CancellationToken cancellationToken)
        {
            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            var pausedChannels =
                new List<DahuaPlaybackGroupChannel>();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await ResumeChannelsAsync(
                        pausedChannels);

                    return CreateFailure(
                        NvrResultStatus.Cancelled,
                        "Dahua 그룹 동기화 일시정지가 취소되었습니다.",
                        "DAHUA_GROUP_SYNC_PAUSE_CANCELLED",
                        "PauseAllChannels");
                }

                if (channel == null
                    || channel.Session == null)
                {
                    await ResumeChannelsAsync(
                        pausedChannels);

                    return CreateFailure(
                        NvrResultStatus.Failed,
                        "Dahua 채널 세션 정보가 없습니다.",
                        "DAHUA_GROUP_CHANNEL_SESSION_INVALID",
                        "PauseAllChannels");
                }

                NvrResult pauseResult =
                    await _provider.PauseAsync(
                        channel.Session,
                        cancellationToken);

                if (pauseResult == null
                    || !pauseResult.Success)
                {
                    await ResumeChannelsAsync(
                        pausedChannels);

                    if (pauseResult == null)
                    {
                        return CreateFailure(
                            NvrResultStatus.Failed,
                            "Dahua 채널 일시정지 결과가 없습니다.",
                            "DAHUA_GROUP_SYNC_PAUSE_RESULT_EMPTY",
                            "PauseAllChannels");
                    }

                    return pauseResult;
                }

                pausedChannels.Add(
                    channel);
            }

            groupSession.SetState(
                NvrPlaybackState.Paused);

            return NvrResult.Ok(
                "Dahua 동기화를 위해 모든 채널을 "
                + "일시정지했습니다.");
        }

        /// <summary>
        /// 동기화 완료 후 모든 Dahua 채널을 재개한다.
        ///
        /// 일부 채널 Resume 실패 시 이미 재개된 채널을
        /// 다시 Pause하여 전체 그룹을 안전한 상태로 유지한다.
        /// </summary>
        private async Task<NvrResult> ResumeAllChannelsAsync(
            DahuaPlaybackGroupSession groupSession,
            CancellationToken cancellationToken)
        {
            IList<DahuaPlaybackGroupChannel> channels =
                groupSession.GetChannels();

            var resumedChannels =
                new List<DahuaPlaybackGroupChannel>();

            foreach (DahuaPlaybackGroupChannel channel
                in channels)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await PauseChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    return CreateFailure(
                        NvrResultStatus.Cancelled,
                        "Dahua 그룹 동기화 재개가 취소되었습니다.",
                        "DAHUA_GROUP_SYNC_RESUME_CANCELLED",
                        "ResumeAllChannels");
                }

                if (channel == null
                    || channel.Session == null)
                {
                    await PauseChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    return CreateFailure(
                        NvrResultStatus.Failed,
                        "Dahua 채널 세션 정보가 없습니다.",
                        "DAHUA_GROUP_CHANNEL_SESSION_INVALID",
                        "ResumeAllChannels");
                }

                NvrResult resumeResult =
                    await _provider.ResumeAsync(
                        channel.Session,
                        cancellationToken);

                if (resumeResult == null
                    || !resumeResult.Success)
                {
                    await PauseChannelsAsync(
                        resumedChannels);

                    groupSession.SetState(
                        NvrPlaybackState.Paused);

                    if (resumeResult == null)
                    {
                        return CreateFailure(
                            NvrResultStatus.Failed,
                            "Dahua 채널 재개 결과가 없습니다.",
                            "DAHUA_GROUP_SYNC_RESUME_RESULT_EMPTY",
                            "ResumeAllChannels");
                    }

                    return resumeResult;
                }

                resumedChannels.Add(
                    channel);
            }

            groupSession.SetState(
                NvrPlaybackState.Playing);

            return NvrResult.Ok(
                "Dahua 동기화 완료 후 모든 채널을 재개했습니다.");
        }

        /// <summary>
        /// Pause 처리 롤백을 위해 채널들을 다시 Resume한다.
        /// </summary>
        private async Task ResumeChannelsAsync(
            IList<DahuaPlaybackGroupChannel> channels)
        {
            for (int index = channels.Count - 1;
                index >= 0;
                index--)
            {
                DahuaPlaybackGroupChannel channel =
                    channels[index];

                if (channel == null
                    || channel.Session == null)
                {
                    continue;
                }

                try
                {
                    await _provider.ResumeAsync(
                        channel.Session,
                        CancellationToken.None);
                }
                catch
                {
                    /*
                     * 롤백 예외가 원래 오류를 덮어쓰지 않게 한다.
                     */
                }
            }
        }

        /// <summary>
        /// Resume 처리 롤백을 위해 채널들을 다시 Pause한다.
        /// </summary>
        private async Task PauseChannelsAsync(
            IList<DahuaPlaybackGroupChannel> channels)
        {
            for (int index = channels.Count - 1;
                index >= 0;
                index--)
            {
                DahuaPlaybackGroupChannel channel =
                    channels[index];

                if (channel == null
                    || channel.Session == null)
                {
                    continue;
                }

                try
                {
                    await _provider.PauseAsync(
                        channel.Session,
                        CancellationToken.None);
                }
                catch
                {
                    /*
                     * 롤백 예외가 원래 오류를 덮어쓰지 않게 한다.
                     */
                }
            }
        }

        /// <summary>
        /// 화면 왼쪽 채널을 우선 기준 시각으로 사용한다.
        ///
        /// 왼쪽 채널이 없으면 화면 위치가 가장 앞선 채널을 사용한다.
        /// </summary>
        private static DahuaPlaybackTimeSnapshot
            SelectReferenceSnapshot(
                IList<DahuaPlaybackTimeSnapshot> snapshots)
        {
            DahuaPlaybackTimeSnapshot leftSnapshot =
                snapshots.FirstOrDefault(
                    item =>
                        item.Channel != null
                        && item.Channel.ScreenPosition == 0);

            if (leftSnapshot != null)
            {
                return leftSnapshot;
            }

            return snapshots
                .OrderBy(
                    item => item.Channel.ScreenPosition)
                .First();
        }

        /// <summary>
        /// 동기화 목표 시각을 그룹 조회 범위 안으로 제한한다.
        /// </summary>
        private static DateTime ClampCommonPlaybackTime(
            DahuaPlaybackGroupSession groupSession,
            DateTime playbackTime)
        {
            if (playbackTime < groupSession.StartTime)
            {
                return groupSession.StartTime;
            }

            if (playbackTime >= groupSession.EndTime)
            {
                return groupSession.EndTime.AddSeconds(-1);
            }

            return playbackTime;
        }

        /// <summary>
        /// Dahua OSD 비교를 위해 밀리초를 제거한다.
        /// </summary>
        private static DateTime TruncateToSecond(
            DateTime value)
        {
            return new DateTime(
                value.Year,
                value.Month,
                value.Day,
                value.Hour,
                value.Minute,
                value.Second,
                value.Kind);
        }

        /// <summary>
        /// 동기화 실패 결과를 생성한다.
        /// </summary>
        private static NvrResult CreateFailure(
            NvrResultStatus status,
            string message,
            string errorCode,
            string operation)
        {
            return NvrResult.Fail(
                status,
                message,
                CreateError(
                    errorCode,
                    message,
                    operation));
        }

        /// <summary>
        /// 동기화 오류 정보를 생성한다.
        /// </summary>
        private static NvrErrorInfo CreateError(
            string errorCode,
            string errorMessage,
            string operation)
        {
            return new NvrErrorInfo
            {
                ErrorCode =
                    errorCode,

                ErrorMessage =
                    errorMessage,

                Operation =
                    operation
            };
        }

        /// <summary>
        /// Dahua 채널에서 측정한 실제 재생시간 정보.
        /// </summary>
        private sealed class DahuaPlaybackTimeSnapshot
        {
            /// <summary>
            /// 시간 측정 대상 채널.
            /// </summary>
            public DahuaPlaybackGroupChannel Channel
            {
                get;
                set;
            }

            /// <summary>
            /// Dahua OSD에서 반환된 원본 시각.
            /// </summary>
            public DateTime ProviderPlaybackTime
            {
                get;
                set;
            }

            /// <summary>
            /// 채널 보정값을 제거한 CamViewer 공통 시각.
            /// </summary>
            public DateTime CommonPlaybackTime
            {
                get;
                set;
            }
        }
    }
}
