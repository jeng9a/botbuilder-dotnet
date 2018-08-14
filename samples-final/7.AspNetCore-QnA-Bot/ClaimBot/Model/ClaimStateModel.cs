
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AspNetCore_QnA_Bot.ClaimBot.Model
{
    [Serializable]
    public class ClaimStateModel : Dictionary<string, object>
    {
        private const string NameKey = "nameKey";

        public ClaimStateModel()
        {
            this[NameKey] = null;
        }

        public string FullName
        {
            get { return (string)this[NameKey]; }
            set { this[NameKey] = value; }
        }
    }
}
