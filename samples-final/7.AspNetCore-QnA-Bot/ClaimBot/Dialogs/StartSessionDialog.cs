namespace AspNetCore_QnA_Bot.ClaimBot.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;

#pragma warning disable 1998

    [Serializable]
    public class StartSessionDialog : IDialog
    {
        public Task DialogBegin(DialogContext dc, IDictionary<string, object> dialogArgs = null)
        {
            return dc.Context.SendActivity("welcome to the start session dialog");
        }
    }
}
