using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AspNetCore_QnA_Bot.ClaimBot.Dialogs;
using AspNetCore_QnA_Bot.ClaimBot.Model;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Prompts;
using Microsoft.Bot.Builder.Prompts.Choices;
using Microsoft.Bot.Schema;
using Microsoft.Recognizers.Text;

using PromptsDialog = Microsoft.Bot.Builder.Dialogs;

namespace AspNetCore_QnA_Bot.ClaimBot
{
    public class AAAClaimBot : IBot
    {
        public static class PromptStep
        {
            public const string NamePrompt = "namePrompt";
            public const string GatherInfo = "gatherInfoPrompt";
        }

        private readonly DialogSet dialogs;
        private readonly ChoicePromptOptions cardOptions;

        private const string CARD_PROMPT_ID = "cardPrompt";
        private const string CARD_SELECTOR_ID = "cardSelector";

        private const string REPORT_ACCIDENT_CARD_ID = "Report an accident";

        private async Task NameValidator(ITurnContext context, TextResult result)
        {
            if (result.Value.Length <= 2)
            {
                result.Status = PromptStatus.NotRecognized;
                await context.SendActivity("Your name should be at least 2 characters long.");
            }
        }

        private async Task AskNameStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            await dialogContext.Prompt(PromptStep.NamePrompt, "What is your name?");
        }

        private async Task GatherInfoStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            var state = dialogContext.Context.GetConversationState<ClaimStateModel>();
            state.FullName = (result as TextResult).Value;
            await dialogContext.Context.SendActivity($"Your name is {state.FullName}, great!");
            await dialogContext.End();
        }

        public AAAClaimBot()
        {
            dialogs = new DialogSet();
            var cardPrompt = new PromptsDialog.ChoicePrompt(Culture.English)
            {
                Style = Microsoft.Bot.Builder.Prompts.ListStyle.List
            };
            cardOptions = GenerateOptions();

            dialogs.Add(PromptStep.NamePrompt,
                new PromptsDialog.TextPrompt(NameValidator));

            // Add a dialog that uses both prompts to gather information from the user
            dialogs.Add(PromptStep.GatherInfo,
                new WaterfallStep[] { AskNameStep, GatherInfoStep });

            dialogs.Add(CARD_PROMPT_ID, cardPrompt);
            dialogs.Add(CARD_SELECTOR_ID, new WaterfallStep[] {
                ChoiceCardStep,
                ShowCardStep
            });
            dialogs.Add("session", new StartSessionDialog());
        }

        private ChoicePromptOptions GenerateOptions()
        {
            return new ChoicePromptOptions()
            {
                Choices = new List<Choice>() {
                    new Choice()
                    {
                        Value = REPORT_ACCIDENT_CARD_ID,
                        Synonyms = new List<string>() { "1", "report an accident" }
                    },
                    new Choice()
                    {
                        Value = "session",
                        Synonyms = new List<string>() { "2", "start a session" }
                    }
                }
            };
        }

        private Task ChoiceCardStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            return dialogContext.Prompt(CARD_PROMPT_ID, "Let us further assist you. Please choose an option below\n", cardOptions);
        }

        private async Task ShowCardStep(DialogContext dialogContext, object result, SkipStepFunction next)
        {
            var selectedCard = (result as Microsoft.Bot.Builder.Prompts.ChoiceResult).Value.Value;
            var activity = dialogContext.Context.Activity;
            switch (selectedCard)
            {
                case REPORT_ACCIDENT_CARD_ID:
                    await dialogContext.Begin(PromptStep.GatherInfo);
                    break;

                case "session":
                    await dialogContext.Begin("session");
                    break;
            }
        }

        private Activity CreateResponse(Activity activity, Attachment attachment)
        {
            var response = activity.CreateReply();
            response.Attachments = new List<Attachment>()
            {
                attachment
            };
            return response;
        }

        public async Task OnTurn(ITurnContext context)
        {
            if (context.Activity.Type == ActivityTypes.Message && !context.Responded)
            {
                var state = context.GetConversationState<ClaimStateModel>();
                var dialogContext = dialogs.CreateContext(context, state);
                if (context.Activity.Type == ActivityTypes.Message)
                {
                    await dialogContext.Continue();
                    if (!context.Responded)
                    {
                        await dialogContext.Begin(CARD_SELECTOR_ID);
                    }
                }
            }
        }
    }
}
