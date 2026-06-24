using CamViewer.Nvr.Core.Models;
using System;

namespace CamViewer.Nvr.Dahua.Playback
{
    /// <summary>
    /// Dahua 다중채널 그룹에 포함된 하나의 채널이다.
    /// </summary>
    internal sealed class DahuaPlaybackGroupChannel
    {
        internal DahuaPlaybackGroupChannel(
            NvrPlaybackGroupChannelRequest request,
            DahuaPlaybackSession session)
        {
            if (request == null)
            {
                throw new ArgumentNullException(
                    "request");
            }

            if (session == null)
            {
                throw new ArgumentNullException(
                    "session");
            }

            Request =
                CloneRequest(
                    request);

            Session =
                session;
        }

        internal NvrPlaybackGroupChannelRequest Request
        {
            get;
            private set;
        }

        internal int ChannelNo
        {
            get
            {
                return Request.ChannelNo;
            }
        }

        internal int ScreenPosition
        {
            get
            {
                return Request.ScreenPosition;
            }
        }

        internal IntPtr RenderTargetHandle
        {
            get
            {
                return Request.RenderTargetHandle;
            }
        }

        internal int TimeOffsetSeconds
        {
            get
            {
                return Request.TimeOffsetSeconds;
            }
        }

        internal DahuaPlaybackSession Session
        {
            get;
            private set;
        }

        internal DateTime ToProviderTime(
            DateTime commonTime)
        {
            return commonTime.AddSeconds(
                TimeOffsetSeconds);
        }

        internal DateTime ToCommonTime(
            DateTime providerTime)
        {
            return providerTime.AddSeconds(
                -TimeOffsetSeconds);
        }

        internal void ReplaceSession(
            DahuaPlaybackSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(
                    "session");
            }

            Session =
                session;
        }

        private static NvrPlaybackGroupChannelRequest CloneRequest(
            NvrPlaybackGroupChannelRequest source)
        {
            return new NvrPlaybackGroupChannelRequest
            {
                ChannelNo =
                    source.ChannelNo,

                ScreenPosition =
                    source.ScreenPosition,

                RenderTargetHandle =
                    source.RenderTargetHandle,

                TimeOffsetSeconds =
                    source.TimeOffsetSeconds
            };
        }
    }
}
