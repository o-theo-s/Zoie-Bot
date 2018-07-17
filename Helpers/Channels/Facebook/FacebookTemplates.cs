using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zoie.Helpers.Channels.Facebook.Library;

namespace Zoie.Helpers
{
    public static partial class ChannelsHelper
    {
        public static partial class Facebook
        {
            public static class Templates
            {
                public static FacebookChannelData CreateListTemplate(
                    FacebookGenericTemplateContent[] contents,
                    FacebookButton bottomButton = null, 
                    string topElementStyle = "large")
                {
                    return new FacebookChannelData()
                    {
                        Attachment = new FacebookAttachment()
                        {
                            Payload = new FacebookGenericTemplate()
                            {
                                TemplateType = "list",
                                TopElementStyle = topElementStyle,
                                Elements = contents,
                                Buttons = bottomButton != null ? new[]
                                {
                                    bottomButton
                                } : null
                            }
                        }
                    };
                }
            }
        }
    }
}
