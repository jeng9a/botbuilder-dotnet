﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Adapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Bot.Builder.Dialogs.Tests
{
    [TestClass]
    public class PromptValidatorContextTests
    {
        [TestMethod]
        public async Task PromptValidatorContextEnd()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var dialogState = convoState.CreateProperty<DialogState>("dialogState");

            var adapter = new TestAdapter()
                .Use(convoState);

            var dialogs = new DialogSet(dialogState);

            dialogs.Add(new TextPrompt("namePrompt", async (context, promptContext) =>
            {
                promptContext.End((string)promptContext.Recognized.Value);
            }));

            dialogs.Add(new WaterfallDialog("nameDialog", new WaterfallStep[]
                    {
                        async (dc, step) =>
                        {
                            return await dc.PromptAsync("namePrompt", "Please type your name.");
                        },
                        async (dc, step) =>
                        {
                            var name = (string)step.Result;
                            await dc.Context.SendActivityAsync($"{name} is a great name!");
                            return await dc.EndAsync();
                        }
                    }
                ));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                var dc = await dialogs.CreateContextAsync(turnContext);
                await dc.ContinueAsync();
                if (!turnContext.Responded)
                {
                    await dc.BeginAsync("nameDialog");
                }
            })
            .Send("hello")
            .AssertReply("Please type your name.")
            .Send("John")
            .AssertReply("John is a great name!")
            .Send("Hi again")
            .AssertReply("Please type your name.")
            .Send("1")
            .AssertReply("1 is a great name!")
            .StartTestAsync();
        }

        [TestMethod]
        public async Task PromptValidatorContextRetryEnd()
        {
            var convoState = new ConversationState(new MemoryStorage());
            var dialogState = convoState.CreateProperty<DialogState>("dialogState");

            TestAdapter adapter = new TestAdapter()
                .Use(convoState);

            DialogSet dialogs = new DialogSet(dialogState);

            // Create TextPrompt with dialogId "namePrompt" and custom validator
            dialogs.Add(new TextPrompt("namePrompt", async (context, promptContext) =>
            {
                string result = promptContext.Recognized.Value;
                if (result.Length > 3)
                {
                    promptContext.End(result);
                }
                else
                {
                    await context.SendActivityAsync("Please send a name that is longer than 3 characters.");
                }
            }));

            dialogs.Add(new WaterfallDialog("nameDialog", new WaterfallStep[]
                    {
                        async (dc, step) =>
                        {
                            return await dc.PromptAsync("namePrompt", "Please type your name.");
                        },
                        async (dc, step) =>
                        {
                            var name = (string)step.Result;
                            await dc.Context.SendActivityAsync($"{name} is a great name!");
                            return await dc.EndAsync();
                        }
                    }
                ));

            await new TestFlow(adapter, async (turnContext, cancellationToken) =>
            {
                var dc = await dialogs.CreateContextAsync(turnContext);
                await dc.ContinueAsync();
                if (!turnContext.Responded)
                {
                    await dc.BeginAsync("nameDialog");
                }
            })
            .Send("hello")
            .AssertReply("Please type your name.")
            .Send("hi")
            .AssertReply("Please send a name that is longer than 3 characters.")
            .Send("John")
            .AssertReply("John is a great name!")
            .StartTestAsync();
        }
    }
}
